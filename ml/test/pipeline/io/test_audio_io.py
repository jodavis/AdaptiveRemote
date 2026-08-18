"""Unit tests for `LibrosaAudioReader`/`SoundfileAudioWriter`.

Per `ml/_spec_OopPipeline.md`'s Component Breakdown, both classes are Wrapper-tier (thin
call-throughs to `librosa.load`/`soundfile.write`), which would normally be exempt from unit
tests under the Wrapper/Testable/Orchestrator taxonomy. ADR-342's task brief explicitly
overrides that default and requires test-first coverage of "the protocol contract" -- these
tests cover that every call is forwarded with the right arguments, that the blocking library
call is offloaded to a thread pool (`asyncio.to_thread`) rather than run inline on the event
loop, and that a library exception propagates rather than being swallowed.
"""

from __future__ import annotations

import asyncio
from pathlib import Path
from unittest.mock import patch

import numpy as np
import pytest

from pipeline.io.audio_io import AudioData, LibrosaAudioReader, SoundfileAudioWriter


class TestLibrosaAudioReader:
    def test_LibrosaAudioReader_Read_CallsLibrosaLoadWithPathAndNoResampling(self) -> None:
        reader = LibrosaAudioReader()
        fake_samples = np.array([0.1, 0.2, 0.3], dtype=np.float32)
        path = Path("sample.wav")

        with patch("librosa.load", return_value=(fake_samples, 16000)) as mock_load:
            result = asyncio.run(reader.read(path))

        mock_load.assert_called_once_with(path, sr=None)
        assert result.sample_rate == 16000
        np.testing.assert_array_equal(result.samples, fake_samples)

    def test_LibrosaAudioReader_Read_CoercesFloatSampleRateToInt(self) -> None:
        reader = LibrosaAudioReader()
        fake_samples = np.array([0.1], dtype=np.float32)
        path = Path("sample.wav")

        with patch("librosa.load", return_value=(fake_samples, 16000.0)):
            result = asyncio.run(reader.read(path))

        assert result.sample_rate == 16000
        assert isinstance(result.sample_rate, int)

    def test_LibrosaAudioReader_Read_OffloadsLibrosaLoadToThreadPool(self) -> None:
        reader = LibrosaAudioReader()
        fake_samples = np.array([0.1], dtype=np.float32)
        path = Path("sample.wav")

        with (
            patch("librosa.load", return_value=(fake_samples, 16000)) as mock_load,
            patch("asyncio.to_thread", wraps=asyncio.to_thread) as mock_to_thread,
        ):
            asyncio.run(reader.read(path))

        mock_to_thread.assert_called_once_with(mock_load, path, sr=None)

    def test_LibrosaAudioReader_Read_LibrosaLoadRaises_PropagatesException(self) -> None:
        reader = LibrosaAudioReader()
        path = Path("sample.wav")

        with patch("librosa.load", side_effect=RuntimeError("boom")):
            with pytest.raises(RuntimeError, match="boom"):
                asyncio.run(reader.read(path))


class TestSoundfileAudioWriter:
    def test_SoundfileAudioWriter_Write_CallsSoundfileWriteWithPathSamplesAndSampleRate(
        self,
    ) -> None:
        writer = SoundfileAudioWriter()
        audio = AudioData(samples=np.array([0.1, 0.2], dtype=np.float32), sample_rate=16000)
        path = Path("out.wav")

        with patch("soundfile.write") as mock_write:
            asyncio.run(writer.write(path, audio))

        mock_write.assert_called_once_with(path, audio.samples, audio.sample_rate)

    def test_SoundfileAudioWriter_Write_OffloadsSoundfileWriteToThreadPool(self) -> None:
        writer = SoundfileAudioWriter()
        audio = AudioData(samples=np.array([0.1], dtype=np.float32), sample_rate=16000)
        path = Path("out.wav")

        with (
            patch("soundfile.write") as mock_write,
            patch("asyncio.to_thread", wraps=asyncio.to_thread) as mock_to_thread,
        ):
            asyncio.run(writer.write(path, audio))

        mock_to_thread.assert_called_once_with(mock_write, path, audio.samples, audio.sample_rate)

    def test_SoundfileAudioWriter_Write_SoundfileWriteRaises_PropagatesException(self) -> None:
        writer = SoundfileAudioWriter()
        audio = AudioData(samples=np.array([0.1], dtype=np.float32), sample_rate=16000)
        path = Path("out.wav")

        with patch("soundfile.write", side_effect=OSError("disk full")):
            with pytest.raises(OSError, match="disk full"):
                asyncio.run(writer.write(path, audio))
