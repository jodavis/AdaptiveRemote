# OOP ML Pipeline

> **Status:** Draft
> **Will become:** per-subpackage `_doc_*.md` files (`ml/pipeline/core/_doc_core.md`,
> `ml/pipeline/io/_doc_io.md`, `ml/pipeline/intent/_doc_intent.md`, `ml/pipeline/stages/_doc_stages.md`,
> `ml/pipeline/speech/_doc_speech.md`) as each subpackage's implementation is completed.
> **Epic:** [ADR-191](https://jodasoft.atlassian.net/browse/ADR-191) Refactor DVC pipeline into proper OOP design patterns

## Overview

Refactors the ML pipeline from numbered procedural scripts to a proper object-oriented design. Every pipeline stage is a Python class with injectable dependencies, enabling full unit testability with mocked collaborators — the same discipline used for the C# application code. A unified `Manifest[S]` / `Sample` abstraction replaces the existing multi-format CSV files and carries typed sample objects — with their applied values, seeds, and content hashes — through the entire pipeline, from intent phrase generation to model evaluation. The core innovation is a seed-based, precision-quantized randomisation algorithm that stabilises sample generation across experiment changes: widening a noise range regenerates only the samples whose applied values would actually change; all others reuse their existing files. DVC continues to orchestrate stage execution; thin entry-point scripts bridge DVC to the OOP classes.

This spec targets a full build of `ml/pipeline/` from scratch, developed test-first throughout. It supersedes the pre-refactor `ml/scripts/` procedural tree and an earlier, partial OOP implementation attempt under `ml/pipeline/` that is being discarded and rebuilt so the design can follow test-driven development from the first class written, rather than have tests retrofitted onto code that was written ahead of them. Several of the design decisions below were learned by building that earlier attempt — this draft folds those lessons in directly rather than leaving them to be rediscovered, applies the project's Wrapper/Testable/Orchestrator component taxonomy from the outset, and folds in two concerns identified while building the earlier attempt: stage-script boilerplate duplication ([ADR-280](https://jodasoft.atlassian.net/browse/ADR-280)) and the lack of static type checking ([ADR-281](https://jodasoft.atlassian.net/browse/ADR-281)).

"Discarded and rebuilt" means the entire existing `ml/pipeline/`/`ml/test/pipeline/` tree — including its two already-committed subpackage docs, `ml/pipeline/io/_doc_io.md` and `ml/pipeline/speech/_doc_speech.md`, which describe decisions this spec reverses (e.g. the old `AudioSample.transcript = TextSample.label` behavior the transcript decision below fixes, and the old `id`-named field this spec renames to `name`) — is deleted as the first implementation task, not selectively kept. Even parts that look compatible with this spec (e.g. `io/audio_io.py`'s `AudioReader`/`AudioWriter` design) are rebuilt from scratch test-first rather than reused as-is, since the point of the rebuild is the TDD discipline itself, not just the end-state code shape.

## Contents

- [Overview](#overview)
- [Responsibilities & Boundaries](#responsibilities--boundaries)
- [Key Design Decisions](#key-design-decisions)
- [Component Breakdown](#component-breakdown)
- [Planned Implementation](#planned-implementation)
- [Open Questions](#open-questions)
- [Task breakdown](#task-breakdown)
- [Related Docs](#related-docs)

## Responsibilities & Boundaries

- **Owns:** All pipeline stage logic (`ml/pipeline/`); `Manifest`/`Sample` data model and JSON serialisation; seed-based randomisation algorithm; `PassFilter` implementations; per-stage injectable protocol abstractions; DVC entry-point scripts; `dvc.yaml`; `params.yaml`; static analysis configuration for the `ml/` tree
- **Does not own:** Model architecture decisions beyond the CTC baseline; TensorFlow/Keras internals; the edge_tts service; the CMU Pronouncing Dictionary source data
- **Integrates with:** DVC (`params.yaml` for experiment parameters, `dvc.yaml` for stage wiring, S3 remote for artifact storage); edge_tts for TTS generation; TensorFlow/Keras for model training and evaluation; librosa/soundfile for audio I/O and spectrogram computation; CMU Pronouncing Dictionary for phoneme lookup

## Key Design Decisions

### All stages get OOP classes

_Context:_ Procedural scripts with module-level side effects, hardcoded paths, and `if __name__ == "__main__"` guards are difficult to unit test. The project requires the same testability standard as the C# application code.

_Decision:_ Every pipeline stage is a class with dependencies injected via its constructor. `Protocol` classes are the Python-native way to define injectable interfaces (structural typing — no inheritance required); `dataclass` is the natural container type. `ABC` is used only where there is real shared *implementation* to inherit — `ModifierStage` (skip-unchanged and GC logic shared by every per-sample stage) and `RandomizedModifierStage` (seed storage and the three-case skip/regen/new algorithm, layered on top for the four stages that vary samples). Stages with no shared implementation are standalone classes.

_Consequences:_ Every class is independently testable with mocked collaborators. Unit tests for `ml/pipeline/core/`, `ml/pipeline/intent/`, and every `ml/pipeline/speech/` stage are written test-first alongside each class, per the project's TDD practices, landing in `ml/test/pipeline/` mirroring the source layout.

---

### Unified end-to-end Manifest format

_Context:_ The pre-refactor pipeline used two distinct CSV formats — a variation CSV for augmentation stages and set-manifest CSVs for training/evaluation — making sample lineage opaque and parameter tracking fragile.

_Decision:_ A single `Manifest[S]` class (generic over a `Sample` subtype) replaces both formats. It serialises to JSON (schema version 1) and carries `TextSample`, `AudioSample`, `SampleSpectrogram`, or `SampleTokens` objects with their applied values, seeds, and content hashes. Train/val/test splits are three separate files of the same format, named with a `_manifest` suffix (`train_manifest.json`, `val_manifest.json`, `test_manifest.json` — see `conventions.split_manifest_path`).

_Consequences:_ All consumers share one JSON schema. `Manifest.__init__` raises `ValueError` on duplicate sample names; `ManifestStore.write` raises `ValueError` on an empty manifest or mixed sample types within one manifest — both are load-bearing correctness checks, not incidental validation.

---

### ModifierStage for all per-sample file transformations

_Context:_ The skip-unchanged and GC logic is valuable for any stage that transforms files one-by-one — not just data augmentation. But only four of the six such stages actually vary their output via a seed; forcing the other two (`SpectrogramStage`, `TokenStage`) to carry seed-related fields and logic they never use just to share the skip/GC base class would be an unnecessary leak.

_Decision:_ `ModifierStage[T_in, T_out]` (`ml/pipeline/core/modifier_stage.py`) is the seedless base class: it implements skip-unchanged (matching by `parent_content_hash`) and GC, used directly by the two deterministic featurisation stages, `SpectrogramStage` and `TokenStage`. `RandomizedModifierStage[T_in, T_out](ModifierStage[T_in, T_out])` adds seed generation/storage and the three-case skip/regen-with-stored-seed/new-sample algorithm on top, used by the four stages that vary samples via `VariationGenerator`: `TtsSampleGenerator`, `DelayAugmentor`, `BackgroundNoiseAugmentor`, `MicrophoneNoiseAugmentor`. `PhraseVariator` (intent phrase generation) and `SetManifestSplitter` are plain classes, not part of this hierarchy at all — see their own sections below.

_Consequences:_ `SampleSpectrogram` and `SampleTokens` carry no `seed` field at all — only samples produced by a `RandomizedModifierStage` subclass do. `RandomizedModifierStage.transform()`'s three-case algorithm (skip / regen-with-stored-seed / new-sample) is the highest-risk logic in the pipeline and should get the deepest unit-test coverage of any single component; the base `ModifierStage`'s two-case skip/regenerate algorithm is simpler but still needs coverage of both cases.

---

### Content hash determines sample identity

_Context:_ The goal is to skip regeneration for samples whose upstream source did not change, while regenerating those that did. DVC can skip a whole stage if no deps changed, but cannot skip individual samples within a stage.

_Decision:_ All non-text samples use a unified content hash formula, split across the two `ModifierStage` levels — each implemented once as the single source of truth for its level, and every `_generate_output` override must call the one from its own base class, never reimplement it. Base `ModifierStage._compute_content_hash` (no seed):

```
content_hash = sha256(parent_content_hash + ":" + canonical(applied_values))
```

`RandomizedModifierStage._compute_content_hash` extends this with a seed term:

```
content_hash = sha256(parent_content_hash + ":" + str(seed) + ":" + canonical(applied_values))
```

where `canonical(applied_values) = json.dumps(applied_values, sort_keys=True, separators=(',',':'), ensure_ascii=True)`. All numeric values in `applied_values` are raw int/float, never formatted strings, so hash stability doesn't depend on formatting logic.

For `TextSample`: `content_hash = sha256(content.encode('utf-8'))` (no parent — see the content-addressable `TextSample.name` decision below).

_Consequences:_ A sample regenerates only when its source content or applied values change. `ModifierStage` output dirs are configured `persist: true` in `dvc.yaml` so DVC doesn't delete them between runs; GC (flat-glob the output dir, delete anything not in the new sample set and not named `manifest.json`) handles orphaned files itself. **GC is always scoped to `self._output_dir.glob("*")` — physical files in the current stage's own directory — never to paths resolved through `data_root`.** This matters because a passthrough sample's `path` can point at an ancestor stage's directory (per the root-relative-paths decision below); if that sample stops passing through unmodified on a later run, its old manifest entry disappears, but there was never a physical file for it inside the current stage's own directory to begin with, so GC has nothing to wrongly delete. GC matches by `Path(sample.path).name` (basename only) against files actually present in `self._output_dir`, so it can never enumerate or delete anything from a different stage's directory. This must be covered by a regression test asserting GC never touches files outside `self._output_dir`, using a passthrough sample whose path points at an ancestor directory as the fixture.

---

### TextSample.name is content-addressable, not a random UUID

_Context:_ An earlier draft of this design assigned `TextSample.name = uuid4()` at construction. That approach was tried in the discarded implementation attempt and found to make `PhraseVariator` output non-reproducible in identity terms: an unchanged base phrase would get a new random name on every `dvc repro`, making every downstream `TtsSampleGenerator` sample look "new" (no `parent_content_hash` match) even when nothing changed — defeating the whole skip-unchanged design for the very first stage.

_Decision:_ `TextSample.name` is set in `__post_init__` to equal `self.content_hash` (`ml/pipeline/core/sample.py`). Since `content_hash = sha256(content)`, two `TextSample`s with identical surface-form text always have the same name. `name` (renamed from an earlier `id`) is deliberately not called "id" — `content_hash` is the field used for every actual identity/equality comparison in the pipeline (`parent_content_hash` matching drives the skip/regen algorithm); `name` only serves manifest-uniqueness, on-disk filenames, and cross-manifest joins (`parent_name`), so naming it `id` invited reading it as the identity field when it isn't.

_Consequences:_ `PhraseVariator` itself needs no skip-unchanged mechanism (it isn't a `ModifierStage`), but its output is stable enough that downstream stages correctly treat unchanged phrases as unchanged.

---

### Precision-quantized rejection sampling

_Context:_ A continuous-interpolation approach to rejection-sampling candidates (`candidate = low + (raw/2^64) * (high - low)`, drawn as continuous floats uniformly across the full pass-filter domain) was tried in the discarded implementation attempt and found to make regeneration nearly universal on any constraint change: moving `max_val` even slightly shifts almost every candidate's exact float value, so a `_derive_name` suffix like `int(value * 1000)` almost never matches between runs, and skip-unchanged detection rarely fires for anything but a truly frozen constraint set.

_Decision:_ `PassFilter` (`ml/pipeline/core/randomization.py`) takes a `precision: int = 0` constructor parameter (decimal places). At construction it computes a quantization grid: `scale = 10**precision`, shifted/biased integer bounds, and a power-of-2 range (`_pow2_range`) sized to cover that grid. `VariationGenerator.generate()` draws a candidate via `raw % pow2_range` over that quantized grid rather than continuous interpolation. Each stage picks its own precision to match the granularity that its `_derive_name` suffix needs: `DelayAugmentor` uses `precision=1` (`int(value * 10)` in names), `BackgroundNoiseAugmentor` uses `precision=2` (`int(value * 100)`), `MicrophoneNoiseAugmentor` uses `precision=3` (`int(value * 1000)`).

_Consequences:_ Candidates are drawn from a finite, precision-sized grid, so most values stay stable across small constraint changes — the skip-unchanged design actually works, not just in theory. `MinMaxFilter`/`NormalFilter` constructors both accept `precision`.

---

### Seed-based randomisation with pass filters

_Context:_ Experiments frequently adjust variation constraints. Without a stable algorithm, all samples would regenerate on every constraint change.

_Decision:_ Each new output sample gets a seed via `int.from_bytes(os.urandom(8), 'big')`, stored in the manifest and reused on subsequent runs when the input is unchanged (matched by `parent_content_hash`). Per-variable values are derived via `VariationGenerator`:
- `should_vary(name, frequency)` — `sha256(f"{seed}:{name}:vary")`-derived probability check.
- `generate(name, pass_filter)` — quantized rejection sampling (see above); raises `ValueError` after 1000 attempts.
- `generate_int(name, pass_filter)` — bitmask rejection sampling over `[int(min_val), int(max_val)]`; returns `min_val` immediately when the range is 0.
- `choose(name, options)` — direct selection via `sha256(f"{seed}:{name}:0") % len(options)`, no rejection loop.

_Consequences:_ Each variable's value is independent of ordering — adding or reordering variables in `_get_applied_values` doesn't affect existing variables' values. This independence property should be covered directly by a unit test, since it is easy to accidentally break by threading shared state through multiple `generate()` calls.

---

### Previous output manifest as the seed store

_Context:_ Seeds must persist across DVC reruns for the four `RandomizedModifierStage` subclasses; the two deterministic `ModifierStage` subclasses (`SpectrogramStage`, `TokenStage`) read the previous output manifest too, just for skip detection, not seed persistence.

_Decision:_ Both levels read the previous output manifest (if present) and index it by `parent_content_hash`. Base `ModifierStage.transform()` has two cases per input sample:
- **Skip** — previous output found and its `content_hash` still matches → keep the previous output object unchanged.
- **New/changed sample** — no previous output found, or its `content_hash` no longer matches → re-run `_generate_output`.

`RandomizedModifierStage.transform()` extends this into three cases, since a stored seed can reproduce the *same* `content_hash` even when regeneration is needed:
- **Skip** — previous output found and re-deriving applied values with the stored seed reproduces the same `content_hash` → keep the previous output object unchanged.
- **Regenerate with stored seed** — previous output found but constraints changed (different applied values) → re-run `_generate_output` with the same seed and a freshly-derived name.
- **New sample** — no previous output found → fresh seed via `os.urandom`, new name.

_Consequences:_ Deleting the output manifest resets all seeds for `RandomizedModifierStage` subclasses (and simply forces full recompute for base `ModifierStage` subclasses, which have no seed to lose). `dvc.yaml` outputs for every stage in this hierarchy must be `persist: true`.

---

### Directories as stage I/O boundaries: paths relative to a common data root

_Context:_ `BackgroundNoiseAugmentor` and `MicrophoneNoiseAugmentor` must skip writing and return the unmodified input `AudioSample` when their augmentation isn't applied — copying unchanged audio bytes on every run would make `dvc push`/`dvc pull` needlessly expensive on a dataset that regenerates constantly, so this passthrough behavior is deliberate and needs explicit unit test coverage for each augmentor's no-op branch (e.g. asserting the writer is not called when `MicrophoneNoiseAugmentor` amplitude is zero, or when `BackgroundNoiseAugmentor`/`DelayAugmentor` draw a no-augmentation outcome), plus an E2E-level check that a fully up-to-date `dvc repro` does not rewrite or copy any already-produced audio file. A naive design — each `ModifierStage` taking a single `input_dir: Path` (the immediately preceding stage's `output_dir`) and resolving `SampleWithPath.path` as a bare filename against it — breaks under this passthrough behavior: the returned sample's bare-filename path only resolves against whichever directory actually wrote the file, which may be one or more stages upstream of whatever stage is now holding the reference. A per-stage `input_dir` is the wrong lookup root whenever a passthrough chain crosses more than one stage, and because each stage is unit-tested in isolation, this class of bug is easy to miss until stages are wired together end-to-end via `dvc repro`. That is exactly what happened in the discarded implementation attempt: the bug went undetected because DVC wiring never reached past `speech_01` before the rebuild decision was made. Root-relative paths are therefore the design from the first stage that reads audio, not a fix applied after the fact.

_Decision:_ `SampleWithPath.path` is root-relative — e.g. `speech_02_add_delays/TV_ON_Jenny_r77.wav` — instead of a bare filename. Stages that read audio (`DelayAugmentor`, `BackgroundNoiseAugmentor`, `MicrophoneNoiseAugmentor`, `SpectrogramStage`) take a single shared `data_root: Path` (the `ml/data/` directory containing every stage's persisted output directory) instead of a per-stage `input_dir: Path`, and resolve reads as `data_root / sample.path` — correct regardless of which upstream stage actually produced the file. Stages that write audio (`TtsSampleGenerator`, `DelayAugmentor`, `BackgroundNoiseAugmentor`, `MicrophoneNoiseAugmentor`) store `path` as `self._output_dir.relative_to(data_root) / f"{output_name}.{ext}"` — every file a stage actually writes carries its owning subdirectory in its path, so a passthrough sample's path still points at wherever it was truly last written. `SpectrogramStage`/`TokenStage` are unaffected beyond `SpectrogramStage` also needing `data_root` for its own audio *reads*: neither has a passthrough/skip-write branch (base `ModifierStage` subclasses always compute fresh output for anything not already cached in their own directory, per the two-case skip/regenerate algorithm above), `TokenStage` doesn't read audio files at all, and `ModelTrainer`/`ModelEvaluator` receive `spectrogram_dir`/`token_dir` explicitly rather than resolving through `data_root`.

GC is unaffected by this design: it matches on `Path(sample.path).name`, which strips any directory component, so root-relative paths don't change the `output_samples` vs. `output_dir.glob("*")` comparison.

_Consequences:_ The I/O-avoiding passthrough behavior in `BackgroundNoiseAugmentor`/`MicrophoneNoiseAugmentor` is preserved by construction. **DVC wiring implication:** any stage resolving root-relative audio paths must list `deps:` on every persisted directory that could be a passthrough source, not just its immediate predecessor — concretely, `speech_02_add_delays/` (a guaranteed terminus, since `DelayAugmentor` always writes), `speech_03_add_background_noise/`, and `speech_04_add_mic_noise/`, since either of the latter two may or may not hold a given file depending on whether that stage's augmentation applied. This is a small but real DVC-wiring complexity, bounded to two adjacent passthrough-capable stages.

This extended `deps:` list is not needed for change detection — a single-level dep on the immediate predecessor's output directory is already sufficient for that, since a passthrough stage's own `manifest.json` embeds the (possibly-changed) upstream `content_hash` for every sample it passes through unchanged, so any upstream change is visible one level down regardless. The extended list is needed for a different reason: **file availability under scoped `--pull`.** Per the "Live TTS in CI" decision, CI uses `dvc repro --pull`, which fetches only the deps *declared* for a stage that's re-running — not a blanket whole-repo pull. If `speech_06` declared only `speech_04/` as a dep, but a passthrough sample's actual bytes were last written in `speech_02/` (because both `speech_03` and `speech_04` skipped writing for that sample), `--pull` would have no reason to fetch `speech_02/`, and `speech_06` would hit a `FileNotFoundError` reading `data_root/speech_02_add_delays/...` even though DVC correctly decided to re-run the stage. The extended `deps:` list exists to keep the physical files available under `--pull`, not to fix change detection.

---

### AudioSample.transcript is the full spoken surface form, not the canonical label

_Context:_ This pipeline trains an automatic speech recognition (ASR) model only — its job is to identify what was actually said. A separate, out-of-scope downstream model maps the transcribed text to command intent. The discarded implementation attempt set `AudioSample.transcript = input_sample.label` (e.g. `"TV_ON"`) in `TtsSampleGenerator._generate_output`, with `TokenStage` tokenizing from `AudioSample.transcript` — meaning the model was wired to map audio directly to the canonical command, skipping literal transcription — while `VocabComputer` built its phoneme vocabulary from `TextSample.content` (the full surface form, including pleasantries and hesitations), since the ASR model needs to recognize every word a user could plausibly say, not just canonical command words. Those two choices are inconsistent: a vocabulary broader than what the model is ever trained to predict.

_Decision:_ `TtsSampleGenerator._generate_output` sets `AudioSample.transcript = input_sample.content` (the full spoken surface form actually synthesized by TTS), not `input_sample.label`. `TokenStage` tokenizes from `AudioSample.transcript` directly, so it correctly encodes the full spoken text, aligned with `VocabComputer`'s content-based vocabulary. `_derive_name` uses `input_sample.label` purely as a filename-readability prefix (e.g. `TV_ON_Jenny_r77`) and is unaffected; grouping generated files by canonical command in the filename stays useful for dataset review independent of what the model is trained to predict.

_Consequences:_ Getting this right on the first TDD cycle for `TtsSampleGenerator` avoids training a model against the wrong target and having to regenerate every downstream spectrogram/token manifest and retrain later.

---

### VocabComputer fails fast on out-of-vocabulary words

_Context:_ `PhonemeProvider.lookup()` can raise `PhonemeNotFoundError` for a phrase-corpus word that isn't in the CMU Pronouncing Dictionary (e.g. a brand name or a typo). The retired procedural script (`ml/scripts/intent_prediction/02_compute_vocab.py`) skipped such words with a warning and continued.

_Decision:_ `VocabComputer` does not catch `PhonemeNotFoundError` — it propagates and fails the `intent_02` stage. No silent-skip behavior is carried forward from the retired script.

_Consequences:_ A missing dictionary entry is a hard stop, not a silently incomplete vocabulary: the corpus author must fix the typo or add the missing word (e.g. to a supplemental pronunciation list) before the pipeline can proceed, rather than discovering later that a word was silently untranscribable.

---

### Incremental DVC wiring with a front-loaded E2E test

_Context:_ Wiring the full pipeline into `dvc.yaml` in one task at the very end, after every stage is implemented, with the E2E CI test as the very last task, is tempting because each stage looks simpler in isolation. The discarded implementation attempt followed that sequencing and left `dvc.yaml` wiring only the first two intent stages plus `speech_01` for most of the epic, with stages through `speech_08` implemented but never exercised end-to-end via `dvc repro`. The root-relative-path decision above is a direct symptom of that gap: a bare-filename design was invisible to isolated unit tests and would only have surfaced once the full pipeline was wired and run.

_Decision:_ Sequence tasks (finalised during task breakdown, not itemised here) so that:
1. **Every task that adds a new stage also extends `dvc.yaml`/`params.yaml` to wire that stage in**, with `dvc repro` running end-to-end through the newly-added stage as part of that task's exit criteria — not deferred to a single late "DVC wiring" task.
2. **The E2E CI test (`ml/test/e2e_pipeline_test.py`, `@pytest.mark.e2e`) is introduced early**, once enough stages exist to produce a trained model end-to-end on the 10-phrase CI fixture, and is extended/re-run as later stages are added — not written only after every stage exists.
3. **Augmentation stages are deferred to a second pass.** The first E2E-capable task set implements the core vertical slice — `intent_01`, `intent_02`, `speech_01` (TTS), `speech_05`/`speech_06` (tokens/spectrograms), `speech_07` (split), `speech_08`/`09`/`10` (train/eval) — training on clean, unaugmented audio to get the E2E test green as early as possible. `speech_02` (delay), `speech_03` (background noise), and `speech_04` (mic noise) are added in a follow-up task set once the clean pipeline is proven, with `dvc repro` re-run end-to-end through each newly-added augmentation stage per the same per-task exit criteria as (1).

_Consequences:_ Each task's exit criteria becomes "the pipeline still runs end-to-end via `dvc repro`," which catches integration issues (like the directory-boundary gap above) at the point they're introduced rather than in one large final-integration task. The front-loaded E2E test only exercises the augmentation stages' passthrough/root-relative-path logic once the second task set lands, so that class of integration bug is caught at the point augmentation is wired in, not before. `ADR-278` ("DVC wiring for intent stages") is superseded by this decision — it proposed a one-off early wire-up of just the intent stages; incremental wiring subsumes that for every stage, not just the first two, and is Cut in Jira accordingly.

---

### ml/pipeline/ package with thin DVC entry points

_Context:_ The pre-refactor `ml/scripts/` tree is retired by this epic and replaced by `ml/pipeline/`.

_Decision:_ All OOP code lives in `ml/pipeline/`. Each DVC stage is a script in `ml/pipeline/stages/` with a two-digit sort-order prefix. Planned numbering:

| Stage | Script | Class |
|---|---|---|
| `intent_00` | `download_phoneme_dictionary.py` (not OOP; a plain script) | — |
| `intent_01` | `intent_01_generate_phrases.py` | `PhraseVariator` |
| `intent_02` | `intent_02_compute_vocab.py` | `VocabComputer` |
| `speech_00` | `speech_00_download_noise_samples.py` (not OOP; a plain script) | — |
| `speech_01` | `speech_01_generate_samples.py` | `TtsSampleGenerator` |
| `speech_02` | `speech_02_add_delays.py` | `DelayAugmentor` |
| `speech_03` | `speech_03_add_background_noise.py` | `BackgroundNoiseAugmentor` |
| `speech_04` | `speech_04_add_mic_noise.py` | `MicrophoneNoiseAugmentor` |
| `speech_05` | `speech_05_compute_tokens.py` | `TokenStage` |
| `speech_06` | `speech_06_compute_spectrograms.py` | `SpectrogramStage` |
| `speech_07` | `speech_07_create_set_manifests.py` | `SetManifestSplitter` |
| `speech_08` | `speech_08_train_model.py` | `ModelTrainer` |
| `speech_09` | `speech_09_evaluate_model.py` | `ModelEvaluator.evaluate()` |
| `speech_10` | `speech_10_package_test_samples.py` | `ModelEvaluator.package_test_samples()` |

The initial `Manifest[TextSample]` is bootstrapped in `intent_01_generate_phrases.py`: it reads `phrase`/`command` columns from the input CSV, constructs `PhraseVariator`, generates variants, applies the `subsample_rate` filter, and writes the manifest.

`speech_00_download_noise_samples.py` mirrors `intent_00`'s pattern: a plain, non-OOP download script, its output (background noise WAV files consumed by `_DirectoryNoiseProvider`) is a DVC-tracked, persisted directory so noise samples aren't re-downloaded on every `dvc repro` when unchanged.

---

### Shared entry-point base class (ADR-280)

_Context:_ The six `ModifierStage`-shaped entry-point scripts (`speech_01` through `speech_06`) share nearly identical logic: parse `--input-manifest-dir`/`--output-dir` (plus stage-specific flags), load `PipelineParams`, read the input manifest, create the output directory, construct the stage, and `asyncio.run(stage.transform(...))`. Only stage construction (and occasionally an extra CLI flag, e.g. `--noise-dir`, `--vocab-dir`) is unique per stage. Writing each script by hand — as the discarded implementation attempt did — reproduces this boilerplate six times; ADR-280 was filed against that duplication.

_Decision:_ Introduce `ModifierStageEntryPoint[T_in, T_out]` (`ml/pipeline/stages/entry_point.py`), an abstract base class that owns the shared `run()` sequence. Subclasses override:
- `build_stage(args, params, store) -> ModifierStage[T_in, T_out]` (required) — construct the stage.
- `add_arguments(parser) -> None` (optional, default no-op) — register stage-specific CLI flags beyond the two shared ones.

Each of the six `ModifierStage`-shaped entry-point scripts is written as a small subclass plus a two-line `if __name__ == "__main__":` block. `intent_01`, `intent_02`, `speech_07` (`SetManifestSplitter`, not a `ModifierStage`), and `speech_08` (synchronous, multi-manifest-input) don't fit this shape and get their own `main()` — the base class targets exactly the boilerplate ADR-280 describes, not every entry point.

_Consequences:_ New `ModifierStage`-shaped stages only need to write `build_stage`. The two evaluation stages are not this shape — `ModelEvaluator` takes multiple manifest inputs like `speech_08` — so this base class has no additional subclasses planned beyond the six speech/intent stages above, but is written generically in case one arises. `ModifierStageEntryPoint.run()` is Orchestrator-tier: it wires tested components together with no branching logic of its own, verified by a simple integration test per concrete entry point rather than full TDD.

---

### Static analysis via mypy --strict (ADR-281)

_Context:_ Type hints are used throughout the pipeline design (`from __future__ import annotations`, `Generic`, `Protocol`, dataclasses) but nothing enforces them; without static checking, type errors surface only at runtime or in code review.

_Decision:_ Add `mypy --strict` over `ml/pipeline/` and `ml/test/`, added as a new `mypy` dependency in `ml/requirements.txt`, configured via `[tool.mypy]` in `ml/pyproject.toml`. A new quality-gate script pair (`scripts/validate-ml-build.sh` / `.cmd`, matching the existing `scripts/validate-build.sh` naming convention at the repo root) runs `mypy --strict ml/pipeline ml/test` from `ml/`. `CLAUDE.md`'s Quality Gates table is updated to list it alongside `validate-build`/`validate-tests`.

_Consequences:_ Deferred imports of `librosa`/`edge_tts`/`tensorflow` inside method bodies, in particular, will need `# type: ignore[import-untyped]` or stub packages where available to pass `--strict` cleanly. Wiring mypy in early — alongside the first stages, not bolted on at the end — means every class is written strict-clean from the start rather than needing a later cleanup pass across the whole tree.

---

### Precomputed test samples in CI with a small fixed phrase set

_Context:_ An E2E CI test must exercise the full pipeline without requiring a large dataset or long training time.

Test coverage must span two distinct layers of change detection, not just one:

- **DVC-level stage skip** — a completely up-to-date `dvc repro` (no deps changed) must not invoke any stage script at all; DVC's own dependency-hash comparison skips the stage before any Python code runs. The E2E test asserts this directly on a second `dvc repro` with no param changes (e.g. no stage subprocess executes / DVC reports every stage as skipped).
- **`ModifierStage`/`RandomizedModifierStage`-level per-sample skip** — DVC can only skip or run a whole stage (per the content-hash-determines-identity decision above); within a stage it does run, each level's algorithm must be covered for all of its cases: a completely fresh run (no previous output manifest), a fully up-to-date run (every sample skips), and — for `RandomizedModifierStage` subclasses specifically — a partial-update run (a parameter change causes some samples to regenerate with their stored seed while others are reused unchanged).

_Decision:_ CI uses `dvc repro --pull --set-param` with a small, fixed 10-phrase CI fixture and cheap param overrides:

```bash
dvc repro --pull \
  --set-param pipeline.input_phrases_path=test/fixtures/ci_phrases.csv \
  --set-param pipeline.epochs=1 \
  --set-param pipeline.subsample_rate=100
```

`speech_01`'s output for this exact fixture/param combination is generated once (live `EdgeTtsProvider` call) and pushed to the S3 remote. Because DVC caches stage outputs by content hash of their deps/params, every subsequent CI run with the same fixed inputs resolves to that same hash and `--pull` fetches the cached audio from the S3 remote instead of re-invoking `EdgeTtsProvider` — CI is normally network-free with respect to edge_tts, and exercises DVC's pull/restore path instead, which the pipeline needs real coverage of anyway. This requires CI to have S3 remote read credentials configured.

If a PR changes anything upstream of `speech_01` (phrase content in `PhraseVariator`, the CI fixture CSV, or any TTS param), the stage's content hash changes and `EdgeTtsProvider` runs live for that CI run instead — an expected, low-frequency fallback, not a failure. `TtsProvider` remains the seam for a full mock if this fallback ever proves noisy. Per the incremental-wiring decision above, this test is introduced as soon as a trained model can be produced end-to-end, not deferred to the last task.

No dedicated *live-network* `EdgeTtsProvider` test is added — live-TTS coverage is a rare, incidental side effect of the fallback path above, not a standing test surface. This doesn't exempt `EdgeTtsProvider` from unit testing generally: its retry/backoff logic is real, mockable behavior and stays Testable-tier, covered with a fake transport that simulates retryable failures.

---

### Injectable PhonemeDecoder for phoneme-to-word reconstruction

_Context:_ `ModelEvaluator`'s `wer` metric is a word-level score, but `TokenStage` trains the model on flat phoneme sequences with no word-boundary marker — the model's raw output is phonemes, not words. Reconstructing words from a phoneme sequence is a real decoding problem with more than one viable strategy (e.g. different beam-search formulations over the lexicon), and those strategies need to be compared against each other rather than settled once and hardcoded into `ModelEvaluator`.

_Decision:_ `PhonemeDecoder` (`ml/pipeline/speech/phoneme_decoder.py`) is a `Protocol` seam: `decode(phonemes: list[str]) -> list[str]`, reconstructing a word sequence from a phoneme sequence. `ModelEvaluator` takes a `PhonemeDecoder` via constructor injection rather than hardcoding a decode strategy, so different decoders can be swapped in and evaluated side by side without changing `ModelEvaluator` itself. The initial concrete implementation is `BeamSearchPhonemeDecoder`, a lexicon-constrained beam search over `VocabResult.words_to_phonemes`.

_Consequences:_ `per` (phoneme error rate) stays decoder-independent — computed directly from the model's raw phoneme output, unaffected by which `PhonemeDecoder` is injected. `wer` depends on the injected decoder, which is exactly why `per` and `wer` are kept as separate top-level scalars in `metrics.json` rather than one combined metric (per the evaluation-metrics decision above). How decoder-comparison runs are tracked (e.g. as separate DVC experiments) is left to task breakdown, not prescribed here.

## Component Breakdown

Classification uses the Wrapper / Testable / Orchestrator taxonomy (`dev-team:component-taxonomy`).

| Component | Type | Responsibility | Depends on |
|---|---|---|---|
| `Sample`/`SampleWithPath`/`TextSample`/`AudioSample`/`SampleSpectrogram`/`SampleTokens` | Wrapper | Plain dataclasses; `TextSample.__post_init__` sets `name = content_hash` | — |
| `Manifest[S]` | Testable | Typed collection with `by_name`/`by_content_hash` lookup; raises on duplicate names | `Sample` |
| `ManifestStore` | Testable | JSON round-trip (schema v1); type-dispatch serialise/deserialise; raises on empty/mixed-type writes | `Manifest`, `Sample` types |
| `PassFilter`/`MinMaxFilter`/`NormalFilter` | Testable | Density/domain math; precision quantization grid setup | — |
| `VariationGenerator` | Testable | Deterministic hash-based rejection sampling; highest-risk logic in the pipeline | `PassFilter` |
| `ModifierStage[T_in,T_out]` | Testable | Two-case skip/regenerate algorithm; GC; manifest read/write | `Manifest`, `ManifestStore` |
| `RandomizedModifierStage[T_in,T_out]` | Testable | Adds seed storage + three-case skip/regen-with-stored-seed/new algorithm | `ModifierStage`, `VariationGenerator` |
| `PhraseVariator` | Testable | Surface-form variation + sanity-check logic | `VariationGenerator`, `GeneratePhraseParams` |
| `PhonemeProvider` (protocol) | — | Injectable phoneme-lookup seam | — |
| `CmuDictPhonemeProvider` | Testable | Loads the CMU Pronouncing Dictionary from `download_phoneme_dictionary.py`'s output; raises `PhonemeNotFoundError` for out-of-vocabulary words | CMU Pronouncing Dictionary file |
| `VocabComputer` | Testable | Extracts phoneme vocabulary from surface-form (`content`) words; fails the stage on `PhonemeNotFoundError` (no silent skipping) | `PhonemeProvider` (injected) |
| `VocabResult` | Wrapper | Plain dataclass: `phoneme_list`, `words_to_phonemes` | — |
| `AudioReader`/`AudioWriter` (protocols) | — | Injectable seams | — |
| `LibrosaAudioReader` | Wrapper | Thin call-through to `librosa.load`, offloaded to a thread pool | librosa |
| `SoundfileAudioWriter` | Wrapper | Thin call-through to `soundfile.write`, offloaded to a thread pool | soundfile |
| `TtsProvider` (protocol) | — | Injectable TTS-synthesis seam | — |
| `TtsSampleGenerator` | Testable | Applied-values/name-derivation logic for TTS; sets `transcript` from `content` (full surface form, not the canonical label); writes root-relative paths | `TtsProvider` (injected) |
| `EdgeTtsProvider` | Testable | Retry/backoff around live edge_tts synthesis — the retry/backoff logic itself is unit-tested with a mocked transport; only a *dedicated live-network test* is excluded (see the CI decision below) | edge_tts |
| `DelayAugmentor` | Testable | Prefix/suffix silence augmentation; reads/writes via `data_root`-relative paths | `AudioReader`/`AudioWriter` |
| `NoiseProvider` (protocol) | — | Injectable noise-file seam | — |
| `_DirectoryNoiseProvider` | Testable | Loads/resamples WAV noise files from a directory at construction | librosa |
| `BackgroundNoiseAugmentor` | Testable | Noise-mix augmentation; skips writing and returns the unmodified input sample when unapplied; reads/writes via `data_root`-relative paths | `NoiseProvider`, `AudioReader`/`AudioWriter` |
| `MicrophoneNoiseAugmentor` | Testable | Gaussian noise augmentation; skips writing and returns the unmodified input sample when unapplied; reads/writes via `data_root`-relative paths | `AudioReader`/`AudioWriter` |
| `SpectrogramStage` | Testable | Deterministic log-mel spectrogram extraction; reads via `data_root`-relative paths (no passthrough of its own) | `AudioReader`, librosa |
| `TokenStage` | Testable | Deterministic transcript→phoneme-token conversion | `VocabResult` |
| `SetManifestSplitter` | Testable | Shuffle + percentage split of the fully-augmented manifest | `ManifestStore` |
| `lookup_sample_triplets` | Testable | Pure function: joins split/spectrogram/token manifests by `parent_name` | `Manifest` |
| `MachineLearningModel`/`MachineLearningModelBuilder` (protocols) | — | Injectable TF seam | — |
| `TensorflowModel`/`TensorflowModelBuilder` | Wrapper | Thin call-through to Keras `fit`/`predict`/`save`/`load`; excluded from unit tests by design (module docstring) | TensorFlow |
| `ModelTrainer` | Testable | Filters/joins manifests, builds `tf.data.Dataset`, drives training | `MachineLearningModelBuilder`, `lookup_sample_triplets` |
| `PhonemeDecoder` (protocol) | — | Injectable phoneme-to-word decoding seam | — |
| `BeamSearchPhonemeDecoder` | Testable | Lexicon-constrained beam search over `VocabResult.words_to_phonemes` | `VocabResult` |
| `ModelEvaluator` (ADR-230) | Testable | Shared `_run_predictions`; `evaluate()` writes DVC-experiment-comparable metrics (`wer`, `per`, S/I/D counts) plus a per-command WER breakdown; `package_test_samples()` zips correct predictions | `MachineLearningModelBuilder`, `lookup_sample_triplets`, `PhonemeDecoder` (injected) |
| `conventions.py` functions | Wrapper | Pure path-formatting, no branching | — |
| `PipelineParams` + per-stage params dataclasses | Wrapper | Straightforward YAML→dataclass deserialisation | PyYAML |
| `ModifierStageEntryPoint` (ADR-280) | Orchestrator | Shared CLI/params/manifest/output-dir/transform wiring for `ModifierStage`-shaped entry points | `ModifierStage`, `RandomizedModifierStage`, `PipelineParams`, `ManifestStore`, `conventions` |
| Individual `stages/*.py` entry-point scripts | Orchestrator | Composition roots; wire concrete dependencies into each stage | All of the above, per stage |

No generic `FileSystem`/`NetworkClient` abstraction is introduced on top of the per-concern seams above. Several libraries in this pipeline don't expose a `fileSystem.write(path, bytes)`-shaped call to intercept (Keras' `model.save()` writes a directory tree internally; `json.dump` needs a real file handle), so a generic fake-filesystem layer would either reimplement chunks of their serialization or leak real disk I/O through the "abstraction" anyway. Instead: where I/O is mixed with business logic worth unit-testing, a per-concern `Protocol` seam is introduced as needed (`AudioReader`/`AudioWriter`, `TtsProvider`, `NoiseProvider` above are exactly this); where I/O is simple JSON round-tripping with no library friction (`ManifestStore`), pytest's `tmp_path` fixture (real, throwaway files in an isolated temp dir) is the standard alternative to mocking the file system; where a library's save/load API offers no clean seam at all (`TensorflowModel.save`/`.load`, `download_phoneme_dictionary.py`'s network call), it stays Wrapper-tier and excluded from unit tests by design, same as `TensorflowModel`/`TensorflowModelBuilder`'s existing exclusion.

## Planned Implementation

### Directory Layout

```
ml/
  pipeline/
    __init__.py
    core/
      sample.py            # Sample, SampleWithPath, TextSample, AudioSample,
                            # SampleSpectrogram, SampleTokens
      manifest.py           # Manifest[S], ManifestStore
      modifier_stage.py     # ModifierStage[T_in, T_out], RandomizedModifierStage[T_in, T_out]
      randomization.py      # PassFilter, MinMaxFilter, NormalFilter,
                            # VariationGenerator
    io/
      audio_io.py           # AudioData, AudioReader, AudioWriter,
                            # LibrosaAudioReader, SoundfileAudioWriter
      _doc_io.md
    intent/
      phrase_variator.py    # PhraseVariator
      vocab_computer.py     # VocabComputer, VocabResult, PhonemeProvider, CmuDictPhonemeProvider
    speech/
      tts_stage.py               # TtsSampleGenerator, TtsProvider, EdgeTtsProvider
      delay_stage.py              # DelayAugmentor
      background_noise_stage.py   # BackgroundNoiseAugmentor, NoiseProvider
      mic_noise_stage.py           # MicrophoneNoiseAugmentor
      spectrogram_stage.py         # SpectrogramStage
      token_stage.py                # TokenStage
      set_splitter.py                # SetManifestSplitter
      manifest_filter.py              # lookup_sample_triplets
      ml_model.py                      # MachineLearningModel, MachineLearningModelBuilder
      tensorflow_backend.py             # TensorflowModel, TensorflowModelBuilder
      model_trainer.py                   # ModelTrainer
      phoneme_decoder.py                  # PhonemeDecoder, BeamSearchPhonemeDecoder
      model_evaluator.py                   # ModelEvaluator (ADR-230)
      _doc_speech.md
    stages/
      conventions.py                      # path helpers
      params.py                            # PipelineParams + sub-dataclasses
      entry_point.py                        # ModifierStageEntryPoint (ADR-280)
      intent_01_generate_phrases.py
      intent_02_compute_vocab.py
      speech_00_download_noise_samples.py
      speech_01_generate_samples.py
      speech_02_add_delays.py
      speech_03_add_background_noise.py
      speech_04_add_mic_noise.py
      speech_05_compute_tokens.py
      speech_06_compute_spectrograms.py
      speech_07_create_set_manifests.py
      speech_08_train_model.py
      speech_09_evaluate_model.py          # val set: writes metrics.json
      speech_10_package_test_samples.py    # test set: writes test_samples.zip
    download_phoneme_dictionary.py
  test/
    pipeline/
      core/
      intent/
      io/
      speech/
      stages/
    e2e_pipeline_test.py    # pytest; invokes dvc repro via subprocess
    fixtures/
      ci_phrases.csv        # 10 canonical phrases
  dvc.yaml                  # wired incrementally, stage by stage, per the incremental-wiring decision
  dvc.lock
  params.yaml               # extended incrementally alongside dvc.yaml
  pyproject.toml            # pytest config; [tool.mypy] (ADR-281)
  requirements.txt          # mypy dependency (ADR-281)
```

### Interfaces

Full interface definitions land in each subpackage's `_doc_*.md` as it is implemented, per the project's documentation conventions. This section elaborates on interfaces central to the design decisions above: the shared entry-point base class and the evaluator.

#### ModifierStageEntryPoint (ADR-280)

```python
class ModifierStageEntryPoint(ABC, Generic[T_in, T_out]):
    description: str  # ArgumentParser description; set by subclass

    def add_arguments(self, parser: argparse.ArgumentParser) -> None:
        """Override to register stage-specific CLI flags (e.g. --noise-dir, --vocab-dir)."""
        return

    @abstractmethod
    def build_stage(
        self, args: argparse.Namespace, params: PipelineParams, store: ManifestStore,
    ) -> ModifierStage[T_in, T_out]:
        """Construct the stage from parsed args and loaded params."""
        ...

    def run(self) -> None:
        """Shared sequence: parse --input-manifest-dir/--output-dir (+ add_arguments()),
        load PipelineParams, read the input manifest, mkdir the output dir,
        build_stage(), then asyncio.run(stage.transform(...))."""
        ...
```

Example usage (`speech_02_add_delays.py`):

```python
class _AddDelaysEntryPoint(ModifierStageEntryPoint[AudioSample, AudioSample]):
    description = "Add silence delays to WAV audio samples"

    def build_stage(self, args, params, store):
        return DelayAugmentor(
            output_dir=args.output_dir,
            manifest_store=store,
            audio_reader=LibrosaAudioReader(),
            audio_writer=SoundfileAudioWriter(),
            input_dir=args.input_manifest_dir,
            params=params.add_delays,
        )

if __name__ == "__main__":
    _AddDelaysEntryPoint().run()
```

#### ModelEvaluator (ADR-230)

```python
class ModelEvaluator:
    def __init__(self, backend: MachineLearningModelBuilder, decoder: PhonemeDecoder) -> None: ...

    def evaluate(
        self,
        manifest: Manifest[AudioSample],       # val split
        model_path: Path,
        vocab: VocabResult,
        spectrogram_manifest: Manifest[SampleSpectrogram],  # full, all splits
        token_manifest: Manifest[SampleTokens],              # full, all splits
        spectrogram_dir: Path,
        token_dir: Path,
        output_dir: Path,
    ) -> EvaluationResult:
        """Same lookup_sample_triplets() join as ModelTrainer.train(). Model's raw phoneme
        output is reconstructed into words via self._decoder (injected PhonemeDecoder)
        before computing wer; per is computed directly from the raw phoneme output,
        independent of the decoder. Writes:
          evaluation_predictions.txt   — tab-separated '{reference}\\t{hypothesis}' word lines
          metrics.json                — {"wer": <float>, "per": <float>,
                                          "substitutions": <int>, "insertions": <int>,
                                          "deletions": <int>} — flat scalars only,
                                          for DVC experiment comparison (dvc metrics/exp show)
          evaluation_wer_by_command.json — {<command label>: <float wer>, ...}, grouped by
                                          input_sample.label (not a transcript→command mapping,
                                          which is out of scope per the AudioSample.transcript
                                          decision above); kept out of metrics.json so DVC's
                                          cross-experiment view stays a small fixed set of scalars
        Implemented via a shared private _run_predictions() also used by
        package_test_samples(), matching ModelTrainer's structure."""
        ...

    def package_test_samples(
        self,
        manifest: Manifest[AudioSample],       # test split
        model_path: Path,
        vocab: VocabResult,
        spectrogram_manifest: Manifest[SampleSpectrogram],
        token_manifest: Manifest[SampleTokens],
        spectrogram_dir: Path,
        token_dir: Path,
        audio_dir: Path,     # source of WAV files for the zip
        output_dir: Path,
    ) -> Path:
        """Runs the same prediction loop via _run_predictions(), then zips audio files
        for samples where hypothesis == reference into test_samples.zip — known-good
        fixtures for app E2E tests. Returns conventions.test_samples_path(output_dir)."""
        ...

@dataclass
class EvaluationResult:
    wer: float
    per: float                  # phoneme error rate — raw model output quality,
                                 # independent of any phoneme→word reconstruction
    substitutions: int
    insertions: int
    deletions: int
    wer_by_command: dict[str, float]  # keyed by input_sample.label
    predictions: list[tuple[str, str]]
```

`conventions.py` needs `evaluation_predictions_path`, `evaluation_metrics_path`, `evaluation_wer_by_command_path`, and `test_samples_path` helpers.

#### Root-relative path helpers

`conventions.py` needs a way to express a sample's stored path relative to `data_root` (e.g. `output_dir.relative_to(data_root) / f"{name}.{ext}"`), distinct from the absolute path used for actual file I/O (`sample_file_path(output_dir, name, ext)`). Exact helper shape is left to the task that implements the first audio-writing stage — not prescribed further here.

### Data Flow

```
01_input_phrases.csv (phrase, command columns)
        │
        │ PhraseVariator → TextSample variants (name = content_hash; sanity-checked)
        │ subsample_rate filter applied
        ▼
Manifest[TextSample]                                              intent_01
        │
        ├──► VocabComputer → VocabResult                          intent_02
        │      (vocabulary from TextSample.content; phoneme_list.txt + words_to_phonemes.json)
        │
        ▼
TtsSampleGenerator(RandomizedModifierStage[TextSample, AudioSample])        speech_01
  Manifest[AudioSample] (voice, speech_rate; transcript=content)
        │
        ▼
DelayAugmentor(RandomizedModifierStage[AudioSample, AudioSample])           speech_02
  Manifest[AudioSample] (prefix_delay_s, suffix_delay_s)
        │
        ▼
BackgroundNoiseAugmentor(RandomizedModifierStage[AudioSample, AudioSample]) speech_03
  Manifest[AudioSample] (noise_file, noise_start_s≡0.0, noise_volume)
        │
        ▼
MicrophoneNoiseAugmentor(RandomizedModifierStage[AudioSample, AudioSample]) speech_04
  Manifest[AudioSample] (mic_noise_amplitude; fully augmented)
        │
        ├──► SpectrogramStage (ModifierStage)                     speech_06
        │      Manifest[SampleSpectrogram]
        │
        ├──► TokenStage (ModifierStage) ◄── VocabResult            speech_05
        │      Manifest[SampleTokens]
        │
        └──► SetManifestSplitter                                  speech_07
               train_manifest.json / val_manifest.json / test_manifest.json

All three (spectrogram, tokens, split) are DVC-parallel outputs of the augmented manifest
        │
        ▼
ModelTrainer ◄── train_manifest + spectrogram/token manifests + VocabResult   speech_08
  speech_to_text_model.keras
        │
        ├──► ModelEvaluator.evaluate() ◄── val_manifest + spectrogram/token manifests + VocabResult + model   speech_09
        │      evaluation_predictions.txt, metrics.json
        │
        └──► ModelEvaluator.package_test_samples() ◄── test_manifest + same inputs + model                    speech_10
               test_samples.zip
```

Per the incremental-wiring decision, each stage is wired into `dvc.yaml` as it is implemented, with `dvc repro` run end-to-end through that stage as part of the same task — not batched into one final wiring task.

## Open Questions

None outstanding — task sequencing for incremental DVC wiring + front-loaded E2E test is resolved in `## Tasks` below.

## Related Docs

- [`src/_doc_Projects.md`](../src/_doc_Projects.md) — project boundaries

Per-subpackage `_doc_*.md` files (`ml/pipeline/core/`, `ml/pipeline/io/`, `ml/pipeline/intent/`,
`ml/pipeline/stages/`, `ml/pipeline/speech/`) are created as each subpackage's implementation
is completed, per the header note above.

## Tasks

Ordered per the incremental-DVC-wiring decision: core primitives first, then the clean-audio vertical slice (TTS → tokens/spectrograms → split → train/eval, with the front-loaded E2E test introduced there), then augmentation stages as a second pass. Every task that adds a DVC stage extends `dvc.yaml`/`params.yaml` and runs `dvc repro` end-to-end through that stage as part of its own exit criteria — not deferred to a later task. Deleting the discarded `ml/pipeline/` implementation attempt is handled directly by branch setup before implementation starts, not tracked as its own task here. Per-task `mypy --strict` cleanliness isn't called out as a separate exit criterion on every task below — Task 1 wires it into `scripts/validate.sh`, so it's already a blanket quality gate on every change (per `CLAUDE.md`), not something to repeat 21 times.

### [ADR-338: Wire mypy --strict static analysis (ADR-281)](https://jodasoft.atlassian.net/browse/ADR-338) 🤖

**Depends on:** — none —

Adds strict static typing enforcement before any pipeline code is written, so every subsequent task is written strict-clean from the start rather than needing a later cleanup pass.

- [ ] `mypy` added to `ml/requirements.txt`
- [ ] `[tool.mypy]` strict configuration added to `ml/pyproject.toml`, scoped to `ml/pipeline/` and `ml/test/`
- [ ] `scripts/validate-ml-build.sh`/`.cmd` added, running `mypy --strict ml/pipeline ml/test` from `ml/`, matching the `scripts/validate-build.sh` naming convention
- [ ] `scripts/validate.sh`/`.cmd` updated to run `validate-ml-build` after `validate-build`, so it's part of the standard pipeline check like every other quality gate
- [ ] `CLAUDE.md`'s Quality Gates table updated to list the new script alongside `validate-build`/`validate-tests`
- [ ] Script passes (trivially, with an empty `ml/pipeline/` tree)

### [ADR-339: Core Sample/Manifest/ManifestStore data model](https://jodasoft.atlassian.net/browse/ADR-339) 🤖

**Depends on:** ADR-338

Builds the unified sample/manifest data model that every stage in the pipeline serialises through.

- [ ] `Sample`, `SampleWithPath`, `TextSample`, `AudioSample`, `SampleSpectrogram`, `SampleTokens` dataclasses in `ml/pipeline/core/sample.py`; `TextSample.__post_init__` sets `name = content_hash`
- [ ] `Manifest[S]` in `ml/pipeline/core/manifest.py`: `by_name`/`by_content_hash` lookups; raises `ValueError` on duplicate names
- [ ] `ManifestStore`: JSON round-trip (schema v1), type-dispatch serialise/deserialise; raises `ValueError` on empty manifest or mixed sample types within one manifest
- [ ] Unit tests written test-first for all of the above, landing in `ml/test/pipeline/core/`

### [ADR-340: PassFilter/VariationGenerator randomization primitives](https://jodasoft.atlassian.net/browse/ADR-340) 🤖

**Depends on:** ADR-338

Builds the seed-based, precision-quantized randomisation primitives every `RandomizedModifierStage` subclass will use.

- [ ] `PassFilter`/`MinMaxFilter`/`NormalFilter` in `ml/pipeline/core/randomization.py`, each taking `precision: int = 0` and computing the quantization grid (`scale`, biased integer bounds, `_pow2_range`) at construction
- [ ] `VariationGenerator`: `should_vary`, `generate` (quantized rejection sampling, raises `ValueError` after 1000 attempts), `generate_int`, `choose` — all seed/name-hash-derived per the spec's formulas
- [ ] Unit test explicitly covering the independence property: reordering/adding variables in a hypothetical `_get_applied_values` doesn't change other variables' derived values
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/core/`

### [ADR-341: ModifierStage/RandomizedModifierStage base classes](https://jodasoft.atlassian.net/browse/ADR-341) 🤖

**Depends on:** ADR-339, ADR-340

Builds the shared skip/regenerate/GC algorithm and its seed-aware extension — the highest-risk logic in the pipeline.

- [ ] `ModifierStage[T_in, T_out]` (`ml/pipeline/core/modifier_stage.py`): seedless base, two-case skip/regenerate algorithm keyed on `parent_content_hash`, GC scoped to `self._output_dir.glob("*")` only
- [ ] `RandomizedModifierStage[T_in, T_out](ModifierStage[T_in, T_out])`: seed storage, three-case skip/regen-with-stored-seed/new-sample algorithm
- [ ] `_compute_content_hash` implemented once per level (no seed term on the base; `+ str(seed)` term added on the randomized subclass) — every future `_generate_output` override calls the one from its own base class
- [ ] Regression test asserting GC never deletes/touches a file outside `self._output_dir`, using a passthrough sample whose `path` points at a different (ancestor) directory as the fixture
- [ ] Unit tests covering all cases at both levels (fresh run, fully up-to-date run, and — for `RandomizedModifierStage` — partial-update run) written test-first, landing in `ml/test/pipeline/core/`

### [ADR-342: AudioReader/AudioWriter I/O seam](https://jodasoft.atlassian.net/browse/ADR-342) 🤖

**Depends on:** ADR-338

Builds the injectable audio I/O seam used by every stage that reads or writes WAV files.

- [ ] `AudioReader`/`AudioWriter` protocols in `ml/pipeline/io/audio_io.py`
- [ ] `LibrosaAudioReader` (thin call-through to `librosa.load`, offloaded to a thread pool) and `SoundfileAudioWriter` (thin call-through to `soundfile.write`, offloaded to a thread pool)
- [ ] Unit tests for the protocol contract written test-first, landing in `ml/test/pipeline/io/`

### [ADR-343: PhraseVariator + intent_01 stage](https://jodasoft.atlassian.net/browse/ADR-343) 🤖

**Depends on:** ADR-339, ADR-340

Bootstraps the pipeline's first `Manifest[TextSample]` from the input phrase CSV.

- [ ] `PhraseVariator` (`ml/pipeline/intent/phrase_variator.py`): surface-form variation + sanity-check logic, using `VariationGenerator`/`GeneratePhraseParams`
- [ ] `intent_01_generate_phrases.py`: reads `phrase`/`command` columns from the input CSV, constructs `PhraseVariator`, generates variants, applies the `subsample_rate` filter, writes `Manifest[TextSample]`
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/intent/`
- [ ] `dvc.yaml`/`params.yaml` wired for `intent_01`; `dvc repro` runs end-to-end through `intent_01`

### [ADR-344: PhonemeProvider/CmuDictPhonemeProvider/VocabComputer + intent_00/intent_02 stages](https://jodasoft.atlassian.net/browse/ADR-344) 🤖

**Depends on:** ADR-339, ADR-343

Builds the phoneme vocabulary computation, including the CMU dictionary download as its own cached DVC stage and fail-fast out-of-vocabulary handling.

- [ ] `download_phoneme_dictionary.py` (plain script, not OOP) recreated in `ml/pipeline/`
- [ ] `intent_00` wired as its own `dvc.yaml` stage with a persisted, DVC-tracked output — the dictionary file is not re-downloaded on a `dvc repro` where it's already present and unchanged
- [ ] `PhonemeProvider` protocol and `CmuDictPhonemeProvider` (loads the downloaded CMU dictionary; raises `PhonemeNotFoundError` for OOV words) in `ml/pipeline/intent/vocab_computer.py`
- [ ] `VocabComputer`: extracts phoneme vocabulary from `TextSample.content` (full surface form, not `label`); does not catch `PhonemeNotFoundError` — propagates and fails the stage
- [ ] `VocabResult` dataclass (`phoneme_list`, `words_to_phonemes`)
- [ ] `intent_02_compute_vocab.py` writes `phoneme_list.txt`/`words_to_phonemes.json`
- [ ] Unit tests written test-first, including a case asserting `PhonemeNotFoundError` propagates uncaught, landing in `ml/test/pipeline/intent/`
- [ ] `dvc.yaml`/`params.yaml` wired for `intent_02` (deps on `intent_00`'s output); `dvc repro` runs end-to-end through `intent_02`

### [ADR-345: ModifierStageEntryPoint base class (ADR-280) + conventions.py/PipelineParams](https://jodasoft.atlassian.net/browse/ADR-345) 🤖

**Depends on:** ADR-341

Builds the shared entry-point scaffolding every `ModifierStage`-shaped stage script will subclass, plus the path-formatting and params-loading helpers they depend on.

- [ ] `conventions.py`: path-formatting helpers, including root-relative-path helpers (`output_dir.relative_to(data_root) / f"{name}.{ext}"` vs. the absolute `sample_file_path`) and `split_manifest_path`
- [ ] `PipelineParams` + per-stage params dataclasses (`ml/pipeline/stages/params.py`), YAML→dataclass deserialisation
- [ ] `ModifierStageEntryPoint[T_in, T_out]` (`ml/pipeline/stages/entry_point.py`): shared `run()` sequence (parse `--input-manifest-dir`/`--output-dir` + `add_arguments()`, load `PipelineParams`, read input manifest, mkdir output dir, `build_stage()`, `asyncio.run(stage.transform(...))`); `build_stage()` abstract, `add_arguments()` default no-op
- [ ] Integration test per the class itself (Orchestrator-tier — wiring only, not full TDD)

### [ADR-346: TtsProvider/EdgeTtsProvider/TtsSampleGenerator + speech_01 stage](https://jodasoft.atlassian.net/browse/ADR-346) 🤖

**Depends on:** ADR-342, ADR-343, ADR-345

Builds the first speech stage: synthesizes audio for each `TextSample`, on the clean (unaugmented) vertical slice.

- [ ] `TtsProvider` protocol and `EdgeTtsProvider` (`ml/pipeline/speech/tts_stage.py`): retry/backoff around live `edge_tts` synthesis, unit-tested with a mocked transport simulating retryable failures
- [ ] `TtsSampleGenerator(RandomizedModifierStage[TextSample, AudioSample])`: applied-values/name-derivation logic; sets `AudioSample.transcript = input_sample.content` (not `label`); writes root-relative paths under its own output directory
- [ ] `speech_01_generate_samples.py` entry point subclassing `ModifierStageEntryPoint`
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_01`; `dvc repro` runs end-to-end through `speech_01` (live `EdgeTtsProvider` call, no CI fixture caching yet) and produces speech sample files

### [ADR-347: TokenStage + speech_05 stage](https://jodasoft.atlassian.net/browse/ADR-347) 🤖

**Depends on:** ADR-344, ADR-345, ADR-346

Deterministic transcript→phoneme-token featurisation, directly downstream of TTS output on the clean vertical slice.

- [ ] `TokenStage(ModifierStage[AudioSample, SampleTokens])` (`ml/pipeline/speech/token_stage.py`): deterministic transcript→phoneme-token conversion from `AudioSample.transcript`, using `VocabResult`
- [ ] `speech_05_compute_tokens.py` entry point
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_05`, deps on `speech_01/` (its only possible source on the clean slice) and the `intent_02` vocab output; `dvc repro` runs end-to-end through `speech_05` and produces token files

### [ADR-348: SpectrogramStage + speech_06 stage](https://jodasoft.atlassian.net/browse/ADR-348) 🤖

**Depends on:** ADR-342, ADR-345, ADR-346

Deterministic log-mel spectrogram featurisation, directly downstream of TTS output on the clean vertical slice.

- [ ] `SpectrogramStage(ModifierStage[AudioSample, SampleSpectrogram])` (`ml/pipeline/speech/spectrogram_stage.py`): deterministic log-mel extraction, reads audio via `data_root`-relative paths
- [ ] `speech_06_compute_spectrograms.py` entry point
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_06`, deps on `speech_01/` (its only possible source on the clean slice); `dvc repro` runs end-to-end through `speech_06` and produces spectrogram files

### [ADR-349: SetManifestSplitter + lookup_sample_triplets + speech_07 stage](https://jodasoft.atlassian.net/browse/ADR-349) 🤖

**Depends on:** ADR-345, ADR-346

Splits the (currently clean, unaugmented) manifest into train/val/test sets and provides the join used by training/evaluation.

- [ ] `SetManifestSplitter` (`ml/pipeline/speech/set_splitter.py`): shuffle + percentage split, own `main()` (not `ModifierStage`-shaped)
- [ ] `lookup_sample_triplets` (`ml/pipeline/speech/manifest_filter.py`): pure function joining split/spectrogram/token manifests by `parent_name`
- [ ] `speech_07_create_set_manifests.py` writes `train_manifest.json`/`val_manifest.json`/`test_manifest.json`
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_07`; `dvc repro` runs end-to-end through `speech_07`

### [ADR-350: MachineLearningModel/TensorflowModel wrapper](https://jodasoft.atlassian.net/browse/ADR-350) 🤖

**Depends on:** ADR-338

Thin, deliberately-untested Keras call-through seam used by training and evaluation. Deferred `tensorflow` imports need `# type: ignore[import-untyped]` to pass `--strict` cleanly.

- [ ] `MachineLearningModel`/`MachineLearningModelBuilder` protocols (`ml/pipeline/speech/ml_model.py`)
- [ ] `TensorflowModel`/`TensorflowModelBuilder` (`ml/pipeline/speech/tensorflow_backend.py`): thin call-through to Keras `fit`/`predict`/`save`/`load`; module docstring excludes it from unit tests by design

### [ADR-351: ModelTrainer + speech_08 stage + front-loaded E2E test](https://jodasoft.atlassian.net/browse/ADR-351) 🤖

**Depends on:** ADR-347, ADR-348, ADR-349, ADR-350

Completes the clean-audio vertical slice with a trainable model, and introduces the E2E CI test now that a trained model can be produced end-to-end.

- [ ] `ModelTrainer` (`ml/pipeline/speech/model_trainer.py`): filters/joins manifests via `lookup_sample_triplets`, builds `tf.data.Dataset`, drives training
- [ ] `speech_08_train_model.py`: synchronous, multi-manifest-input entry point with its own `main()` (not `ModifierStage`-shaped)
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_08`; `dvc repro` runs end-to-end through `speech_08`, producing `speech_to_text_model.keras`
- [ ] `ml/test/fixtures/ci_phrases.csv` (10 canonical phrases) added
- [ ] `ml/test/e2e_pipeline_test.py` (`@pytest.mark.e2e`) added, using `dvc repro --set-param` overrides (`input_phrases_path`, `epochs=1`, `subsample_rate=100`) — live `EdgeTtsProvider` calls at this point, CI fixture caching added in Task 15

```gherkin
Feature: End-to-end clean pipeline reproduction

  Scenario: Fresh run produces a trained model
    Given no prior pipeline outputs exist
    When "dvc repro" is run with the CI phrase fixture and epochs=1
    Then intent_01 through speech_08 all execute
    And a speech_to_text_model.keras file is produced

  Scenario: Fully up-to-date run skips every stage
    Given a completed "dvc repro" run with no param changes since
    When "dvc repro" is run again with the same params
    Then DVC reports every stage as skipped
    And no stage script is invoked
```

### [ADR-352: Configure CI S3 credentials + precompute CI fixture audio](https://jodasoft.atlassian.net/browse/ADR-352) 🧑

**Depends on:** ADR-346, ADR-351

Infrastructure/access setup that can't be done by an agent: CI needs read credentials for the DVC S3 remote, and the CI fixture's `speech_01` output needs to be generated once and pushed so CI stops needing live `EdgeTtsProvider` calls.

- [ ] CI runner configured with S3 remote read credentials for `dvc pull`
- [ ] `speech_01` run once locally against the CI phrase fixture/param set; output pushed to the S3 remote via `dvc push`
- [ ] `ml/test/e2e_pipeline_test.py` updated to use `dvc repro --pull` instead of plain `dvc repro`
- [ ] A CI run confirmed to resolve `speech_01` via `--pull` rather than invoking `EdgeTtsProvider` live

### [ADR-353: PhonemeDecoder protocol + BeamSearchPhonemeDecoder](https://jodasoft.atlassian.net/browse/ADR-353) 🤖

**Depends on:** ADR-344

Builds the injectable phoneme-to-word reconstruction seam `ModelEvaluator` needs for word-level WER.

- [ ] `PhonemeDecoder` protocol (`ml/pipeline/speech/phoneme_decoder.py`): `decode(phonemes: list[str]) -> list[str]`
- [ ] `BeamSearchPhonemeDecoder`: lexicon-constrained beam search over `VocabResult.words_to_phonemes`
- [ ] Unit tests written test-first (pure-function testing against a hand-built `VocabResult`), landing in `ml/test/pipeline/speech/`

### [ADR-354: ModelEvaluator + speech_09/speech_10 stages](https://jodasoft.atlassian.net/browse/ADR-354) 🤖

**Depends on:** ADR-351, ADR-353

Completes the clean-audio vertical slice with validation metrics and packaged test fixtures.

- [ ] `ModelEvaluator` (`ml/pipeline/speech/model_evaluator.py`): constructor-injected `PhonemeDecoder`; shared `_run_predictions()`
- [ ] `evaluate()`: writes `evaluation_predictions.txt`, `metrics.json` (`wer`, `per`, `substitutions`, `insertions`, `deletions` — flat scalars for DVC experiment comparison), `evaluation_wer_by_command.json`
- [ ] `package_test_samples()`: zips audio for samples where hypothesis == reference into `test_samples.zip`
- [ ] `conventions.py` helpers: `evaluation_predictions_path`, `evaluation_metrics_path`, `evaluation_wer_by_command_path`, `test_samples_path`
- [ ] `speech_09_evaluate_model.py`/`speech_10_package_test_samples.py` entry points
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_09`/`speech_10`, both depending on `speech_08`'s trained model in addition to their own manifests; `dvc repro` runs end-to-end through `speech_10` — the clean vertical slice is now complete

### [ADR-355: DelayAugmentor + speech_02 stage](https://jodasoft.atlassian.net/browse/ADR-355) 🤖

**Depends on:** ADR-346, ADR-351

First augmentation-pass stage, inserted between TTS and the featurisation stages now that the clean pipeline is proven end-to-end.

- [ ] `DelayAugmentor(RandomizedModifierStage[AudioSample, AudioSample])` (`ml/pipeline/speech/delay_stage.py`): prefix/suffix silence augmentation, `precision=1`, reads/writes via `data_root`-relative paths
- [ ] `speech_02_add_delays.py` entry point
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_02`, inserted between `speech_01` and `speech_05`/`speech_06`; deps updated so `speech_05`/`speech_06` read from `speech_02`'s output; `dvc repro` runs end-to-end through the updated pipeline

### [ADR-356: NoiseProvider/_DirectoryNoiseProvider/BackgroundNoiseAugmentor + speech_00/speech_03 stages](https://jodasoft.atlassian.net/browse/ADR-356) 🤖

**Depends on:** ADR-355

Second augmentation-pass stage: mixes in background noise, with passthrough when unapplied, and the noise-sample download as its own cached DVC stage — the same pattern as `intent_00` for the phoneme dictionary.

- [ ] `speech_00_download_noise_samples.py` (plain script, not OOP) added, downloading background noise WAV files
- [ ] `speech_00` wired as its own `dvc.yaml` stage with a persisted, DVC-tracked output — noise samples are not re-downloaded on a `dvc repro` where they're already present and unchanged
- [ ] `NoiseProvider` protocol and `_DirectoryNoiseProvider` (`ml/pipeline/speech/background_noise_stage.py`): loads/resamples WAV noise files from `speech_00`'s output directory at construction
- [ ] `BackgroundNoiseAugmentor(RandomizedModifierStage[AudioSample, AudioSample])`: noise-mix augmentation, `precision=2`; skips writing and returns the unmodified input sample when unapplied
- [ ] `speech_03_add_background_noise.py` entry point
- [ ] Unit test asserting the writer is not called when `BackgroundNoiseAugmentor` draws a no-augmentation outcome
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml`/`params.yaml` wired for `speech_03` (deps on `speech_00` and `speech_02`), inserted between `speech_02` and `speech_05`/`speech_06`; `dvc repro` runs end-to-end through the updated pipeline

### [ADR-357: MicrophoneNoiseAugmentor + speech_04 stage + expanded downstream deps:](https://jodasoft.atlassian.net/browse/ADR-357) 🤖

**Depends on:** ADR-356, ADR-347, ADR-348

Final augmentation-pass stage, and the point at which the multi-directory `deps:` requirement (from the root-relative-paths decision) becomes real: `speech_05`/`speech_06` can now read a passthrough file from `speech_02/`, `speech_03/`, or `speech_04/`.

- [ ] `MicrophoneNoiseAugmentor(RandomizedModifierStage[AudioSample, AudioSample])` (`ml/pipeline/speech/mic_noise_stage.py`): Gaussian noise augmentation, `precision=3`; skips writing and returns the unmodified input sample when unapplied (amplitude zero)
- [ ] `speech_04_add_mic_noise.py` entry point
- [ ] Unit test asserting the writer is not called when `MicrophoneNoiseAugmentor` amplitude is zero
- [ ] Unit tests written test-first, landing in `ml/test/pipeline/speech/`
- [ ] `dvc.yaml` updated: `speech_04` wired in; `speech_05`/`speech_06`'s `deps:` expanded to list `speech_02/`, `speech_03/`, and `speech_04/` output directories, not just their immediate predecessor
- [ ] `dvc repro` runs end-to-end through the fully-augmented pipeline

### [ADR-358: Extend E2E test through augmentation + passthrough regression coverage](https://jodasoft.atlassian.net/browse/ADR-358) 🤖

**Depends on:** ADR-355, ADR-356, ADR-357

Closes out the augmentation pass by extending the front-loaded E2E test to cover the now-complete pipeline, per the incremental-wiring decision's requirement that the E2E test is re-run (not just written once) as later stages are added.

- [ ] `ml/test/e2e_pipeline_test.py` extended to run against the fully-augmented pipeline (all ten `speech_*` stages)
- [ ] E2E-level check added: a fully up-to-date `dvc repro` does not rewrite or copy any already-produced audio file
- [ ] `scripts/validate.sh` (which now includes `validate-ml-build`) and `scripts/validate-tests` pass

```gherkin
Feature: End-to-end augmented pipeline reproduction

  Scenario: Fresh run through the fully-augmented pipeline
    Given no prior pipeline outputs exist
    When "dvc repro --pull" is run with the CI phrase fixture and epochs=1
    Then intent_01 through speech_10 all execute
    And metrics.json and test_samples.zip are produced

  Scenario: Partial update after a constraint change
    Given a completed "dvc repro" run
    When a background-noise constraint in params.yaml is changed and "dvc repro" is run again
    Then only speech_03 and its downstream stages regenerate
    And unaffected samples in speech_03's output manifest keep their prior seed and content_hash

  Scenario: No-op augmentation does not copy audio
    Given a sample for which BackgroundNoiseAugmentor and MicrophoneNoiseAugmentor both draw a no-augmentation outcome
    When the pipeline runs
    Then no new audio file is written by either stage
    And the sample's path still resolves to its speech_02 output
```
