## Summary: ml/pipeline/io

Audio I/O protocols and default implementations for the OOP pipeline. Provides a
shared abstraction that audio-manipulation stages use to read and write WAV files,
decoupling stages from specific libraries and enabling stub injection in tests.

## Protocols

| Class | Role |
|-------|------|
| [`AudioReader`](audio_io.py) | Read an audio file and return `(samples, sample_rate)` |
| [`AudioWriter`](audio_io.py) | Write a mono float32 array to a WAV file |

**`AudioReader` contract:** `async def read(self, path: Path) -> tuple[np.ndarray, int]`

The returned array is always 1-D mono float32, regardless of the source file's channel
count. Stereo-to-mono conversion is the implementation's responsibility. Consumers never
handle channel reduction themselves.

**`AudioWriter` contract:** `async def write(self, path: Path, data: np.ndarray, sample_rate: int) -> None`

Both are `Protocol` classes (structural typing; no inheritance required), following the
same pattern as `TtsProvider` and `NoiseProvider`.

## Default Implementations

| Class | Library | Notes |
|-------|---------|-------|
| [`LibrosaAudioReader`](audio_io.py) | librosa | Uses `librosa.load(..., mono=True, dtype=float32)` for format decoding and stereo-to-mono conversion |
| [`SoundfileAudioWriter`](audio_io.py) | soundfile | Writes WAV files with `PCM_16` subtype |

**Stereo-to-mono algorithm:** `librosa.load(..., mono=True)` is used rather than manual
channel averaging (e.g. `data.mean(axis=1)`). The spec specifies the outcome (1-D mono
float32) but not the algorithm; the librosa built-in is idiomatic and simpler.

**Thread pool offload:** Both implementations call synchronous disk I/O. To avoid
blocking the event loop, each call is offloaded to a thread pool executor via
`loop.run_in_executor(None, lambda: ...)`.

**Deferred imports:** `librosa` and `soundfile` are imported inside the method body
rather than at the module top level. This allows the module to be imported in
environments where these libraries are not installed (e.g. unit tests that supply stub
readers/writers).
