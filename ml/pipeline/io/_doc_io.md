# io Subpackage Architecture & Design

Summary: Describes the `AudioReader`/`AudioWriter` seam — the injectable, async I/O boundary every pipeline stage uses to read or write WAV files.

## Overview

The `io` subpackage is the pipeline's only place where WAV file I/O is mixed with business
logic worth unit-testing. It exposes two `Protocol` seams — `AudioReader` and `AudioWriter` —
plus their concrete `librosa`/`soundfile`-backed implementations, in
[`audio_io.py`](audio_io.py). Every future stage that touches audio on disk (`DelayAugmentor`,
`BackgroundNoiseAugmentor`, `MicrophoneNoiseAugmentor`, `SpectrogramStage`, and others per
`ml/_spec_OopPipeline.md`) is expected to depend on these seams rather than calling `librosa`/
`soundfile` directly.

## Responsibilities & Boundaries

- **Reading/writing WAV files:** `LibrosaAudioReader.read` and `SoundfileAudioWriter.write` are
  thin, Wrapper-tier call-throughs to `librosa.load`/`soundfile.write`.
- **In-memory audio representation:** `AudioData` holds a sample array plus its sample rate,
  mirroring `librosa.load`'s `(y, sr)` return shape and `soundfile.write`'s `(data,
  samplerate)` signature.
- **No path resolution:** callers pass already-resolved absolute `Path`s. Root-relative sample
  path resolution belongs to downstream stage/entry-point code (`ml/pipeline/stages/
  conventions.py`), not to this seam.
- **No generic filesystem abstraction:** per `ml/_spec_OopPipeline.md`'s "No generic
  FileSystem/NetworkClient abstraction is introduced," this is a narrow, per-concern seam for
  audio specifically — it is not meant to grow into a broader I/O abstraction.

## Key Design Decisions

- **`Protocol`, not `ABC`:** `AudioReader`/`AudioWriter` use structural typing since there is no
  shared implementation to inherit — consistent with the project-wide convention that
  injectable interfaces are `Protocol` classes.
- **Async seam, offloaded to a thread pool:** `read`/`write` are `async def` because every
  stage's `transform()` runs inside an event loop (`asyncio.run(stage.transform(...))`, per
  ADR-280) and must not block it on synchronous file I/O. The concrete implementations wrap
  their blocking library call in `asyncio.to_thread(...)`.
- **Native sample rate preserved on read:** `LibrosaAudioReader.read` passes `sr=None` to
  `librosa.load` so the file's native sample rate is kept rather than silently resampled to
  librosa's 22050 Hz default. Resampling, if ever needed, is left to a stage/param to decide
  explicitly.

## Usage Patterns & Limitations

- Stages depend on `AudioReader`/`AudioWriter` (the protocols), not the concrete
  `Librosa`/`Soundfile` classes, so tests can substitute fakes/mocks.
- Both concrete implementations use a deferred `import librosa` / `import soundfile` inside the
  method body (per ADR-281's static-analysis pattern for optional/heavy dependencies), rather
  than a module-level import.

## Testability

- The protocol contract (call-through arguments, thread-pool offload, exception propagation,
  native-sample-rate preservation) is unit-tested test-first in
  [`ml/test/pipeline/io/test_audio_io.py`](../../../test/pipeline/io/test_audio_io.py) — an
  explicit exception to the Wrapper tier's normal unit-test exemption, per the ADR-342 task
  brief.

## Updating This Document

Update this document only when the `io` subpackage's design or boundaries change (e.g. a new
seam is added, or the async/thread-pool offload strategy changes). For implementation details,
refer to [`audio_io.py`](audio_io.py) and its inline comments.
