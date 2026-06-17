"""AudioReader and AudioWriter protocols with default librosa/soundfile implementations.

AudioReader returns a 1-D mono float32 numpy array regardless of the source channel
count. Stereo-to-mono conversion is the implementation's responsibility; consumers never
handle channel reduction.

AudioWriter writes the supplied mono float32 array to a WAV file (PCM_16 subtype).
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

import numpy as np


@dataclass
class AudioData:
    """Audio samples with their native sample rate.

    samples is always a 1-D mono float32 array. If the source file was stereo,
    the AudioReader implementation converts to mono before returning.
    """

    samples: np.ndarray
    sample_rate: int


class AudioReader(Protocol):
    """Read an audio file and return AudioData (1-D mono float32 samples + sample rate).

    If the source file is stereo, the implementation converts to mono before returning.
    """

    async def read(self, path: Path) -> AudioData: ...


class AudioWriter(Protocol):
    """Write AudioData (1-D float32 samples + sample rate) to a WAV file at the given path."""

    async def write(self, path: Path, audio: AudioData) -> None: ...


class LibrosaAudioReader:
    """Default AudioReader using librosa.

    librosa.load() handles format decoding and mono conversion internally via
    ``mono=True``. The returned array is always dtype float32, as librosa normalises
    to float32 by default.

    The synchronous librosa.load() call is offloaded to a thread pool executor so that
    the event loop is not blocked during file I/O.
    """

    async def read(self, path: Path) -> AudioData:
        import librosa  # deferred so unit tests without librosa installed can import module

        loop = asyncio.get_running_loop()
        samples, sample_rate = await loop.run_in_executor(
            None, lambda: librosa.load(str(path), sr=None, mono=True, dtype=np.float32)
        )
        return AudioData(samples=samples, sample_rate=int(sample_rate))


class SoundfileAudioWriter:
    """Default AudioWriter using soundfile (WAV, PCM_16 subtype).

    The synchronous sf.write() call is offloaded to a thread pool executor so that
    the event loop is not blocked during file I/O.
    """

    async def write(self, path: Path, audio: AudioData) -> None:
        import soundfile as sf  # deferred so unit tests without soundfile can import module

        loop = asyncio.get_running_loop()
        await loop.run_in_executor(
            None,
            lambda: sf.write(
                str(path), audio.samples, audio.sample_rate, format="WAV", subtype="PCM_16"
            ),
        )
