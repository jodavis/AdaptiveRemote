"""Unit tests for AudioReader and AudioWriter implementations."""

from __future__ import annotations

import asyncio
import sys
from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.io.audio_io import AudioData, LibrosaAudioReader, SoundfileAudioWriter


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _write_stereo_wav(path: Path, sample_rate: int = 16000, duration_s: float = 0.1) -> None:
    """Write a stereo WAV fixture to path."""
    n_samples = int(sample_rate * duration_s)
    data = np.zeros((n_samples, 2), dtype=np.float32)
    data[:, 0] = 0.5  # left channel non-zero
    data[:, 1] = -0.5  # right channel non-zero
    sf.write(str(path), data, sample_rate, format="WAV", subtype="PCM_16")


def _write_mono_wav(path: Path, sample_rate: int = 16000, duration_s: float = 0.1) -> None:
    """Write a mono WAV fixture to path."""
    n_samples = int(sample_rate * duration_s)
    data = np.zeros(n_samples, dtype=np.float32)
    data[: n_samples // 2] = 0.3
    sf.write(str(path), data, sample_rate, format="WAV", subtype="PCM_16")


# ---------------------------------------------------------------------------
# TestLibrosaAudioReader
# ---------------------------------------------------------------------------


class TestLibrosaAudioReader:
    def test_read_stereo_returns_one_dimensional_array(self, tmp_path: Path) -> None:
        wav_path = tmp_path / "stereo.wav"
        _write_stereo_wav(wav_path)

        reader = LibrosaAudioReader()
        result = asyncio.run(reader.read(wav_path))

        assert isinstance(result, AudioData)
        assert result.samples.ndim == 1

    def test_read_stereo_returns_float32_dtype(self, tmp_path: Path) -> None:
        wav_path = tmp_path / "stereo.wav"
        _write_stereo_wav(wav_path)

        reader = LibrosaAudioReader()
        result = asyncio.run(reader.read(wav_path))

        assert result.samples.dtype == np.float32

    def test_read_mono_returns_one_dimensional_array(self, tmp_path: Path) -> None:
        wav_path = tmp_path / "mono.wav"
        _write_mono_wav(wav_path)

        reader = LibrosaAudioReader()
        result = asyncio.run(reader.read(wav_path))

        assert result.samples.ndim == 1

    def test_read_mono_returns_float32_dtype(self, tmp_path: Path) -> None:
        wav_path = tmp_path / "mono.wav"
        _write_mono_wav(wav_path)

        reader = LibrosaAudioReader()
        result = asyncio.run(reader.read(wav_path))

        assert result.samples.dtype == np.float32

    def test_read_returns_correct_sample_rate(self, tmp_path: Path) -> None:
        wav_path = tmp_path / "mono.wav"
        _write_mono_wav(wav_path, sample_rate=22050)

        reader = LibrosaAudioReader()
        result = asyncio.run(reader.read(wav_path))

        assert result.sample_rate == 22050

    def test_read_stereo_sample_count_matches_original(self, tmp_path: Path) -> None:
        sample_rate = 16000
        duration_s = 0.1
        wav_path = tmp_path / "stereo.wav"
        _write_stereo_wav(wav_path, sample_rate=sample_rate, duration_s=duration_s)

        reader = LibrosaAudioReader()
        result = asyncio.run(reader.read(wav_path))

        expected_samples = int(sample_rate * duration_s)
        assert len(result.samples) == expected_samples


# ---------------------------------------------------------------------------
# TestSoundfileAudioWriter
# ---------------------------------------------------------------------------


class TestSoundfileAudioWriter:
    def test_write_creates_file(self, tmp_path: Path) -> None:
        writer = SoundfileAudioWriter()
        out_path = tmp_path / "out.wav"
        data = np.zeros(1600, dtype=np.float32)
        asyncio.run(writer.write(out_path, data, 16000))

        assert out_path.exists()

    def test_write_produces_readable_wav(self, tmp_path: Path) -> None:
        writer = SoundfileAudioWriter()
        out_path = tmp_path / "out.wav"
        data = np.linspace(0, 1, 1600, dtype=np.float32)
        asyncio.run(writer.write(out_path, data, 16000))

        read_data, sr = sf.read(str(out_path))
        assert sr == 16000
        assert read_data.ndim == 1

    def test_write_preserves_sample_count(self, tmp_path: Path) -> None:
        writer = SoundfileAudioWriter()
        out_path = tmp_path / "out.wav"
        data = np.zeros(3200, dtype=np.float32)
        asyncio.run(writer.write(out_path, data, 16000))

        read_data, _ = sf.read(str(out_path))
        assert len(read_data) == 3200
