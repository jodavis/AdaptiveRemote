"""MicrophoneNoiseAugmentor: add Gaussian microphone noise to WAV audio samples.

mic_noise_amplitude is stored as 0.0 when should_vary returns False.
When amplitude == 0.0, the input sample is returned unchanged — no file is written.
Gaussian noise is seeded from output_seed for reproducibility.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

import numpy as np

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import MinMaxFilter, VariationGenerator
from pipeline.core.sample import AudioSample
from pipeline.io.audio_io import AudioData, AudioReader, AudioWriter
from pipeline.stages import conventions
from pipeline.stages.params import AddMicNoiseParams


class MicrophoneNoiseAugmentor(ModifierStage[AudioSample, AudioSample]):
    """Augmentation stage that adds Gaussian microphone noise to WAV samples.

    mic_noise_amplitude is stored as 0.0 when the noise is not applied. When
    amplitude == 0.0, the input sample is returned unchanged — no file is
    written and no I/O occurs.

    Gaussian noise uses output_seed for reproducibility via numpy's default_rng.

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
        params: AddMicNoiseParams,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._audio_reader = audio_reader
        self._audio_writer = audio_writer
        self._input_dir = input_dir
        self._vary_probability = params.vary_probability
        self._amplitude_filter = MinMaxFilter(params.amplitude_min, params.amplitude_max, precision=3)

    def _get_applied_values(
        self, sample: AudioSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        if generator.should_vary("mic_noise_amplitude", self._vary_probability):
            amplitude = generator.generate("mic_noise_amplitude", self._amplitude_filter)
        else:
            amplitude = 0.0

        return {
            "mic_noise_amplitude": float(amplitude),
        }

    def _derive_id(self, input_sample: AudioSample, applied_values: dict[str, Any]) -> str:
        amplitude: float = applied_values["mic_noise_amplitude"]
        if amplitude == 0.0:
            # No modification — return the input id unchanged.
            return input_sample.id
        return f"{input_sample.id}_mic{int(amplitude * 1000)}"

    async def _generate_output(
        self,
        input_sample: AudioSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> AudioSample:
        amplitude: float = applied_values["mic_noise_amplitude"]

        # When not applied, return the input sample unchanged — no I/O.
        if amplitude == 0.0:
            return input_sample

        input_path = self._input_dir / input_sample.path
        audio = await self._audio_reader.read(input_path)

        rng = np.random.default_rng(output_seed)
        noise = rng.normal(0, amplitude, len(audio.samples)).astype(np.float32)
        output_samples: np.ndarray = np.clip(audio.samples + noise, -1.0, 1.0).astype(np.float32)

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
