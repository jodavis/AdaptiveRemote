"""AudioReader and AudioWriter protocols with default librosa/soundfile implementations.

AudioReader returns a 1-D mono float32 numpy array regardless of the source channel
count. Stereo-to-mono conversion is the implementation's responsibility; consumers never
handle channel reduction.

AudioWriter writes the supplied mono float32 array to a WAV file (PCM_16 subtype).
"""

from __future__ import annotations

from pathlib import Path
from typing import Protocol

import numpy as np


class AudioReader(Protocol):
    """Read an audio file and return (samples, sample_rate).

    The returned array is always 1-D mono float32. If the source file is stereo,
    the implementation converts to mono before returning.
    """

    async def read(self, path: Path) -> tuple[np.ndarray, int]: ...


class AudioWriter(Protocol):
    """Write a 1-D float32 array to a WAV file at the given path."""

    async def write(self, path: Path, data: np.ndarray, sample_rate: int) -> None: ...


class LibrosaAudioReader:
    """Default AudioReader using librosa.

    librosa.load() handles format decoding and mono conversion internally via
    ``mono=True``. The returned array is always dtype float32, as librosa normalises
    to float32 by default.
    """

    async def read(self, path: Path) -> tuple[np.ndarray, int]:
        import librosa  # deferred so unit tests without librosa installed can import module

        data, sample_rate = librosa.load(str(path), sr=None, mono=True, dtype=np.float32)
        return data, int(sample_rate)


class SoundfileAudioWriter:
    """Default AudioWriter using soundfile (WAV, PCM_16 subtype)."""

    async def write(self, path: Path, data: np.ndarray, sample_rate: int) -> None:
        import soundfile as sf

        sf.write(str(path), data, sample_rate, format="WAV", subtype="PCM_16")
