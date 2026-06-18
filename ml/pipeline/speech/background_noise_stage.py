"""BackgroundNoiseAugmentor: mix environmental noise into WAV audio samples.

noise_file is always chosen (choose() always called) for hash stability, even when
not applied. noise_start_s and noise_volume are stored as 0.0 when should_vary
returns False. All three keys (noise_file, noise_start_s, noise_volume) are always
present in every applied_values dict.

noise_start_s bounds are derived from file durations: both the noise file and the
audio sample file are read to compute max_start_s = noise_duration_s - audio_duration_s.
When the noise file is shorter than the audio sample, max_start_s is negative and
clamped to 0.0 (min and max both 0.0).
"""

from __future__ import annotations

import asyncio
import concurrent.futures
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
    """Protocol for listing available noise files.

    Consistent with TtsProvider living in tts_stage.py — the protocol belongs
    alongside the stage that uses it.
    """

    def list_files(self) -> list[Path]: ...


def _read_duration_s(audio_reader: AudioReader, path: Path) -> float:
    """Read a file and return its duration in seconds.

    Handles both a running event loop (uses a thread executor) and no loop
    (uses asyncio.run()).
    """
    async def _read() -> float:
        data = await audio_reader.read(path)
        return len(data.samples) / data.sample_rate

    try:
        loop = asyncio.get_running_loop()
    except RuntimeError:
        # No running loop — safe to call asyncio.run()
        return asyncio.run(_read())

    # Inside a running loop — run in a thread to avoid blocking the loop
    with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
        future = executor.submit(asyncio.run, _read())
        return future.result()


class BackgroundNoiseAugmentor(ModifierStage[AudioSample, AudioSample]):
    """Augmentation stage that mixes environmental noise into WAV samples.

    The noise file is always chosen (even when not applied) so that the
    content hash remains stable across configuration changes. Only
    noise_start_s and noise_volume are set to 0.0 when the noise is not applied.

    Noise mixing algorithm: slice noise at noise_start_s for len(audio) samples,
    zero-pad if noise is shorter after the slice, multiply by noise_volume, add to
    audio samples, then clip to [-1.0, 1.0].

    input_dir is the directory containing the input WAV files referenced by
    AudioSample.path. This is typically the output directory of the preceding stage.
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
        # Always choose a noise file for hash stability
        noise_files = sorted([p.name for p in self._noise_provider.list_files()])
        noise_file: str = generator.choose("noise_file", noise_files)

        if generator.should_vary("noise", self._vary_probability):
            # Derive noise_start_s bounds from file durations
            noise_path = self._noise_provider.list_files()[0].parent / noise_file
            audio_path = self._input_dir / sample.path

            noise_duration_s = _read_duration_s(self._audio_reader, noise_path)
            audio_duration_s = _read_duration_s(self._audio_reader, audio_path)

            max_start_s = noise_duration_s - audio_duration_s
            clamped_max = max(0.0, max_start_s)
            start_filter = MinMaxFilter(0.0, clamped_max, precision=2)
            noise_start_s = generator.generate("noise_start_s", start_filter)
            noise_volume = generator.generate("noise_volume", self._volume_filter)
        else:
            noise_start_s = 0.0
            noise_volume = 0.0

        return {
            "noise_file": noise_file,
            "noise_start_s": float(noise_start_s),
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
        noise_file: str = applied_values["noise_file"]
        noise_start_s: float = applied_values["noise_start_s"]
        noise_volume: float = applied_values["noise_volume"]

        input_path = self._input_dir / input_sample.path
        audio = await self._audio_reader.read(input_path)
        output_samples = audio.samples.copy()

        if noise_volume > 0.0:
            # Resolve noise file path via provider
            noise_path = self._noise_provider.list_files()[0].parent / noise_file
            noise_audio = await self._audio_reader.read(noise_path)

            start_sample = int(noise_start_s * noise_audio.sample_rate)
            n_needed = len(output_samples)
            noise_slice = noise_audio.samples[start_sample: start_sample + n_needed]

            # Zero-pad if noise slice is shorter than audio
            if len(noise_slice) < n_needed:
                noise_slice = np.pad(noise_slice, (0, n_needed - len(noise_slice)))

            output_samples = output_samples + noise_volume * noise_slice
            output_samples = np.clip(output_samples, -1.0, 1.0).astype(np.float32)

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "wav")
        await self._audio_writer.write(output_path, AudioData(samples=output_samples, sample_rate=audio.sample_rate))

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
