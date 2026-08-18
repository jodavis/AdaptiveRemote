"""`AudioReader`/`AudioWriter`: the injectable audio I/O seam used by every pipeline stage
that reads or writes WAV files.

`Protocol` classes define the seam per "All stages get OOP classes" in
`ml/_spec_OopPipeline.md` -- structural typing, no inheritance required -- while
`LibrosaAudioReader`/`SoundfileAudioWriter` are thin, Wrapper-tier call-throughs to
`librosa.load`/`soundfile.write`. Per "No generic FileSystem/NetworkClient abstraction is
introduced" in the spec, this per-concern seam is the project's endorsed shape wherever I/O is
mixed with business logic worth unit-testing -- it is not meant to be generalized into a
broader filesystem abstraction.

Both concrete implementations offload their blocking library call to a worker thread via
`asyncio.to_thread`, since every stage's `transform()` runs inside an event loop
(`asyncio.run(stage.transform(...))`, per ADR-280) and must not block it on synchronous file
I/O. `LibrosaAudioReader.read` reads with `sr=None` so the file's native sample rate is
preserved rather than silently resampled to librosa's 22050 Hz default -- resampling, if ever
needed, is a decision for a stage/param to make explicitly, not something this seam should do
implicitly on every read.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

import numpy as np
import numpy.typing as npt


@dataclass
class AudioData:
    """In-memory audio: sample array plus its sample rate, mirroring `librosa.load`'s
    `(y, sr)` return shape and `soundfile.write`'s `(data, samplerate)` signature.
    """

    samples: npt.NDArray[np.float32]
    sample_rate: int


class AudioReader(Protocol):
    """Injectable seam for reading a WAV file (given an already-resolved absolute path) into
    `AudioData`. Callers resolve root-relative sample paths themselves -- see "Directories as
    stage I/O boundaries" in the spec.
    """

    async def read(self, path: Path) -> AudioData: ...


class AudioWriter(Protocol):
    """Injectable seam for writing `AudioData` to a WAV file at an already-resolved absolute
    path.
    """

    async def write(self, path: Path, audio: AudioData) -> None: ...


class LibrosaAudioReader:
    """Thin call-through to `librosa.load`, offloaded to a thread pool.

    Reads with `sr=None` to preserve the file's native sample rate -- see the module
    docstring. Wrapper-tier per the spec's Component Breakdown; the task brief overrides the
    taxonomy's normal Wrapper test exclusion and requires test-first coverage here.
    """

    async def read(self, path: Path) -> AudioData:
        import librosa  # deferred import, per ADR-281 "Static analysis via mypy --strict"

        samples, sample_rate = await asyncio.to_thread(librosa.load, path, sr=None)
        return AudioData(samples=samples, sample_rate=int(sample_rate))


class SoundfileAudioWriter:
    """Thin call-through to `soundfile.write`, offloaded to a thread pool.

    Wrapper-tier per the spec's Component Breakdown; the task brief overrides the taxonomy's
    normal Wrapper test exclusion and requires test-first coverage here.
    """

    async def write(self, path: Path, audio: AudioData) -> None:
        import soundfile  # type: ignore[import-untyped]  # deferred import; no stubs published

        await asyncio.to_thread(soundfile.write, path, audio.samples, audio.sample_rate)
