"""SpectrogramStage: compute log-mel spectrograms from WAV audio samples.

Each AudioSample is transformed into a SampleSpectrogram whose .npy file
contains an (n_mels, time_steps) float32 array. The stage is deterministic
(_is_deterministic = True) so output_seed is always 0.

_get_applied_values returns {"n_mels": ..., "time_steps": ...} so that changing
spectrogram dimensions between runs changes the content_hash and triggers regen
of any previously-cached outputs that used different dimensions.

The mel spectrogram computation (librosa) is offloaded to a thread pool
executor so the asyncio event loop is not blocked by CPU-bound work.
"""

from __future__ import annotations

import asyncio
from pathlib import Path
from typing import Any, ClassVar

import numpy as np

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleSpectrogram
from pipeline.io.audio_io import AudioReader
from pipeline.stages import conventions


class SpectrogramStage(ModifierStage[AudioSample, SampleSpectrogram]):
    """Deterministic stage that converts WAV files to log-mel spectrogram .npy files.

    Output shape: (n_mels, time_steps). Short spectrograms are zero-padded on
    the right; long spectrograms are truncated from the right.

    _get_applied_values includes n_mels and time_steps so that changes to either
    invalidate previously-cached outputs via content_hash comparison.

    input_dir is the directory containing the WAV files referenced by
    AudioSample.path — typically the output directory of the preceding stage.
    """

    _is_deterministic: ClassVar[bool] = True

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        n_mels: int,
        time_steps: int,
        audio_reader: AudioReader,
        input_dir: Path,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._n_mels = n_mels
        self._time_steps = time_steps
        self._audio_reader = audio_reader
        self._input_dir = input_dir

    def _get_applied_values(
        self, sample: AudioSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        return {"n_mels": self._n_mels, "time_steps": self._time_steps}

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
        input_path = self._input_dir / input_sample.path
        audio = await self._audio_reader.read(input_path)

        loop = asyncio.get_running_loop()
        log_s = await loop.run_in_executor(
            None,
            lambda: self._compute_spectrogram(audio.samples, audio.sample_rate),
        )

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "npy")
        await loop.run_in_executor(None, lambda: np.save(str(output_path), log_s))

        content_hash = self._compute_content_hash(parent_content_hash, output_seed, applied_values)

        return SampleSpectrogram(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.npy"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.transcript,
            parent_id=input_sample.id,
        )

    def _compute_spectrogram(self, samples: np.ndarray, sample_rate: int) -> np.ndarray:
        """Compute log-mel spectrogram; pad or truncate to (n_mels, time_steps)."""
        import librosa  # deferred: unit tests without librosa can import module

        s = librosa.feature.melspectrogram(y=samples, sr=sample_rate, n_mels=self._n_mels)
        log_s = librosa.power_to_db(s, ref=np.max)

        if log_s.shape[1] < self._time_steps:
            pad_width = self._time_steps - log_s.shape[1]
            log_s = np.pad(log_s, ((0, 0), (0, pad_width)), mode="constant")
        else:
            log_s = log_s[:, : self._time_steps]

        return log_s.astype(np.float32)
