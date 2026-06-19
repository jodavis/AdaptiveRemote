"""Unit tests for SpectrogramStage."""

from __future__ import annotations

import asyncio
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleSpectrogram
from pipeline.io.audio_io import AudioData
from pipeline.speech.spectrogram_stage import SpectrogramStage


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str = "TV_ON_Jenny_r100",
    transcript: str = "TV_ON",
    sample_rate: int = 16000,
    duration_s: float = 0.5,
) -> AudioSample:
    content = f"{sample_id}:audio"
    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=content_hash,
        path=Path(f"{sample_id}.wav"),
        parent_content_hash="parent_hash",
        transcript=transcript,
        applied_values={},
    )


class _RecordingAudioReader:
    """Stub AudioReader that returns silence at a fixed sample rate."""

    def __init__(self, sample_rate: int = 16000, duration_s: float = 0.5) -> None:
        self.calls: list[Path] = []
        self._sample_rate = sample_rate
        self._duration_s = duration_s

    async def read(self, path: Path) -> AudioData:
        self.calls.append(path)
        n = int(self._sample_rate * self._duration_s)
        return AudioData(samples=np.zeros(n, dtype=np.float32), sample_rate=self._sample_rate)


def _make_stage(
    output_dir: Path,
    *,
    audio_reader: _RecordingAudioReader | None = None,
    n_mels: int = 80,
    time_steps: int = 200,
    sample_rate: int = 16000,
    duration_s: float = 0.5,
    input_dir: Path | None = None,
) -> tuple[SpectrogramStage, _RecordingAudioReader]:
    if audio_reader is None:
        audio_reader = _RecordingAudioReader(sample_rate=sample_rate, duration_s=duration_s)
    if input_dir is None:
        input_dir = output_dir
    stage = SpectrogramStage(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        audio_reader=audio_reader,
        input_dir=input_dir,
        sample_rate=sample_rate,
        n_mels=n_mels,
        time_steps=time_steps,
    )
    return stage, audio_reader


# ---------------------------------------------------------------------------
# TestIsDeterministic
# ---------------------------------------------------------------------------


class TestIsDeterministic:
    def test_is_deterministic_true(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        assert stage._is_deterministic is True


# ---------------------------------------------------------------------------
# TestGetAppliedValues
# ---------------------------------------------------------------------------


class TestGetAppliedValues:
    def test_get_applied_values_returns_empty_dict(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        result = stage._get_applied_values(sample, VariationGenerator(0))
        assert result == {}


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_returns_input_sample_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {})
        assert result == "TV_ON_Jenny_r100"

    def test_derive_id_returns_unchanged_id_for_different_samples(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="VOLUME_UP_Aria_r110")
        result = stage._derive_id(sample, {})
        assert result == "VOLUME_UP_Aria_r110"


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_output_npy_file_written(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=40, time_steps=100)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert (tmp_path / f"{sample.id}.npy").exists()

    def test_output_shape_is_n_mels_by_time_steps(self, tmp_path: Path) -> None:
        n_mels = 40
        time_steps = 100
        stage, _ = _make_stage(tmp_path, n_mels=n_mels, time_steps=time_steps)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        arr = np.load(tmp_path / f"{sample.id}.npy")
        assert arr.shape == (n_mels, time_steps)

    def test_output_short_audio_zero_padded_to_time_steps(self, tmp_path: Path) -> None:
        """Short audio (fewer frames than time_steps) is zero-padded."""
        stage, _ = _make_stage(tmp_path, n_mels=40, time_steps=500, duration_s=0.1)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        arr = np.load(tmp_path / f"{sample.id}.npy")
        assert arr.shape[1] == 500

    def test_output_long_audio_truncated_to_time_steps(self, tmp_path: Path) -> None:
        """Long audio (more frames than time_steps) is truncated from the end."""
        stage, _ = _make_stage(tmp_path, n_mels=40, time_steps=10, duration_s=2.0)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        arr = np.load(tmp_path / f"{sample.id}.npy")
        assert arr.shape[1] == 10

    def test_output_sample_parent_id_equals_input_sample_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].parent_id == "MY_SAMPLE"

    def test_output_sample_id_equals_input_sample_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].id == "MY_SAMPLE"

    def test_output_sample_transcript_preserved(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(transcript="VOLUME_UP")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].transcript == "VOLUME_UP"

    def test_output_sample_is_sample_spectrogram(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert isinstance(result.samples[0], SampleSpectrogram)

    def test_audio_reader_called_with_input_path(self, tmp_path: Path) -> None:
        reader = _RecordingAudioReader()
        input_dir = tmp_path / "input"
        input_dir.mkdir()
        stage, _ = _make_stage(tmp_path, audio_reader=reader, input_dir=input_dir)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert reader.calls[0] == input_dir / "TV_ON_Jenny_r100.wav"

    def test_second_run_skips_file_write(self, tmp_path: Path) -> None:
        """Deterministic stage: second run with same input reads manifest and skips."""
        reader = _RecordingAudioReader()
        stage, _ = _make_stage(tmp_path, audio_reader=reader)
        sample = _make_audio_sample()

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        calls_after_first = len(reader.calls)

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        # Second run should not call reader again (skip path)
        assert len(reader.calls) == calls_after_first
