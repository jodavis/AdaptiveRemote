"""SpectrogramStage: compute mel spectrograms from WAV audio samples.

Deterministic stage (seed=0). Writes {id}.npy of shape (n_mels, time_steps).
Long spectrograms are truncated from the end; short ones are zero-padded.
"""

from __future__ import annotations

import asyncio
from pathlib import Path
from typing import Any

import numpy as np

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleSpectrogram
from pipeline.io.audio_io import AudioReader
from pipeline.stages import conventions


class SpectrogramStage(ModifierStage[AudioSample, SampleSpectrogram]):
    """Compute mel spectrograms from WAV audio samples.

    Writes {id}.npy of shape (n_mels, time_steps).
    Truncates long spectrograms from the end; zero-pads short ones.
    """

    _is_deterministic: bool = True

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        audio_reader: AudioReader,
        input_dir: Path,
        sample_rate: int,
        n_mels: int,
        time_steps: int,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._audio_reader = audio_reader
        self._input_dir = input_dir
        self._sample_rate = sample_rate
        self._n_mels = n_mels
        self._time_steps = time_steps

    def _get_applied_values(
        self, sample: AudioSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        return {}

    def _derive_id(self, input_sample: AudioSample, applied_values: dict[str, Any]) -> str:
        return input_sample.id

    async def _generate_output(
        self,
        input_sample: AudioSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> SampleSpectrogram:
        import librosa  # deferred import to allow unit tests without librosa

        input_path = self._input_dir / input_sample.path
        audio = await self._audio_reader.read(input_path)

        loop = asyncio.get_running_loop()
        mel = await loop.run_in_executor(
            None,
            lambda: librosa.feature.melspectrogram(
                y=audio.samples,
                sr=audio.sample_rate,
                n_mels=self._n_mels,
            ),
        )

        # Truncate or zero-pad to exactly time_steps columns
        if mel.shape[1] >= self._time_steps:
            mel = mel[:, : self._time_steps]
        else:
            pad_width = self._time_steps - mel.shape[1]
            mel = np.pad(mel, ((0, 0), (0, pad_width)), mode="constant")

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "npy")
        np.save(str(output_path), mel)

        content_hash = self._compute_content_hash(
            parent_content_hash, output_seed, applied_values
        )

        return SampleSpectrogram(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.npy"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.transcript,
            parent_id=input_sample.id,
        )
