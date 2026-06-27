"""BackgroundNoiseAugmentor: mix environmental noise into WAV audio samples."""

from __future__ import annotations

from pathlib import Path
from typing import Any, Protocol

import numpy as np

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import MinMaxFilter, VariationGenerator
from pipeline.core.sample import AudioSample
from pipeline.io.audio_io import AudioData, AudioReader, AudioWriter
from pipeline.stages import conventions
from pipeline.stages.params import AddBackgroundNoiseParams


class NoiseProvider(Protocol):
    """Protocol for supplying pre-loaded noise audio data.

    Implementations load and resample noise files at construction time so that
    each call to list_files() is cheap and all returned audio uses the same
    sample rate.

    Returns a list of (filename, AudioData) pairs. The filename is used for
    hash-stable variation selection; AudioData is used for mixing.
    """

    def list_files(self) -> list[tuple[str, AudioData]]: ...


class BackgroundNoiseAugmentor(ModifierStage[AudioSample, AudioSample]):
    """Augmentation stage that mixes environmental noise into WAV samples.

    The noise file is always chosen (even when not applied) so that the
    content hash remains stable across configuration changes. Only
    noise_volume is set to 0.0 when the noise is not applied. noise_start_s
    is always 0.0 (noise always mixed from the start of the noise file).

    When noise_volume == 0.0, the input sample is returned unchanged — no
    file is written and no I/O occurs.

    Noise mixing (when applied): slice noise for len(audio) samples,
    zero-pad if noise is shorter, multiply by noise_volume, add, clip to [-1.0, 1.0].
    """

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        audio_reader: AudioReader,
        audio_writer: AudioWriter,
        input_dir: Path,
        noise_provider: NoiseProvider,
        params: AddBackgroundNoiseParams,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._audio_reader = audio_reader
        self._audio_writer = audio_writer
        self._input_dir = input_dir
        self._noise_provider = noise_provider
        self._vary_probability = params.vary_probability
        self._volume_filter = MinMaxFilter(params.volume_min, params.volume_max, precision=2)

    def _get_applied_values(
        self, sample: AudioSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        # Always choose a noise file for hash stability, even when not applied.
        # list_files() returns pre-loaded (name, AudioData) pairs.
        noise_items = self._noise_provider.list_files()
        noise_file: str = generator.choose("noise_file", sorted([name for name, _ in noise_items]))

        if generator.should_vary("noise", self._vary_probability):
            noise_volume = generator.generate("noise_volume", self._volume_filter)
        else:
            noise_volume = 0.0

        return {
            "noise_file": noise_file,
            "noise_start_s": 0.0,  # Always zero — noise is mixed from the start of the file.
            "noise_volume": float(noise_volume),
        }

    def _derive_id(self, input_sample: AudioSample, applied_values: dict[str, Any]) -> str:
        noise_file: str = applied_values["noise_file"]
        noise_volume: float = applied_values["noise_volume"]
        noise_filestem = Path(noise_file).stem
        return f"{input_sample.id}_{noise_filestem}_v{int(noise_volume * 100)}"

    async def _generate_output(
        self,
        input_sample: AudioSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> AudioSample:
        noise_volume: float = applied_values["noise_volume"]

        input_path = self._input_dir / input_sample.path
        audio = await self._audio_reader.read(input_path)

        if noise_volume > 0.0:
            noise_file: str = applied_values["noise_file"]
            noise_start_s: float = applied_values["noise_start_s"]

            # Look up the pre-loaded noise audio from the provider.
            noise_items = self._noise_provider.list_files()
            noise_audio = next(data for name, data in noise_items if name == noise_file)

            if noise_audio.sample_rate != audio.sample_rate:
                raise ValueError(
                    f"Noise file '{noise_file}' has sample_rate {noise_audio.sample_rate} Hz "
                    f"but input audio has sample_rate {audio.sample_rate} Hz. "
                    f"Configure _DirectoryNoiseProvider with the pipeline sample_rate so that "
                    f"noise files are resampled at load time."
                )

            start_sample = int(noise_start_s * noise_audio.sample_rate)
            n_needed = len(audio.samples)
            noise_slice = noise_audio.samples[start_sample: start_sample + n_needed]

            # Zero-pad if noise slice is shorter than audio
            if len(noise_slice) < n_needed:
                noise_slice = np.pad(noise_slice, (0, n_needed - len(noise_slice)))

            out_samples: np.ndarray = np.clip(
                audio.samples + noise_volume * noise_slice, -1.0, 1.0
            ).astype(np.float32)
            output_audio = AudioData(samples=out_samples, sample_rate=audio.sample_rate)
        else:
            output_audio = audio  # pass-through: no noise applied

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "wav")
        await self._audio_writer.write(output_path, output_audio)

        content_hash = self._compute_content_hash(
            parent_content_hash, output_seed, applied_values
        )

        return AudioSample(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.wav"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.transcript,
            applied_values=applied_values,
        )
