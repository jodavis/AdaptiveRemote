"""Unit tests for SpectrogramStage."""

from __future__ import annotations

import asyncio
import hashlib
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample
from pipeline.io.audio_io import AudioData
from pipeline.speech.spectrogram_stage import SpectrogramStage


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str = "TV_ON_Jenny_r100",
    transcript: str = "TV_ON",
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
    """Stub AudioReader that records read() calls and returns silence."""

    def __init__(self, n_mels: int = 40, sample_rate: int = 16000, duration_s: float = 0.5) -> None:
        self.calls: list[Path] = []
        self._sample_rate = sample_rate
        self._duration_s = duration_s

    async def read(self, path: Path) -> AudioData:
        self.calls.append(path)
        n_samples = int(self._sample_rate * self._duration_s)
        samples = np.zeros(n_samples, dtype=np.float32)
        return AudioData(samples=samples, sample_rate=self._sample_rate)


def _make_stage(
    output_dir: Path,
    *,
    n_mels: int = 40,
    time_steps: int = 32,
    audio_reader: _RecordingAudioReader | None = None,
    input_dir: Path | None = None,
) -> tuple[SpectrogramStage, _RecordingAudioReader]:
    if audio_reader is None:
        audio_reader = _RecordingAudioReader(n_mels=n_mels)
    if input_dir is None:
        input_dir = output_dir
    stage = SpectrogramStage(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        n_mels=n_mels,
        time_steps=time_steps,
        audio_reader=audio_reader,
        input_dir=input_dir,
    )
    return stage, audio_reader


# ---------------------------------------------------------------------------
# TestIsDeterministic
# ---------------------------------------------------------------------------


class TestIsDeterministic:
    def test_is_deterministic_class_var_is_true(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        assert stage._is_deterministic is True

    def test_is_deterministic_is_class_variable(self) -> None:
        assert SpectrogramStage._is_deterministic is True


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_returns_input_sample_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {})
        assert result == "TV_ON_Jenny_r100"

    def test_derive_id_returns_input_sample_id_different_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="VOLUME_UP_Aria_r110")
        result = stage._derive_id(sample, {})
        assert result == "VOLUME_UP_Aria_r110"

    def test_derive_id_ignores_applied_values(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = stage._derive_id(sample, {"some_key": "some_value"})
        assert result == "MY_SAMPLE"


# ---------------------------------------------------------------------------
# TestGetAppliedValues
# ---------------------------------------------------------------------------


class TestGetAppliedValues:
    def test_get_applied_values_includes_n_mels(self, tmp_path: Path) -> None:
        from pipeline.core.randomization import VariationGenerator

        stage, _ = _make_stage(tmp_path, n_mels=40, time_steps=32)
        sample = _make_audio_sample()
        result = stage._get_applied_values(sample, VariationGenerator(0))
        assert result["n_mels"] == 40

    def test_get_applied_values_includes_time_steps(self, tmp_path: Path) -> None:
        from pipeline.core.randomization import VariationGenerator

        stage, _ = _make_stage(tmp_path, n_mels=40, time_steps=32)
        sample = _make_audio_sample()
        result = stage._get_applied_values(sample, VariationGenerator(0))
        assert result["time_steps"] == 32

    def test_get_applied_values_reflects_different_n_mels(self, tmp_path: Path) -> None:
        from pipeline.core.randomization import VariationGenerator

        stage, _ = _make_stage(tmp_path, n_mels=80, time_steps=32)
        sample = _make_audio_sample()
        result = stage._get_applied_values(sample, VariationGenerator(0))
        assert result["n_mels"] == 80


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_parent_id_equals_input_sample_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].parent_id == "TV_ON_Jenny_r100"

    def test_parent_id_equals_input_sample_id_different_sample(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(sample_id="MUTE_Bob_r95")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].parent_id == "MUTE_Bob_r95"

    def test_output_npy_shape_is_n_mels_by_time_steps(self, tmp_path: Path) -> None:
        n_mels = 8
        time_steps = 16
        stage, _ = _make_stage(tmp_path, n_mels=n_mels, time_steps=time_steps)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        npy_path = tmp_path / "TV_ON_Jenny_r100.npy"
        assert npy_path.exists()
        data = np.load(str(npy_path))
        assert data.shape == (n_mels, time_steps)

    def test_output_npy_shape_with_different_dimensions(self, tmp_path: Path) -> None:
        n_mels = 20
        time_steps = 32
        stage, _ = _make_stage(tmp_path, n_mels=n_mels, time_steps=time_steps)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        npy_path = tmp_path / "TV_ON_Jenny_r100.npy"
        data = np.load(str(npy_path))
        assert data.shape == (n_mels, time_steps)

    def test_output_npy_dtype_is_float32(self, tmp_path: Path) -> None:
        """Spectrogram array is saved as float32 regardless of librosa output dtype."""
        n_mels = 8
        time_steps = 16
        stage, _ = _make_stage(tmp_path, n_mels=n_mels, time_steps=time_steps)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        npy_path = tmp_path / "TV_ON_Jenny_r100.npy"
        data = np.load(str(npy_path))
        assert data.dtype == np.float32

    def test_output_file_has_npy_extension(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].path == Path("MY_SAMPLE.npy")

    def test_output_calls_audio_reader(self, tmp_path: Path) -> None:
        stage, reader = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 1

    def test_output_sample_id_equals_input_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].id == "TV_ON_Jenny_r100"

    def test_transcript_preserved(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(transcript="CHANNEL_UP")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].transcript == "CHANNEL_UP"


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------


class TestSkipPath:
    def test_skip_path_does_not_call_reader_on_second_run(self, tmp_path: Path) -> None:
        """Second transform() call does not read audio again (skip-unchanged)."""
        stage, reader = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample()

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 1

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 1  # No additional call

    def test_skip_path_preserves_output_sample_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")

        result1 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        result2 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result2.samples[0].id == result1.samples[0].id

    def test_skip_path_preserves_parent_id(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, n_mels=8, time_steps=16)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")

        result1 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        result2 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result2.samples[0].parent_id == result1.samples[0].parent_id

    def test_changing_n_mels_triggers_regen(self, tmp_path: Path) -> None:
        """When n_mels changes between runs, audio reader is called again (regen path)."""
        reader = _RecordingAudioReader()
        stage1, _ = _make_stage(tmp_path, n_mels=8, time_steps=16, audio_reader=reader)
        sample = _make_audio_sample()
        asyncio.run(stage1.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 1

        stage2, _ = _make_stage(tmp_path, n_mels=16, time_steps=16, audio_reader=reader)
        asyncio.run(stage2.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 2  # regen triggered by n_mels change

    def test_changing_time_steps_triggers_regen(self, tmp_path: Path) -> None:
        """When time_steps changes between runs, audio reader is called again (regen path)."""
        reader = _RecordingAudioReader()
        stage1, _ = _make_stage(tmp_path, n_mels=8, time_steps=16, audio_reader=reader)
        sample = _make_audio_sample()
        asyncio.run(stage1.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 1

        stage2, _ = _make_stage(tmp_path, n_mels=8, time_steps=32, audio_reader=reader)
        asyncio.run(stage2.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 2  # regen triggered by time_steps change
