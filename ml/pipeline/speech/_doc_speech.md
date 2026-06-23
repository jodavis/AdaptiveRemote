## Summary: ml/pipeline/speech

Speech pipeline stages for the OOP pipeline. The directory contains three cohorts:

- **Augmentation stages** — `ModifierStage` subtypes that take an `AudioSample` (or
  `TextSample`) manifest and produce a derived `AudioSample` manifest with per-sample
  variation applied. These are non-deterministic: each variant has a unique `id` and
  `content_hash`.
- **Featurisation stages** — deterministic `ModifierStage` subtypes that convert
  `AudioSample` manifests into model-ready feature manifests (`SampleSpectrogram`,
  `SampleTokens`). The output `id` equals the input `AudioSample.id` (same stem,
  different extension), and `_get_applied_values` returns `{}`.
- **Splitter** — `SetManifestSplitter`, a plain (non-stage) class that partitions a
  fully-augmented `AudioSample` manifest into train/val/test subsets.
- **Training** — `ModelTrainer`, a plain class that trains a CTC speech-to-text Keras
  model from the featurised manifests and split produced by earlier pipeline stages.

## Stages

| Module | Class | Transform |
|--------|-------|-----------|
| [`tts_stage.py`](tts_stage.py) | `TtsSampleGenerator` | `TextSample → AudioSample` via edge_tts |
| [`delay_stage.py`](delay_stage.py) | `DelayAugmentor` | `AudioSample → AudioSample` (silence padding) |
| [`background_noise_stage.py`](background_noise_stage.py) | `BackgroundNoiseAugmentor` | `AudioSample → AudioSample` (environmental noise mix) |
| [`mic_noise_stage.py`](mic_noise_stage.py) | `MicrophoneNoiseAugmentor` | `AudioSample → AudioSample` (Gaussian mic noise) |
| [`spectrogram_stage.py`](spectrogram_stage.py) | `SpectrogramStage` | `AudioSample → SampleSpectrogram` (log-mel `.npy`) |
| [`token_stage.py`](token_stage.py) | `TokenStage` | `AudioSample → SampleTokens` (phoneme token `.json`) |
| [`set_splitter.py`](set_splitter.py) | `SetManifestSplitter` | Split augmented manifest into train/val/test |
| [`model_trainer.py`](model_trainer.py) | `ModelTrainer` | Train CTC model from filtered spectrogram/token manifests |

## Key design decisions

### Augmentation stages

**`TtsProvider` protocol as the synthesis seam.** `TtsSampleGenerator` accepts a
`TtsProvider` and never imports `edge_tts` directly — unit tests supply a stub, the
entry-point supplies `EdgeTtsProvider`. Retries are entirely `EdgeTtsProvider`'s
responsibility; the stage treats `synthesize()` as infallible.

**`applied_values` stores raw int for `speech_rate`, not the formatted string.**
The edge_tts rate string (e.g. `"+5%"`) is assembled inside `_generate_output` at
synthesis time and never persisted. Keeping the raw int stable means the content hash
does not change if the formatting logic changes.

**`AudioSample.transcript` = `TextSample.label` (the canonical command), not
`TextSample.content` (the spoken surface form).** TTS speaks the surface form (which
may include hesitations or pleasantries), but the model is trained to output the
canonical command. `_derive_id` also uses `input_sample.label` as the filename stem
prefix for the same reason.

**Voice list fetched and filtered in the entry-point, not in the stage.** The stage
accepts `list[str]` voices directly. The entry-point runs `asyncio.run(edge_tts.list_voices())`
and filters: `Gender == 'Female'`, `Locale == 'en-US'`, `':' not in ShortName`,
`'DragonHD' not in ShortName`, `'Turbo' not in ShortName`.

**`NoiseProvider` protocol as the noise-file seam for `BackgroundNoiseAugmentor`.**
Consistent with `TtsProvider` in `tts_stage.py` — the protocol lives in the same
module as the stage that uses it. Unit tests supply `_FakeNoiseProvider`; the
entry-point supplies `_DirectoryNoiseProvider` (globbing `*.wav` from `--noise-dir`).

**`noise_file` is always chosen (hash stability), `noise_volume` is 0.0 when not applied.**
`VariationGenerator.choose()` runs on the sorted filename list before the `should_vary`
check so the content hash does not change if `vary_probability` is toggled. All three
keys (`noise_file`, `noise_start_s`, `noise_volume`) are always present in `applied_values`.
`noise_start_s` is always 0.0 — noise is mixed from the beginning of the noise file.

**Gaussian noise in `MicrophoneNoiseAugmentor` is seeded from `output_seed`.** Uses
`np.random.default_rng(output_seed).normal(0, amplitude, len(samples))` for
reproducibility. The amplitude is stored as 0.0 when not applied.

### Featurisation stages

**`_is_deterministic = True` on both featurisation stages.** `ModifierStage` uses this
flag to force `output_seed = 0`. Because the transform is fully determined by the input
audio or transcript, no random variation is needed and `_get_applied_values` returns `{}`.

**`_derive_id` returns `input_sample.id` unchanged.** Featurisation stages preserve the
input filename stem (different extension: `.npy` for spectrograms, `.json` for tokens).
This differs from augmentation stages, which append a suffix to make each variant's id
unique. The shared `id` is what downstream consumers (`ModelTrainer`, `ModelEvaluator`)
use to join each feature file back to the split manifests.

**`SampleSpectrogram.parent_id` and `SampleTokens.parent_id` = `input_sample.id`.**
Set from the input `AudioSample.id`, not the `content_hash`. Used downstream to join
spectrogram/token outputs to the corresponding `AudioSample` in the split manifests.

**Spectrogram padding/truncation.** `SpectrogramStage` applies `librosa.power_to_db`
after `librosa.feature.melspectrogram`, then zero-pads short frames on the right
(`np.pad(mode='constant')`) or truncates long frames from the right (`log_s[:, :time_steps]`).
Output shape is always `(n_mels, time_steps)`.

**Blocking compute offloaded to thread pool.** Both the mel spectrogram computation
(`librosa.feature.melspectrogram`) and the file writes (`np.save`, `write_text`) are
offloaded via `asyncio.get_running_loop().run_in_executor(None, ...)` to avoid blocking
the event loop.

### SetManifestSplitter

**Splits the fully-augmented manifest, not clean audio.** The splitter receives the
`MicrophoneNoiseAugmentor` output manifest — the complete augmented dataset — before any
feature extraction. This ensures every augmented variant ends up in exactly one split.

**`seed=42` by default; uses stdlib `random.Random`, not numpy.** Shuffling is a pure
ordering operation with no numerical distribution requirements. `random.Random` is
sufficient and avoids adding a numpy dependency on a non-numerical operation.

**Writes via `conventions.split_manifest_path`.** Output filenames are
`train_manifest.json`, `val_manifest.json`, and `test_manifest.json` (the `_manifest`
suffix is required by `conventions.split_manifest_path`). `AudioSample.id` values are
preserved unchanged — no re-assignment in split outputs.

### ModelTrainer

**`KerasBackend` as the TF seam.** `ModelTrainer` accepts a `KerasBackend` Protocol
and never imports TensorFlow directly. `DefaultKerasBackend` is the production
implementation; it defers all `import tensorflow as tf` calls inside method bodies
(`build_ctc_model` and `_build_dataset`) so the module is importable without TF
installed (e.g. on CI without a GPU image). This mirrors the `TtsProvider`,
`NoiseProvider`, and `AudioReader` patterns used elsewhere in the pipeline.

**`ModelTrainer.train()` is synchronous.** The entry-point (`speech_10_train_model.py`)
calls it directly — no `asyncio.run()` wrapper is needed.

**Filtering is `ModelTrainer`'s responsibility.** `spectrogram_manifest` and
`token_manifest` are the full combined manifests (all splits). `train_manifest` is
the train-split subset produced by `SetManifestSplitter`. `ModelTrainer.train()`
filters internally: only entries whose `parent_id ∈ {s.id for s in train_manifest.samples}`
are included. Lookup dicts keyed by `parent_id` are built for both filtered sets before
any file I/O begins.

**Constructor-injected params.** `n_mels`, `time_steps`, `epochs`, and `batch_size` are
passed to `ModelTrainer.__init__()`. The trainer does not read `PipelineParams` directly —
the entry-point is responsible for bridging values from `PipelineParams.compute_spectrograms`
and `PipelineParams.train_model` to the constructor.

**Dataset construction is `_build_dataset`'s responsibility.** `ModelTrainer._build_dataset()`
stacks spectrogram and token arrays into numpy arrays, constructs a `tf.data.Dataset`
via `from_tensor_slices`, and applies `.batch(batch_size).prefetch(tf.data.AUTOTUNE)`.
`KerasBackend.train()` receives an already-batched, already-prefetched dataset.

**Empty-pair guard.** If filtering produces zero `(spectrogram, token)` pairs, a
`WARNING` is logged before calling `build_ctc_model` / `train`. This surfaces
misconfiguration (e.g. wrong manifest directories, split-manifest mismatch) that would
otherwise cause Keras to silently fit on zero steps.
