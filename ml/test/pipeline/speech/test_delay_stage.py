"""Unit tests for DelayAugmentor."""

from __future__ import annotations

import asyncio
import hashlib
import sys
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample
from pipeline.io.audio_io import AudioData
from pipeline.speech.delay_stage import DelayAugmentor
from pipeline.stages.params import AddDelaysParams


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str = "TV_ON_Jenny_r100",
    transcript: str = "TV_ON",
    sample_rate: int = 16000,
    duration_s: float = 0.1,
    wav_dir: Path | None = None,
) -> AudioSample:
    content = f"{sample_id}:audio"
    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
    path = Path(f"{sample_id}.wav")
    if wav_dir is not None:
        wav_path = wav_dir / path
        n_samples = int(sample_rate * duration_s)
        data = np.zeros(n_samples, dtype=np.float32)
        sf.write(str(wav_path), data, sample_rate, format="WAV", subtype="PCM_16")
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=content_hash,
        path=path,
        parent_content_hash="parent_hash",
        transcript=transcript,
        applied_values={},
    )


class _RecordingAudioReader:
    """Stub AudioReader that records read() calls and returns silence."""

    def __init__(self, sample_rate: int = 16000, duration_s: float = 0.1) -> None:
        self.calls: list[Path] = []
        self._sample_rate = sample_rate
        self._duration_s = duration_s

    async def read(self, path: Path) -> AudioData:
        self.calls.append(path)
        n_samples = int(self._sample_rate * self._duration_s)
        samples = np.zeros(n_samples, dtype=np.float32)
        return AudioData(samples=samples, sample_rate=self._sample_rate)


class _RecordingAudioWriter:
    """Stub AudioWriter that records write() calls and writes dummy bytes."""

    def __init__(self) -> None:
        self.calls: list[tuple[Path, np.ndarray, int]] = []

    async def write(self, path: Path, data: np.ndarray, sample_rate: int) -> None:
        self.calls.append((path, data, sample_rate))
        # Write an actual WAV file so the skip path can check file existence
        sf.write(str(path), data, sample_rate, format="WAV", subtype="PCM_16")


def _make_params(
    prefix_vary_probability: float = 0.0,
    prefix_min_s: float = 0.0,
    prefix_max_s: float = 0.1,
    suffix_vary_probability: float = 0.0,
    suffix_min_s: float = 0.0,
    suffix_max_s: float = 0.1,
) -> AddDelaysParams:
    return AddDelaysParams(
        prefix_vary_probability=prefix_vary_probability,
        prefix_min_s=prefix_min_s,
        prefix_max_s=prefix_max_s,
        suffix_vary_probability=suffix_vary_probability,
        suffix_min_s=suffix_min_s,
        suffix_max_s=suffix_max_s,
    )


def _make_stage(
    output_dir: Path,
    *,
    audio_reader: _RecordingAudioReader | None = None,
    audio_writer: _RecordingAudioWriter | None = None,
    input_dir: Path | None = None,
    params: AddDelaysParams | None = None,
    prefix_vary_probability: float = 0.0,
    prefix_min_s: float = 0.0,
    prefix_max_s: float = 0.1,
    suffix_vary_probability: float = 0.0,
    suffix_min_s: float = 0.0,
    suffix_max_s: float = 0.1,
) -> tuple[DelayAugmentor, _RecordingAudioReader, _RecordingAudioWriter]:
    if audio_reader is None:
        audio_reader = _RecordingAudioReader()
    if audio_writer is None:
        audio_writer = _RecordingAudioWriter()
    if input_dir is None:
        input_dir = output_dir
    if params is None:
        params = _make_params(
            prefix_vary_probability=prefix_vary_probability,
            prefix_min_s=prefix_min_s,
            prefix_max_s=prefix_max_s,
            suffix_vary_probability=suffix_vary_probability,
            suffix_min_s=suffix_min_s,
            suffix_max_s=suffix_max_s,
        )
    stage = DelayAugmentor(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        audio_reader=audio_reader,
        audio_writer=audio_writer,
        input_dir=input_dir,
        params=params,
    )
    return stage, audio_reader, audio_writer


# ---------------------------------------------------------------------------
# TestAppliedValues
# ---------------------------------------------------------------------------


class TestAppliedValues:
    def test_applied_values_has_prefix_delay_s_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert "prefix_delay_s" in av

    def test_applied_values_has_suffix_delay_s_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert "suffix_delay_s" in av

    def test_applied_values_has_exactly_two_keys(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert set(av.keys()) == {"prefix_delay_s", "suffix_delay_s"}

    def test_prefix_zero_when_vary_probability_is_zero(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(
            tmp_path,
            prefix_vary_probability=0.0,
            prefix_min_s=0.05,
            prefix_max_s=0.1,
        )
        sample = _make_audio_sample()
        for seed in range(10):
            av = stage._get_applied_values(sample, VariationGenerator(seed))
            assert av["prefix_delay_s"] == 0.0

    def test_suffix_zero_when_vary_probability_is_zero(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(
            tmp_path,
            suffix_vary_probability=0.0,
            suffix_min_s=0.05,
            suffix_max_s=0.1,
        )
        sample = _make_audio_sample()
        for seed in range(10):
            av = stage._get_applied_values(sample, VariationGenerator(seed))
            assert av["suffix_delay_s"] == 0.0

    def test_prefix_zero_stored_as_float(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, prefix_vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["prefix_delay_s"], float)

    def test_suffix_zero_stored_as_float(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, suffix_vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["suffix_delay_s"], float)

    def test_prefix_and_suffix_are_independent(self, tmp_path: Path) -> None:
        """prefix_vary_probability=1.0 and suffix=0.0 → prefix nonzero, suffix is 0."""
        stage, _, _ = _make_stage(
            tmp_path,
            prefix_vary_probability=1.0,
            prefix_min_s=0.1,
            prefix_max_s=0.1,
            suffix_vary_probability=0.0,
            suffix_min_s=0.1,
            suffix_max_s=0.5,
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["prefix_delay_s"] > 0.0
        assert av["suffix_delay_s"] == 0.0

    def test_suffix_applied_when_only_suffix_probability_is_one(
        self, tmp_path: Path
    ) -> None:
        stage, _, _ = _make_stage(
            tmp_path,
            prefix_vary_probability=0.0,
            prefix_min_s=0.1,
            prefix_max_s=0.5,
            suffix_vary_probability=1.0,
            suffix_min_s=0.1,
            suffix_max_s=0.1,
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["prefix_delay_s"] == 0.0
        assert av["suffix_delay_s"] > 0.0

    def test_prefix_value_is_float_type(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(
            tmp_path,
            prefix_vary_probability=1.0,
            prefix_min_s=0.1,
            prefix_max_s=0.1,
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["prefix_delay_s"], float)

    def test_suffix_value_is_float_type(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(
            tmp_path,
            suffix_vary_probability=1.0,
            suffix_min_s=0.1,
            suffix_max_s=0.1,
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["suffix_delay_s"], float)


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_format_with_both_delays(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r77")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.04, "suffix_delay_s": 0.02})
        assert result == "TV_ON_Jenny_r77_pre40_suf20"

    def test_derive_id_zero_delays_returns_base_id(self, tmp_path: Path) -> None:
        """When both delays are 0.0, no suffixes are appended."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.0, "suffix_delay_s": 0.0})
        assert result == "TV_ON_Jenny_r100"

    def test_derive_id_40ms_prefix_produces_pre40(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r77")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.040, "suffix_delay_s": 0.0})
        assert result == "TV_ON_Jenny_r77_pre40"

    def test_derive_id_omits_suf_when_suffix_is_zero(self, tmp_path: Path) -> None:
        """When suffix delay is 0.0, no suf segment is appended."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r77")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.040, "suffix_delay_s": 0.0})
        assert "suf" not in result

    def test_derive_id_omits_pre_when_prefix_is_zero(self, tmp_path: Path) -> None:
        """When prefix delay is 0.0, no pre segment is appended."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r77")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.0, "suffix_delay_s": 0.02})
        assert result == "TV_ON_Jenny_r77_suf20"
        assert "pre" not in result

    def test_derive_id_uses_input_id_as_prefix(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="VOLUME_UP_Aria_r110")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.04, "suffix_delay_s": 0.0})
        assert result.startswith("VOLUME_UP_Aria_r110_")

    def test_derive_id_truncates_to_milliseconds(self, tmp_path: Path) -> None:
        """int() truncation: 0.0499 → 49ms not 50ms."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="X")
        result = stage._derive_id(sample, {"prefix_delay_s": 0.0499, "suffix_delay_s": 0.0})
        assert "pre49" in result


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_generate_output_calls_audio_reader(self, tmp_path: Path) -> None:
        stage, reader, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        assert len(reader.calls) == 1

    def test_generate_output_calls_audio_writer(self, tmp_path: Path) -> None:
        stage, _, writer = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        assert len(writer.calls) == 1

    def test_output_audio_length_with_prefix_silence(self, tmp_path: Path) -> None:
        """100ms prefix added to 100ms audio → 200ms output."""
        sample_rate = 16000
        duration_s = 0.1
        prefix_s = 0.1
        stage, _, writer = _make_stage(
            tmp_path,
            prefix_vary_probability=1.0,
            prefix_min_s=prefix_s,
            prefix_max_s=prefix_s,
            suffix_vary_probability=0.0,
        )
        sample = _make_audio_sample(
            wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s
        )

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _path, written_data, _ = writer.calls[0]
        expected_len = int(sample_rate * (duration_s + prefix_s))
        assert len(written_data) == expected_len

    def test_output_audio_length_with_suffix_silence(self, tmp_path: Path) -> None:
        """100ms suffix added to 100ms audio → 200ms output."""
        sample_rate = 16000
        duration_s = 0.1
        suffix_s = 0.1
        stage, _, writer = _make_stage(
            tmp_path,
            prefix_vary_probability=0.0,
            suffix_vary_probability=1.0,
            suffix_min_s=suffix_s,
            suffix_max_s=suffix_s,
        )
        sample = _make_audio_sample(
            wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s
        )

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _path, written_data, _ = writer.calls[0]
        expected_len = int(sample_rate * (duration_s + suffix_s))
        assert len(written_data) == expected_len

    def test_output_audio_length_with_no_delays(self, tmp_path: Path) -> None:
        """No delays → output same length as input."""
        sample_rate = 16000
        duration_s = 0.1
        stage, _, writer = _make_stage(
            tmp_path,
            prefix_vary_probability=0.0,
            suffix_vary_probability=0.0,
        )
        sample = _make_audio_sample(
            wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s
        )

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _path, written_data, _ = writer.calls[0]
        assert len(written_data) == int(sample_rate * duration_s)

    def test_transcript_preserved_in_output_sample(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(transcript="VOLUME_UP", wav_dir=tmp_path)

        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        assert result.samples[0].transcript == "VOLUME_UP"

    def test_prefix_silence_is_zeros(self, tmp_path: Path) -> None:
        """The prepended samples must be zero (silence)."""
        sample_rate = 16000
        duration_s = 0.1
        prefix_s = 0.1
        n_prefix = int(sample_rate * prefix_s)

        stage, _, writer = _make_stage(
            tmp_path,
            prefix_vary_probability=1.0,
            prefix_min_s=prefix_s,
            prefix_max_s=prefix_s,
            suffix_vary_probability=0.0,
        )
        sample = _make_audio_sample(
            wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s
        )

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _path, written_data, _ = writer.calls[0]
        assert np.all(written_data[:n_prefix] == 0.0)

    def test_suffix_silence_is_zeros(self, tmp_path: Path) -> None:
        """The appended samples must be zero (silence)."""
        sample_rate = 16000
        duration_s = 0.1
        suffix_s = 0.1
        n_suffix = int(sample_rate * suffix_s)

        stage, _, writer = _make_stage(
            tmp_path,
            prefix_vary_probability=0.0,
            suffix_vary_probability=1.0,
            suffix_min_s=suffix_s,
            suffix_max_s=suffix_s,
        )
        sample = _make_audio_sample(
            wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s
        )

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _path, written_data, _ = writer.calls[0]
        assert np.all(written_data[-n_suffix:] == 0.0)


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------


class TestSkipPath:
    def test_skip_path_does_not_call_audio_reader_on_second_run(
        self, tmp_path: Path
    ) -> None:
        stage, reader, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        # First run — generates
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        initial_call_count = len(reader.calls)
        assert initial_call_count == 1

        # Second run — same input, same constraints → skip
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == initial_call_count

    def test_skip_path_does_not_call_audio_writer_on_second_run(
        self, tmp_path: Path
    ) -> None:
        stage, _, writer = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        initial_write_count = len(writer.calls)
        assert initial_write_count == 1

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(writer.calls) == initial_write_count

    def test_skip_path_preserves_output_sample_id(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        result1 = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )
        result2 = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        assert result2.samples[0].id == result1.samples[0].id
