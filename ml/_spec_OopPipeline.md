# OOP ML Pipeline

> **Status:** Implementation-ready
> **Will become:** `_doc_MLPipeline.md` once implementation is complete

## Overview

Refactors the ML pipeline from numbered procedural scripts to a proper object-oriented design. Every pipeline stage becomes a Python class with injectable dependencies, enabling full unit testability with mocked collaborators — the same discipline used for the C# application code. A unified `Manifest[S]` / `Sample` abstraction replaces the existing multi-format CSV files and carries typed sample objects — with their applied values, seeds, and content hashes — through the entire pipeline, from intent phrase generation to model evaluation. The core innovation is a seed-based randomisation algorithm that stabilises sample generation across experiment changes: widening a noise range regenerates only the samples whose applied values would change; all others reuse their existing files. DVC continues to orchestrate stage execution; thin CLI entry points bridge DVC to the OOP classes.

## Responsibilities & Boundaries

- **Owns:** All pipeline stage logic (`ml/pipeline/`); `Manifest`/`Sample` data model and JSON serialisation; seed-based randomisation algorithm; `PassFilter` implementations; per-stage injectable protocol abstractions; DVC entry-point scripts; `dvc.yaml`; `params.yaml`
- **Does not own:** Model architecture decisions; TensorFlow/Keras internals; the edge_tts service; the CMU Pronouncing Dictionary source data
- **Integrates with:** DVC (`params.yaml` for experiment parameters, `dvc.yaml` for stage wiring); edge_tts for TTS generation; TensorFlow/Keras for model training and evaluation; librosa for spectrogram computation; CMU Pronouncing Dictionary for phoneme lookup

## Key Design Decisions

### All stages get OOP classes

_Context:_ Procedural scripts with module-level side effects, hardcoded paths, and `if __name__ == "__main__"` guards are difficult to unit test. The Jira requires the same testability standard as the C# application code.

_Decision:_ Every pipeline stage is a class with dependencies injected via its constructor. This is idiomatic Python: constructor injection is standard, `Protocol` classes are the Python-native way to define injectable interfaces (structural typing — no inheritance required), and `dataclass` is the natural container type. `ABC` is used only where there is real shared *implementation* to inherit — specifically `ModifierStage`, which encapsulates the skip-unchanged and GC logic. Stages with no shared implementation are standalone classes; no artificial common base is added just for uniformity.

_Consequences:_ Every class is independently testable with mocked collaborators. DVC entry points become thin wrappers.

---

### Unified end-to-end Manifest format

_Context:_ The existing pipeline uses two distinct CSV formats — a variation CSV for augmentation stages and set-manifest CSVs for training/evaluation — making sample lineage opaque and parameter tracking fragile.

_Decision:_ A single `Manifest[S]` class (generic over a `Sample` subtype) replaces both formats. The manifest serialises to JSON and carries `TextSample`, `AudioSample`, `SampleSpectrogram`, or `SampleTokens` objects with their applied values, seeds, and content hashes. Train/val/test splits are three separate files of the same format. All pipeline stages — including featurisation, training, and evaluation — consume the manifest type directly.

_Consequences:_ All consumers share one JSON schema. The schema must be stable across pipeline runs to preserve stored seeds. Existing DVC stage I/O paths change.

---

### ModifierStage for all per-sample file transformations

_Context:_ The skip-unchanged and GC logic is valuable for any stage that transforms files one-by-one — not just data augmentation. Spectrogram computation has historically been expensive; recomputing all spectrograms when only a few samples changed wastes significant time.

_Decision:_ `ModifierStage[T_in, T_out]` is used for every stage that transforms an input manifest into an output manifest on a per-sample basis: augmentation stages (`TtsSampleGenerator`, `DelayAugmentor`, `BackgroundNoiseAugmentor`, `MicrophoneNoiseAugmentor`) and featurisation stages (`SpectrogramStage`, `TokenStage`). Stages with no randomisation (featurisation) set `_is_deterministic = True`; `transform()` uses `output_seed = 0` for those stages. Intent phrase generation is handled by `PhraseVariator` (called from the entry-point) and is not a `ModifierStage` subclass — see the "ml/pipeline/ package" design decision below.

_Consequences:_ `SampleSpectrogram` and `SampleTokens` carry a `seed` field set to 0. This is a minor inconsistency accepted in exchange for full code reuse of the skip-unchanged, GC, and manifest-management logic.

---

### Content hash determines sample identity

_Context:_ The goal is to skip regeneration for samples whose upstream source did not change, while regenerating those that did. DVC will skip a stage entirely if no deps change, but will re-run a stage if even one dep file changes — it cannot skip individual samples within a stage.

_Decision:_ All non-text samples use a **unified content hash formula**:

```
content_hash = sha256(parent_content_hash + ":" + str(seed) + ":" + canonical(applied_values))
```

Where `canonical(applied_values) = json.dumps(applied_values, sort_keys=True, separators=(',',':'), ensure_ascii=True)` and `str(seed)` is the decimal representation of the integer seed. All numeric values in `applied_values` are stored as raw int/float, never as formatted strings, to ensure hash stability across code changes.

For `TextSample`: `content_hash = sha256(content.encode('utf-8'))` (no parent).

For deterministic stages (`SpectrogramStage`, `TokenStage`): `seed = 0` and `applied_values = {}`, so:
```
content_hash = sha256(parent_content_hash + ":0:{}")
```

Every `SampleWithPath` output type stores `parent_content_hash: str` (the content hash of the sample it was derived from). This field is the lookup key for skip-unchanged detection across all stage types, including chained `AudioSample → AudioSample` stages.

All `ModifierStage` output dirs are configured with `persist: true` in `dvc.yaml` so DVC does not delete them between runs; `ModifierStage` handles GC of unreferenced files itself. GC algorithm: after building the output manifest, collect `{sample.path.name for sample in output_samples}`; glob `output_dir` flat (non-recursive); delete any file not in that set and not named `manifest.json`. DVC tracks directory outputs by hashing all files and automatically picks up GC changes on the next `dvc repro` — no manual `dvc commit` needed.

**Terminology:**
- **Variation constraints** — inputs from `params.yaml` controlling randomisation shape (min/max, frequency, distribution). Not stored on the sample.
- **Applied values** — the specific values `VariationGenerator` selected, stored in `AudioSample.applied_values` and included in `content_hash`.

_Consequences:_ A sample is regenerated only when its source content or applied values change. Applied values are stable across runs for the same source and seed. Changing constraints regenerates only samples whose rejection-sampling chain selects a different value.

---

### Seed-based randomisation with pass filters

_Context:_ Experiments frequently adjust variation constraints. Without a stable algorithm, all samples regenerate on every constraint change.

_Decision:_ Each new output sample gets a seed via `int.from_bytes(os.urandom(8), 'big')`, stored in the manifest. If an input sample was seen before (matched by `parent_content_hash`), the stored seed is reused. Per-variable sub-seeds are derived as `int.from_bytes(sha256(f"{seed}:{variable_name}").digest()[:8], 'big')`. The frequency check uses a `:vary` sub-key: `sha256(f"{seed}:{variable_name}:vary")`. Rejection sampling draws candidates uniformly from `pass_filter.sample_domain()` and accepts them with probability `pass_filter.density(candidate)`. This makes each variable's value independent of ordering — reordering or adding variables in `_get_applied_values` does not affect existing variables' values.

_Consequences:_ The randomisation logic is non-trivial and must be fully deterministic and independently reproducible.

---

### Previous output manifest as the seed store

_Context:_ Seeds must persist across DVC reruns. DVC invalidates outputs when params change, but `persist: true` preserves output files.

_Decision:_ Every `ModifierStage` writes its output manifest to a path supplied by the entry-point. On each run, `transform` reads the previous manifest (if present) via `ManifestStore` to recover stored seeds. Seed recovery uses a **three-case algorithm**:
- **Skip**: previous output found for this input (`parent_content_hash` matches) AND recomputing the content hash with the stored seed and new constraints gives the same result → keep output unchanged.
- **Regenerate with stored seed**: previous output found BUT constraints changed, giving different applied values → re-run `_generate_output` with the same id and seed; new content_hash reflects new applied values.
- **New sample**: no previous output found → assign new id and seed via `os.urandom`.

_Consequences:_ Deleting the output manifest resets all seeds. DVC stages using `ModifierStage` must have `persist: true`.

---

### Python generics for typed stage input/output

_Context:_ `ModifierStage[T_in, T_out]` and `Manifest[S]` have meaningful type parameters.

_Decision:_ Use `typing.Generic[T_in, T_out]` with `TypeVar`. Python generics are **erased at runtime**; type parameters are for mypy only. `from __future__ import annotations` required for forward references.

---

### Directories as stage I/O boundaries

_Context:_ The existing pipeline duplicates paths in both `cmd:` and `deps:` of `dvc.yaml`.

_Decision:_ Every stage exchanges whole directories. Entry-point scripts resolve file paths via `conventions.py`. `ManifestStore` and `ModifierStage` accept explicit `Path` arguments. DVC `deps` lists input directories; `outs` lists the output directory.

_Consequences:_ `dvc.yaml` entries are short. File convention changes require updating only `conventions.py`.

---

### ml/pipeline/ package with thin DVC entry points

_Context:_ The existing `ml/scripts/` tree will be deleted before implementation begins.

_Decision:_ All OOP code lives in `ml/pipeline/`. Each DVC stage is a minimal entry-point script in `ml/pipeline/stages/` that parses CLI args, resolves paths via `conventions.py`, constructs the stage with injected dependencies, and calls it. Stage filenames carry a two-digit prefix for sort order.

The initial `Manifest[TextSample]` is bootstrapped in `intent_01_generate_phrases.py`. The entry-point constructs `PhraseVariator(random.Random(42))` and calls it to generate surface-form variants from the input CSV (reading the `phrase` and `command` columns). Each valid variant becomes a `TextSample` with `content = surface_form` and `label = command` (speech_to_detect). The entry-point applies `subsample_rate` filter and writes `Manifest[TextSample]`.

The CMU Pronouncing Dictionary download is retained as a plain non-OOP DVC stage. The existing `ml/scripts/` directory and old `dvc.yaml` are deleted as the first implementation step; the new `dvc.yaml` is written from scratch.

**Required params.yaml keys:**

| Key | Default | Description |
|-----|---------|-------------|
| `pipeline.input_phrases_path` | `scripts/intent_prediction/01_input_phrases.csv` | Relative to DVC root (`ml/`); overridden in CI |
| `pipeline.subsample_rate` | `1` | 1 in N phrase variants; 1 = all |
| `pipeline.variations_per_phrase` | (impl) | Variants attempted per base phrase in PhraseVariator |
| `pipeline.n_mels` | (impl) | Spectrogram mel bands |
| `pipeline.time_steps` | (impl) | Spectrogram time dimension |
| `pipeline.input_token_length` | (impl) | Padded token length |
| `pipeline.epochs` | (impl) | Overridden in CI |
| `pipeline.batch_size` | (impl) | Training batch size |

Per-stage variation constraints are under `stages.<stage_name>:` sections. Required keys:

**`stages.create_set_manifests`** — split percentages (must sum to 100):
- `train_pct`, `val_pct`, `test_pct` (int)

**`stages.add_delays`** — delay augmentation:
- `prefix_vary_probability`, `suffix_vary_probability` (float, 0.0–1.0)
- `prefix_min_s`, `prefix_max_s`, `suffix_min_s`, `suffix_max_s` (float, seconds)

**`stages.add_background_noise`** — background noise augmentation:
- `vary_probability` (float)
- `volume_min`, `volume_max` (float, multiplier applied to noise signal)

**`stages.add_mic_noise`** — microphone noise augmentation:
- `vary_probability` (float)
- `amplitude_min`, `amplitude_max` (float)

**`stages.generate_speech_samples`** — TTS speech rate:
- `speech_rate_min`, `speech_rate_max` (int, percent; e.g. -10 to +20)

---

### Live TTS in CI with a small fixed phrase set

_Context:_ The Jira requires an E2E CI test that runs the full pipeline on a small sample.

_Decision:_ CI uses `dvc repro --set-param` flags to override expensive parameters:

```bash
dvc repro \
  --set-param pipeline.input_phrases_path=test/fixtures/ci_phrases.csv \
  --set-param pipeline.epochs=1 \
  --set-param pipeline.subsample_rate=100
```

Edge_tts is called live. The `TtsProvider` protocol is the replacement seam if TTS strategy changes.

## Planned Implementation

### Directory Layout

```
ml/
  pipeline/
    __init__.py
    core/
      sample.py           # Sample, SampleWithPath, TextSample, AudioSample,
                          # SampleSpectrogram, SampleTokens
      manifest.py         # Manifest[S], ManifestStore
      modifier_stage.py   # ModifierStage[T_in, T_out]
      randomization.py    # VariationGenerator, PassFilter, MinMaxFilter, NormalFilter
    io/
      audio_io.py         # AudioReader, AudioWriter protocols + defaults
    intent/
      phrase_variator.py  # PhraseVariator (rng injectable; ports existing logic + sanity_check)
      vocab_computer.py   # VocabComputer
    speech/
      tts_stage.py                    # TtsSampleGenerator(ModifierStage[TextSample, AudioSample])
      delay_stage.py                  # DelayAugmentor(ModifierStage[AudioSample, AudioSample])
      background_noise_stage.py       # BackgroundNoiseAugmentor(ModifierStage[AudioSample, AudioSample])
      mic_noise_stage.py              # MicrophoneNoiseAugmentor(ModifierStage[AudioSample, AudioSample])
      set_splitter.py                 # SetManifestSplitter
      token_stage.py                  # TokenStage(ModifierStage[AudioSample, SampleTokens])
      spectrogram_stage.py            # SpectrogramStage(ModifierStage[AudioSample, SampleSpectrogram])
      model_trainer.py                # ModelTrainer
      model_evaluator.py              # ModelEvaluator
    stages/
      conventions.py
      intent_01_generate_phrases.py   # PhraseVariator; bootstraps Manifest[TextSample]
      intent_02_compute_vocab.py
      speech_03_generate_samples.py
      speech_04_add_delays.py
      speech_05_add_background_noise.py
      speech_06_add_mic_noise.py
      speech_07_compute_tokens.py
      speech_08_compute_spectrograms.py
      speech_09_create_set_manifests.py
      speech_10_train_model.py
      speech_11_evaluate_model.py     # val set: writes metrics.json
      speech_12_package_test_samples.py  # test set: writes test_samples.zip for app E2E tests
  test/
    pipeline/
      core/
      intent/
      speech/
    e2e_pipeline_test.py  # pytest; invokes dvc repro via subprocess
    fixtures/
      ci_phrases.csv      # 10 canonical phrases
  download_phoneme_dictionary.py
  dvc.yaml                # written from scratch
  params.yaml
```

### Interfaces

#### conventions.py

```python
def manifest_path(output_dir: Path) -> Path:
    return output_dir / "manifest.json"

def split_manifest_path(output_dir: Path, split: str) -> Path:
    """split: 'train', 'val', or 'test'"""
    return output_dir / f"{split}.json"

def sample_file_path(output_dir: Path, sample_id: str, ext: str) -> Path:
    """ext has no leading dot, e.g. 'wav', 'npy', 'json'"""
    return output_dir / f"{sample_id}.{ext}"

def model_path(output_dir: Path) -> Path:
    return output_dir / "speech_to_text_model.keras"

def evaluation_predictions_path(output_dir: Path) -> Path:
    return output_dir / "evaluation_predictions.txt"

def evaluation_metrics_path(output_dir: Path) -> Path:
    """JSON file written by ModelEvaluator: {"wer": <float>}"""
    return output_dir / "metrics.json"

def test_samples_path(output_dir: Path) -> Path:
    """Zip written by ModelEvaluator.package_test_samples(): known-good audio fixtures."""
    return output_dir / "test_samples.zip"
```

---

#### Sample and Manifest

```python
@dataclass
class Sample(ABC):
    id: str            # human-readable stable identifier derived from input values + applied values
                       # TextSample: uuid4() — has no file; not user-visible
                       # SampleWithPath types: derived by each stage's _derive_id(); used as filename stem
                       # e.g. "TV_ON_Jenny_r77_pre40_suf0" after TTS + delay stages
    seed: int          # stable as long as parent_content_hash is unchanged; 0 for deterministic
    content_hash: str  # sha256(parent_content_hash + ":" + str(seed) + ":" + canonical(applied))
                       # exception: TextSample uses sha256(content.encode('utf-8'))

@dataclass
class SampleWithPath(Sample, ABC):
    """All ModifierStage T_out types. GC uses sample.path.name."""
    path: Path                 # relative filename derived from id (e.g. 'TV_ON_Jenny_r77.wav')
    parent_content_hash: str   # content_hash of the sample this was derived from;
                               # used as the skip-unchanged lookup key in transform()

@dataclass
class TextSample(Sample):
    content: str   # phrase to speak (surface form variation)
    label: str     # speech_to_detect — what the model outputs
    # seed = 0; no parent_content_hash (bootstrapped, not from a ModifierStage)

@dataclass
class AudioSample(SampleWithPath):
    transcript: str                   # speech_to_detect (= TextSample.label)
    applied_values: dict[str, Any]    # raw int/float values; see stage specs below

@dataclass
class SampleSpectrogram(SampleWithPath):
    transcript: str
    parent_id: str    # id of the AudioSample; used by ModelTrainer/Evaluator for lookup

@dataclass
class SampleTokens(SampleWithPath):
    transcript: str
    parent_id: str    # id of the AudioSample; used by ModelTrainer/Evaluator for lookup

class Manifest(Generic[S]):
    def __init__(self, samples: Sequence[S]): ...
    @property
    def samples(self) -> tuple[S, ...]: ...
    def by_content_hash(self, h: str) -> S | None: ...
    def by_id(self, id: str) -> S | None: ...

class ManifestStore:
    def read(self, path: Path) -> Manifest[Any]:
        """Deserialise using 'sample_type' field:
          'text' → TextSample, 'audio' → AudioSample,
          'spectrogram' → SampleSpectrogram, 'tokens' → SampleTokens.
        Path fields are relative filenames; callers prepend output_dir."""
        ...
    def write(self, manifest: Manifest, path: Path) -> None: ...
```

JSON schema (version 1):
```json
{
  "version": 1,
  "sample_type": "audio",
  "samples": [
    {
      "id": "uuid",
      "path": "uuid.wav",
      "transcript": "TV_ON",
      "seed": 67890,
      "content_hash": "sha256hex",
      "parent_content_hash": "sha256hex",
      "applied_values": { "voice": "en-US-JennyNeural", "speech_rate": 5 }
    }
  ]
}
```

`"sample_type"` declared once per manifest. Valid values: `"text"`, `"audio"`, `"spectrogram"`, `"tokens"`. `"path"` is a filename with no directory component. Numeric values in `applied_values` stored as raw int/float.

`TextSample` JSON (no `path`, `parent_content_hash`, or `applied_values`; `seed` is always 0 and is serialised):
```json
{
  "version": 1,
  "sample_type": "text",
  "samples": [
    { "id": "uuid", "seed": 0, "content_hash": "sha256hex",
      "content": "okay turn on the tv", "label": "TV_ON" }
  ]
}
```

`SampleSpectrogram` / `SampleTokens` JSON (include `parent_id`; no `applied_values` since always `{}`):
```json
{
  "version": 1,
  "sample_type": "spectrogram",
  "samples": [
    { "id": "uuid", "path": "uuid.npy", "seed": 0, "content_hash": "sha256hex",
      "parent_content_hash": "sha256hex", "transcript": "TV_ON", "parent_id": "audio-uuid" }
  ]
}
```

---

#### PhraseVariator

```python
class PhraseVariator:
    def __init__(self, rng: random.Random): ...
    def generate(
        self,
        base_phrases: Sequence[tuple[str, str]],  # (phrase, command) from 01_input_phrases.csv
        variations_per_phrase: int,
    ) -> list[TextSample]:
        """Ports _create_variation() and sanity_check() from the existing VariationGenerator
        class in 01_generate_phrases.py (pleasantries, hesitations, case transforms, spelling
        variants, repeats). NOT the full generate_variations() / incremental-deduplication loop
        — that logic is replaced by a simple loop: for each base phrase, attempt to generate
        `variations_per_phrase` valid variants; each attempt calls _create_variation() once
        and passes it through sanity_check(). Does NOT port `target_samples` or
        `load_existing_variations()` — no incremental logic.
        This is NOT the new VariationGenerator in randomization.py (the seed-based numeric
        randomiser).
        "Port" means: every `random.*` module-level call in the original is replaced by
        `self.rng.*` — same logic, no restructuring. Output must be identical for a fixed seed.
        TextSample.content = surface form with all transformations (what TTS will speak);
        TextSample.label = command (speech_to_detect, the canonical form the model should output).
        Each output: id=uuid4(), seed=0 (PhraseVariator explicitly sets seed=0 at construction),
        content_hash=sha256(content.encode('utf-8')).
        Entry-point uses rng=random.Random(42) and reads variations_per_phrase from params.yaml."""
        ...
```

`intent_01_generate_phrases.py`:
1. Reads `pipeline.input_phrases_path` CSV, columns `phrase` (surface_form) and `command` (speech_to_detect)
2. Constructs `PhraseVariator(random.Random(42))`
3. Calls `generate(base_phrases)` → `list[TextSample]`
4. Applies: `[s for i, s in enumerate(variants) if i % subsample_rate == 0]`
5. Writes `Manifest[TextSample]`

---

#### PassFilter and VariationGenerator

```python
class PassFilter(ABC):
    @abstractmethod
    def density(self, value: float) -> float:
        """Normalised density; max == 1.0. Acceptance probability in rejection sampling."""
        ...

    @abstractmethod
    def sample_domain(self) -> tuple[float, float]:
        """(low, high): range for uniform candidate generation.
        MinMaxFilter: (min_val, max_val).
        NormalFilter: (mean - 5*std_dev, mean + 5*std_dev)."""
        ...

class MinMaxFilter(PassFilter):
    """Uniform over [min_val, max_val]. density() == 1.0 in range, 0.0 outside."""
    def __init__(self, min_val: float, max_val: float): ...

class NormalFilter(PassFilter):
    """Gaussian. density(x) = gaussian_pdf(x)/gaussian_pdf(mean); peak == 1.0.
    Raises ValueError if std_dev <= 0."""
    def __init__(self, mean: float, std_dev: float): ...

class VariationGenerator:
    def __init__(self, sample_seed: int): ...

    def should_vary(self, variable_name: str, frequency: float) -> bool:
        """True with probability frequency.
        int.from_bytes(sha256(f"{seed}:{variable_name}:vary").digest()[:8], 'big') / 2^64 < frequency"""
        ...

    def generate(self, variable_name: str, pass_filter: PassFilter) -> float:
        """Rejection-sample deterministically. For attempt n = 0, 1, ...:
          domain_low, domain_high = pass_filter.sample_domain()
          raw = int.from_bytes(sha256(f"{seed}:{variable_name}:{n}").digest()[:8], 'big')
          candidate = domain_low + (raw / 2^64) * (domain_high - domain_low)
          accept_raw = int.from_bytes(sha256(f"{seed}:{variable_name}:{n}:accept").digest()[:8], 'big')
          if (accept_raw / 2^64) < pass_filter.density(candidate): return candidate
        Raises ValueError after 1000 iterations."""
        ...

    def generate_int(self, variable_name: str, pass_filter: MinMaxFilter) -> int:
        """Integer in [int(min_val), int(max_val)] inclusive, uniform distribution.
        Uses attempt-indexed hashes exactly like generate() — for attempt n = 0, 1, ...:
          raw_int = int.from_bytes(sha256(f"{seed}:{variable_name}:{n}").digest()[:8], 'big')
        Then bitmask rejection: range = int(max_val) - int(min_val);
          mask = 2^ceil(log2(range+1))-1.
          When range == 0: mask = 0; returns int(min_val) immediately (n=0, no loop).
          candidate = int(min_val) + (raw_int & mask); accepted if candidate <= int(max_val).
        Raises ValueError after 1000 iterations (same guard as generate())."""
        ...

    def choose(self, variable_name: str, options: Sequence[T]) -> T:
        """Direct selection, no rejection loop:
        idx = int.from_bytes(sha256(f"{seed}:{variable_name}:0").digest()[:8], 'big') % len(options)
        return options[idx]"""
        ...
```

---

#### ModifierStage

```python
T_out = TypeVar('T_out', bound=SampleWithPath)

class ModifierStage(ABC, Generic[T_in, T_out]):
    _is_deterministic: ClassVar[bool] = False
    # SpectrogramStage and TokenStage set this to True.
    # transform() uses output_seed = 0 when True; os.urandom(8) otherwise.

    def __init__(self, output_dir: Path, manifest_store: ManifestStore): ...

    async def transform(
        self,
        input_manifest: Manifest[T_in],
        manifest_path: Path,
    ) -> Manifest[T_out]:
        """
        Entry-point scripts call: asyncio.run(stage.transform(manifest, path))

        Steps:
        1. Read previous output manifest from manifest_path (if present).
           Build index: prev_by_parent = {out.parent_content_hash: out for out in prev.samples}

        2. For each input_sample in input_manifest.samples:

           a. prev_out = prev_by_parent.get(input_sample.content_hash)

           b. If prev_out is not None:
              - Compute new_applied = _get_applied_values(input_sample,
                                       VariationGenerator(prev_out.seed))
              - Compute expected_hash = sha256(input_sample.content_hash + ":" +
                                         str(prev_out.seed) + ":" + canonical(new_applied))
              - If expected_hash == prev_out.content_hash:
                  → KEEP prev_out unchanged (step is skipped; file already exists)
              - Else (constraints changed → different applied_values):
                  → new_id = _derive_id(input_sample, new_applied)
                  → await _generate_output(input_sample,
                        output_id=new_id, output_seed=prev_out.seed,
                        applied_values=new_applied,
                        parent_content_hash=input_sample.content_hash)
                  (seed preserved; id, content_hash, and file updated; old file GC'd in step 3)

           c. If prev_out is None (new sample):
              - output_seed = 0 if self._is_deterministic else int.from_bytes(os.urandom(8), 'big')
              - generator = VariationGenerator(output_seed)
              - new_applied = _get_applied_values(input_sample, generator)
              - output_id = _derive_id(input_sample, new_applied)
              - await _generate_output(input_sample, output_id, output_seed,
                    new_applied, input_sample.content_hash)

        3. GC: collect {sample.path.name for sample in output_samples};
           flat-glob output_dir; delete files not in that set and not 'manifest.json'.

        4. Write output manifest to manifest_path.
        """
        ...

    @abstractmethod
    def _get_applied_values(
        self, sample: T_in, generator: VariationGenerator
    ) -> dict[str, Any]:
        """Return applied values dict. Return {} for deterministic stages."""
        ...

    @abstractmethod
    async def _generate_output(
        self,
        input_sample: T_in,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> T_out:
        """Generate output file; return complete output Sample with output_id, output_seed,
        parent_content_hash, and content_hash all set.
        MUST compute content_hash via _compute_content_hash — do not reimplement the formula."""
        ...

    @abstractmethod
    def _derive_id(self, input_sample: T_in, applied_values: dict[str, Any]) -> str:
        """Return the id (= filename stem) for the output sample.
        Called for both new samples AND regens with changed constraints — the id must be
        deterministically derivable from input_sample.id and applied_values alone.
        Each stage composes: f"{input_sample.id}_{stage_suffix(applied_values)}".
        SpectrogramStage and TokenStage return input_sample.id unchanged (same stem,
        different extension).
        Uniqueness within the output directory is guaranteed because input_sample.id is
        already unique and all stages only write to their own output directory."""
        ...

    @staticmethod
    def _compute_content_hash(
        parent_content_hash: str, output_seed: int, applied_values: dict[str, Any]
    ) -> str:
        """Compute the canonical content_hash for a non-text output sample.
        All _generate_output implementations MUST call this — it is the single source of
        truth for the hash formula; reimplementing it risks silent skip-detection breakage.
          content_hash = sha256(parent_content_hash + ":" + str(output_seed) + ":" +
                                json.dumps(applied_values, sort_keys=True,
                                           separators=(',',':'), ensure_ascii=True))"""
        ...
```

---

#### Stage Constructor Signatures and Applied Values

```python
class TtsSampleGenerator(ModifierStage[TextSample, AudioSample]):
    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        tts_provider: TtsProvider,  # retries are the TtsProvider's responsibility
        voices: list[str],          # en-US female ShortNames; fetched by entry-point via
                                    # asyncio.run(edge_tts.list_voices()), filtered:
                                    # Gender=='Female', Locale=='en-US',
                                    # ':' not in ShortName, 'DragonHD'/'Turbo' not in ShortName
    ): ...
    # applied_values: {"voice": str, "speech_rate": int}
    # voice: VariationGenerator.choose("voice", voices)
    # speech_rate: int (raw, e.g. 5 for +5%), from generate_int("speech_rate", MinMaxFilter(...))
    # edge_tts rate string (e.g. "+5%") is formatted in _generate_output, NOT stored
    #
    # Synthesis: tts_provider.synthesize(text=input_sample.content, ...)
    #   TextSample.content is the full spoken form (surface form with pleasantries/hesitations)
    #   AudioSample.transcript is set to input_sample.label (speech_to_detect, no hesitations)
    #   i.e. TTS speaks "um, turn on the TV" but the model is trained to output "TV_ON"
    #
    # _derive_id: f"{input_sample.label}_{voice_short}_r{speech_rate + 100}"
    #   voice_short = voice.split('-')[-1].replace('Neural', '')  # "en-US-JennyNeural" → "Jenny"
    #   e.g. "TV_ON_Jenny_r77" for label="TV_ON", voice=JennyNeural, rate=-23%

class DelayAugmentor(ModifierStage[AudioSample, AudioSample]):
    # applied_values: {"prefix_delay_s": float, "suffix_delay_s": float}
    # Each drawn via generate("prefix_delay_s", MinMaxFilter(min_s, max_s)) if should_vary(...) else 0.0
    # 0.0 stored when not applied — both keys always present in applied_values for hash stability
    #
    # _derive_id: f"{input_sample.id}_pre{int(prefix_delay_s*1000)}_suf{int(suffix_delay_s*1000)}"
    #   e.g. "TV_ON_Jenny_r77_pre40_suf0" for 40ms prefix, no suffix
    # params.yaml keys (new float semantics; replaces old integer 1-in-N frequency keys):
    #   stages.add_delays.prefix_vary_probability: float   # e.g. 0.333 = 1-in-3 chance
    #   stages.add_delays.prefix_min_s: float
    #   stages.add_delays.prefix_max_s: float
    #   stages.add_delays.suffix_vary_probability: float
    #   stages.add_delays.suffix_min_s: float
    #   stages.add_delays.suffix_max_s: float

class BackgroundNoiseAugmentor(ModifierStage[AudioSample, AudioSample]):
    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        noise_provider: NoiseProvider,
        audio_reader: AudioReader,
        audio_writer: AudioWriter,
    ): ...
    # applied_values: {"noise_file": str, "noise_start_s": float, "noise_volume": float}
    # noise_file: filename only (no path), from:
    #   VariationGenerator.choose("noise_file", sorted([p.name for p in provider.list_files()]))
    #   sorted() ensures OS-independent determinism
    # choose() is ALWAYS called (regardless of should_vary), so the filename is always stored
    # noise_start_s and noise_volume are 0.0 if should_vary returns False
    # → all three keys always present in applied_values for hash stability
    #
    # noise_start_s bounds: derived at runtime from file durations.
    #   max_start_s = noise_file_duration_s - audio_sample_duration_s
    #   Filter: MinMaxFilter(0.0, max(0.0, max_start_s))
    #   Both durations read via AudioReader; max(0.0, ...) handles edge case where noise is
    #   shorter than the audio sample (start forced to 0.0).
    #
    # _derive_id: f"{input_sample.id}_{noise_filestem}_v{int(noise_volume*100)}"
    #   noise_filestem = Path(noise_file).stem  (noise_file is always stored per choose() contract)
    #   e.g. "TV_ON_Jenny_r77_pre40_suf0_BabyCry_v0" when volume=0 (noise not applied)
    #         "TV_ON_Jenny_r77_pre40_suf0_CafeFar_v45" when volume=0.45

class MicrophoneNoiseAugmentor(ModifierStage[AudioSample, AudioSample]):
    # applied_values: {"mic_noise_amplitude": float}
    # amplitude: 0.0 if should_vary returns False; drawn from MinMaxFilter otherwise
    #
    # _derive_id: f"{input_sample.id}_mic{int(mic_noise_amplitude*1000)}"
    #   e.g. "TV_ON_Jenny_r77_pre40_suf0_CafeFar_v45_mic0" (not applied)
    #         "TV_ON_Jenny_r77_pre40_suf0_CafeFar_v45_mic12" (amplitude=0.012)

class SpectrogramStage(ModifierStage[AudioSample, SampleSpectrogram]):
    _is_deterministic = True
    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        n_mels: int,
        time_steps: int,
        audio_reader: AudioReader,
    ): ...
    # _get_applied_values returns {}; output is .npy file of shape (n_mels, time_steps)
    # _derive_id: return input_sample.id  (same stem, .npy extension)

class TokenStage(ModifierStage[AudioSample, SampleTokens]):
    _is_deterministic = True
    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        vocab: VocabResult,
        input_token_length: int,
    ): ...
    # _get_applied_values returns {}; tokens derived from AudioSample.transcript
    # output is .json file named {id}.json (conventions.sample_file_path(output_dir, id, 'json'))
    # content: {"phonemes": [...], "tokens": [...]} padded to input_token_length
    # _derive_id: return input_sample.id  (same stem as spectrogram and audio; .json extension)
```

---

#### IO Protocols

```python
class AudioReader(Protocol):
    async def read(self, path: Path) -> tuple[np.ndarray, int]: ...
    # Returns (samples, sample_rate). Array is always 1-D mono float32.
    # If the source file is stereo, the implementation converts to mono before returning.
    # Consumers never need to handle channel reduction.

class AudioWriter(Protocol):
    async def write(self, path: Path, data: np.ndarray, sample_rate: int) -> None: ...

class TtsProvider(Protocol):
    async def synthesize(self, text: str, voice: str, rate: str, output_path: Path) -> None: ...
    # Retries are the implementation's responsibility (not TtsSampleGenerator's)

class NoiseProvider(Protocol):
    def list_files(self) -> list[Path]: ...

class PhonemeProvider(Protocol):
    def lookup(self, word: str) -> list[str]: ...
    # Raises PhonemeNotFoundError if word not in dictionary
```

---

#### Non-ModifierStage Classes

```python
class PhraseVariator:
    # See PhraseVariator section above

class VocabComputer:
    def __init__(self, phoneme_provider: PhonemeProvider): ...
    def compute(self, manifest: Manifest[TextSample], output_dir: Path) -> VocabResult:
        """Extract phoneme vocabulary from TextSample.label values (speech_to_detect).
        INTENTIONAL CHANGE from existing pipeline (which used surface_form):
        label is what the model outputs; surface forms are irrelevant for vocabulary coverage.
        Labels are canonical command names (TV_ON, VOLUME_UP, etc.) — no digits.
        Digit-to-word substitution (e.g. '1'→'ONE') from the old pipeline is NOT needed
        and is NOT ported.
        Writes to output_dir: phoneme_list.txt and words_to_phonemes.json.
        The phoneme_trie.json from the old pipeline is dropped (not in VocabResult)."""
        ...

@dataclass
class VocabResult:
    phoneme_list: list[str]
    words_to_phonemes: dict[str, list[str]]
    ctc_blank_idx: int  # = len(phoneme_list); blank token appended at end — matches existing io_utils convention

class SetManifestSplitter:
    def __init__(self, seed: int = 42): ...
    def split(
        self, manifest: Manifest[AudioSample],
        train_pct: int, val_pct: int, test_pct: int,
        output_dir: Path,
    ) -> tuple[Manifest[AudioSample], Manifest[AudioSample], Manifest[AudioSample]]:
        """Shuffle and split by individual AudioSample (not stratified by transcript).
        Writes train.json, val.json, test.json to output_dir. Percentages must sum to 100.
        Input is the fully-augmented manifest from MicrophoneNoiseAugmentor.
        INTENTIONAL CHANGE from existing pipeline (which split clean audio only).
        Does NOT reassign ids — the AudioSample objects in the split manifests are the same
        objects from the input manifest with unchanged id values. ModelTrainer/ModelEvaluator
        filter SampleSpectrogram/SampleTokens via {parent_id in {s.id for s in split_manifest}}."""
        ...

class KerasBackend(Protocol):
    def build_ctc_model(self, num_classes: int, n_mels: int, time_steps: int) -> Any: ...
    def train(
        self, model: Any,
        dataset: Any,   # tf.data.Dataset yielding (spectrogram, tokens) tuples;
                        # spectrogram shape (n_mels, time_steps), tokens shape (input_token_length,)
                        # ModelTrainer is responsible for batching and prefetching
        epochs: int,
    ) -> list[float]: ...  # per-epoch loss values (logged but not written to disk)
    def predict(self, model: Any, dataset: Any) -> np.ndarray: ...
    def save(self, model: Any, path: Path) -> None: ...
    def load(self, path: Path) -> Any: ...

class ModelTrainer:
    def __init__(self, keras_backend: KerasBackend): ...
    def train(
        self,
        train_manifest: Manifest[AudioSample],
        vocab: VocabResult,
        spectrogram_manifest: Manifest[SampleSpectrogram],
        token_manifest: Manifest[SampleTokens],
        spectrogram_dir: Path,
        token_dir: Path,
        output_dir: Path,
    ) -> Path:
        """spectrogram_manifest and token_manifest are the FULL combined manifests from
        SpectrogramStage/TokenStage — they cover all splits (train + val + test).
        train_manifest is the split subset. Filter: keep only spectrogram/token entries
        where parent_id ∈ {s.id for s in train_manifest.samples}.
        Build {parent_id: sample} lookup dicts from the filtered sets.
        Construct tf.data.Dataset with batching/prefetching.
        Call KerasBackend.train, then KerasBackend.save to conventions.model_path(output_dir).
        Return the saved model path."""
        ...

class ModelEvaluator:
    def __init__(self, keras_backend: KerasBackend): ...
    def evaluate(
        self,
        manifest: Manifest[AudioSample],
        model_path: Path,
        vocab: VocabResult,
        spectrogram_manifest: Manifest[SampleSpectrogram],
        token_manifest: Manifest[SampleTokens],
        spectrogram_dir: Path,
        token_dir: Path,
        output_dir: Path,
    ) -> EvaluationResult:
        """Same parent_id lookup as ModelTrainer for the provided manifest (val split).
        Writes to output_dir:
          evaluation_predictions.txt — tab-separated lines: '{reference}\\t{hypothesis}'
          metrics.json               — {"wer": <float>}"""
        ...

    def package_test_samples(
        self,
        manifest: Manifest[AudioSample],
        model_path: Path,
        vocab: VocabResult,
        spectrogram_manifest: Manifest[SampleSpectrogram],
        token_manifest: Manifest[SampleTokens],
        spectrogram_dir: Path,
        token_dir: Path,
        audio_dir: Path,   # MicrophoneNoiseAugmentor output dir; used to locate WAV files for zip
        output_dir: Path,
    ) -> Path:
        """Runs the same prediction loop as evaluate() (implemented via shared private logic),
        then writes test_samples.zip to output_dir containing audio files for samples the
        model predicted correctly (hypothesis == reference). These become known-good fixtures
        for app unit/E2E tests. Implemented as a separate public method; both evaluate() and
        package_test_samples() call private _run_predictions(...) to avoid duplication.
        Returns the zip path (conventions.test_samples_path(output_dir))."""
        ...

@dataclass
class EvaluationResult:
    wer: float
    predictions: list[tuple[str, str]]
```

---

### Data Flow

```
01_input_phrases.csv (phrase, command columns)
        │
        │ PhraseVariator(rng=Random(42)) → TextSample variants (with sanity_check)
        │ subsample_rate filter applied
        ▼
Manifest[TextSample]
        │
        ├──► VocabComputer → VocabResult
        │                    (labels extracted from TextSample.label; phoneme_list.txt +
        │                     words_to_phonemes.json written; intentional change from surface_form)
        │
        ▼
TtsSampleGenerator(ModifierStage[TextSample, AudioSample])
  Manifest[AudioSample] (voice from sorted en-US list; speech_rate as int)
        │
        ▼
DelayAugmentor(ModifierStage[AudioSample, AudioSample])
  Manifest[AudioSample] (prefix_delay_s, suffix_delay_s)
        │
        ▼
BackgroundNoiseAugmentor(ModifierStage[AudioSample, AudioSample])
  Manifest[AudioSample] (noise_file, noise_start_s, noise_volume)
        │
        ▼
MicrophoneNoiseAugmentor(ModifierStage[AudioSample, AudioSample])
  Manifest[AudioSample] (mic_noise_amplitude; fully augmented)
        │
        ├──► SpectrogramStage (_is_deterministic=True)
        │      Manifest[SampleSpectrogram] (parent_id + parent_content_hash → AudioSample)
        │
        ├──► TokenStage (_is_deterministic=True) ◄── VocabResult
        │      Manifest[SampleTokens] (parent_id + parent_content_hash → AudioSample)
        │
        └──► SetManifestSplitter (splits augmented audio by individual sample)
               train.json / val.json / test.json  (Manifest[AudioSample])
               (intentional change: existing pipeline split clean audio only)

All three outputs are DVC-parallel (no interdependence)
        │
        ├──► ModelTrainer ◄── train manifest + spectrogram/token manifests + VocabResult
        │      speech_to_text_model.keras (KerasBackend.save called by ModelTrainer)
        │
        ├──► ModelEvaluator.evaluate() ◄── val manifest + spectrogram/token manifests + VocabResult
        │      evaluation_predictions.txt, metrics.json ({"wer": <float>})   [stage 11]
        │
        └──► ModelEvaluator.package_test_samples() ◄── test manifest + same inputs
               test_samples.zip (known-good audio fixtures for app E2E tests)            [stage 12]
```

**Skip-unchanged detection across chained AudioSample→AudioSample stages:** each `AudioSample` stores `parent_content_hash` (the content_hash of the audio it was derived from). `transform()` builds a `{output.parent_content_hash: output}` index. For each input audio sample, it looks up `input.content_hash` in that index — a match means this stage previously processed this exact input. It then re-derives applied_values using the stored seed and checks whether the content_hash would change; only regenerates if constraints changed.

---

### Testing Approach

**Unit tests** (pytest, `ml/test/pipeline/`):

- `PhraseVariator`: determinism with fixed seed; variation types produced; sanity_check filters malformed variants.
- `VariationGenerator`: same seed → same value; stability across range widening; `ValueError` after 1000 iters; `choose` is direct (no loop); range=0 → returns min_val immediately.
- `ModifierStage`: unchanged samples preserved intact (step 2b skip path); constraint change → regenerate with stored seed (step 2b regen path); new samples get fresh seed (step 2c); GC removes orphaned files; `_is_deterministic=True` → output_seed=0.
- `TtsSampleGenerator`: applied_values has `voice` (str) and `speech_rate` (int); rate string formatted in `_generate_output`.
- `BackgroundNoiseAugmentor`: noise_file always stored even when noise not applied; noise_start_s and noise_volume are 0.0 when not applied.
- Async mocks use `asyncio.Event` and `asyncio.Future`.

**E2E CI test** (`ml/test/e2e_pipeline_test.py`, pytest):

```python
ml_root = Path(__file__).parent.parent           # ml/
train_output_dir = ml_root / "data" / "speech_10_train_model"
eval_output_dir  = ml_root / "data" / "speech_11_evaluate_model"

def test_full_pipeline_ci():
    subprocess.run([
        "dvc", "repro",
        "--set-param", "pipeline.input_phrases_path=test/fixtures/ci_phrases.csv",
        "--set-param", "pipeline.epochs=1",
        "--set-param", "pipeline.subsample_rate=100",
    ], check=True, cwd=ml_root)

    assert conventions.model_path(train_output_dir).exists()
    metrics = json.loads(conventions.evaluation_metrics_path(eval_output_dir).read_text())
    assert math.isfinite(metrics["wer"])  # any finite WER ok; guards against NaN/inf only
```

Edge_tts is called live; CI runners must have internet access. The test is marked
`@pytest.mark.e2e` and excluded from the default `pytest` run via `pyproject.toml`
(`addopts = "-m 'not e2e'"`); CI invokes it explicitly with `pytest -m e2e`.

## Open Questions

_(None — all questions resolved during spec review.)_

## Tasks

### [ADR-221](https://jodasoft.atlassian.net/browse/ADR-221) Task 1: Core data model

Implement the `Sample` hierarchy and `Manifest[S]` / `ManifestStore` in `ml/pipeline/core/`.

- [ ] `sample.py`: `Sample`, `SampleWithPath`, `TextSample`, `AudioSample`, `SampleSpectrogram`, `SampleTokens` dataclasses with all fields from the spec; `TextSample.id = uuid4()`; `SampleWithPath.id` derived per `_derive_id()` contract
- [ ] `manifest.py`: `Manifest[S]` with `samples`, `by_content_hash()`, `by_id()`; `ManifestStore.read()` (type registry) and `write()`; JSON schema version 1
- [ ] Unit tests: round-trip serialisation for all four sample types; `ManifestStore.read()` selects the correct class from `sample_type`; `TextSample` serialises `seed: 0`; `SampleSpectrogram` serialises `parent_id`
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-222](https://jodasoft.atlassian.net/browse/ADR-222) Task 2a: Randomisation engine — PassFilter and VariationGenerator

Implement `ml/pipeline/core/randomization.py`.

- [ ] `PassFilter` ABC with `density()` and `sample_domain()`
- [ ] `MinMaxFilter`: uniform over `[min_val, max_val]`; `density()` = 1.0 in range, 0.0 outside
- [ ] `NormalFilter`: Gaussian; `density(x) = gaussian_pdf(x)/gaussian_pdf(mean)`; raises `ValueError` for `std_dev <= 0`
- [ ] `VariationGenerator`: `should_vary`, `generate` (rejection-sample with attempt indexing), `generate_int` (bitmask + attempt indexing; range=0 → return `min_val` immediately), `choose` (direct, no loop) — all hash formulas from spec
- [ ] Unit tests: same seed → same value for all methods; stability across range widening; `generate` raises `ValueError` after 1000 iterations; `choose` is direct (no rejection loop); `generate_int` range=0; `NormalFilter` rejects `std_dev <= 0`; `should_vary` probability converges over many seeds; change constraints (make max higher and lower) with a value that changes and a value that doesn't change (find a seed that exhibits each behavior, one that gets higher or lower when max changes, another that stays in the lower range when max changes)
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-223](https://jodasoft.atlassian.net/browse/ADR-223) Task 2b: Randomisation engine — ModifierStage

Implement `ml/pipeline/core/modifier_stage.py`. Depends on Task 2a.

- [ ] `ModifierStage[T_in, T_out]`: `transform()` three-case algorithm (skip / regen-with-stored-seed / new sample); `_derive_id()`, `_get_applied_values()`, `_generate_output()` abstract; `_compute_content_hash()` static; `_is_deterministic` class var
- [ ] `transform()` step 2b: re-derives applied values with stored seed; computes expected hash; skips if unchanged; calls `_derive_id(input, new_applied)` for regen (old file GC'd)
- [ ] `transform()` step 2c: calls `_derive_id(input, new_applied)` for new samples (no `uuid4()`)
- [ ] GC: deletes files in `output_dir` not in `{sample.path.name for sample in output_samples}` and not named `manifest.json`
- [ ] Unit tests: skip path preserves output file and id unchanged; constraint change → new id, same seed, updated content_hash, old file GC'd; new sample → `_derive_id` called, fresh seed; `_is_deterministic=True` → `output_seed=0`; GC removes orphaned files; find seeds where the same constraint change causes a change in one sample but not another
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-224](https://jodasoft.atlassian.net/browse/ADR-224) Task 3: Intent stages

Implement `ml/pipeline/intent/` and the first two DVC entry-points.

**⚠️ Must be implemented before Task 9 runs** — `01_generate_phrases.py` is the reference for `_create_variation()` and `sanity_check()`. Read it in full before the scripts directory is deleted.

- [ ] `phrase_variator.py`: `PhraseVariator` — port `_create_variation()` and `sanity_check()` from `ml/scripts/intent_prediction/01_generate_phrases.py`; replace every `random.*` call with `self.rng.*`; `generate(base_phrases, variations_per_phrase)` signature
- [ ] `vocab_computer.py`: `VocabComputer`, `VocabResult`; extracts from `TextSample.label`; writes `phoneme_list.txt` and `words_to_phonemes.json`; no digit substitution; `ctc_blank_idx = len(phoneme_list)`
- [ ] `stages/conventions.py`: all functions from the spec's Interfaces section
- [ ] `stages/intent_01_generate_phrases.py` and `intent_02_compute_vocab.py` entry-points
- [ ] Unit tests: `PhraseVariator` determinism with fixed seed; output identical to original `VariationGenerator` for same inputs; `sanity_check` filters malformed variants; `VocabComputer` produces correct phoneme list from label words
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-225](https://jodasoft.atlassian.net/browse/ADR-225) Task 4: TTS stage

Implement `ml/pipeline/speech/tts_stage.py` and `stages/speech_03_generate_samples.py`.

- [ ] `TtsProvider` protocol; edge_tts implementation (retries internal to implementation)
- [ ] `TtsSampleGenerator(ModifierStage[TextSample, AudioSample])`: `_derive_id` = `f"{input.label}_{voice_short}_r{rate+100}"`; synthesizes `input_sample.content`; stores `transcript = input_sample.label`; `applied_values = {"voice": str, "speech_rate": int}`; rate string formatted in `_generate_output`, not stored
- [ ] Voice list fetched in entry-point; sorted for determinism; filtered per spec
- [ ] Unit tests: `applied_values` keys and types; skip path; rate string formatted correctly; `TtsProvider` called with `input_sample.content`; `AudioSample.transcript` = `input_sample.label`; derived id format
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-226](https://jodasoft.atlassian.net/browse/ADR-226) Task 5a: Audio I/O and delay augmentation

Implement `ml/pipeline/io/` and `delay_stage.py`.

- [ ] `io/audio_io.py`: `AudioReader` protocol (returns mono float32 array; converts stereo internally), `AudioWriter` protocol; default implementations using librosa/soundfile
- [ ] `delay_stage.py`: `DelayAugmentor`; `_derive_id` = `f"{input.id}_pre{int(prefix*1000)}_suf{int(suffix*1000)}"`; both keys always in `applied_values` (0.0 when not applied); independent `should_vary` checks; float `params.yaml` keys per spec
- [ ] `stages/speech_04_add_delays.py` entry-point
- [ ] Unit tests: both keys always present; 0.0 stored correctly; `AudioReader` converts stereo input to mono; derived id format
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-227](https://jodasoft.atlassian.net/browse/ADR-227) Task 5b: Background noise and microphone noise augmentation

Implement `background_noise_stage.py` and `mic_noise_stage.py`. Depends on Task 5a (AudioReader).

- [ ] `background_noise_stage.py`: `BackgroundNoiseAugmentor`; `NoiseProvider` protocol; `choose()` always called (noise_file always stored); `noise_start_s` bounds derived at runtime from durations; `noise_start_s` and `noise_volume` = 0.0 when not applied; all three keys always present; `_derive_id` = `f"{input.id}_{noise_filestem}_v{int(volume*100)}"`
- [ ] `mic_noise_stage.py`: `MicrophoneNoiseAugmentor`; `_derive_id` = `f"{input.id}_mic{int(amplitude*1000)}"`
- [ ] `stages/speech_05_add_background_noise.py` and `speech_06_add_mic_noise.py`
- [ ] Unit tests: `BackgroundNoiseAugmentor` stores noise_file even when not applied; noise_start_s clamped to 0.0 when noise shorter than audio; all keys always present; derived id formats
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-228](https://jodasoft.atlassian.net/browse/ADR-228) Task 6: Featurisation and set splitting

Implement `SpectrogramStage`, `TokenStage`, `SetManifestSplitter` and their entry-points.

- [ ] `spectrogram_stage.py`: `SpectrogramStage(_is_deterministic=True)`; `_derive_id` returns `input_sample.id`; writes `{id}.npy` of shape `(n_mels, time_steps)`; `SampleSpectrogram.parent_id = input_sample.id`
- [ ] `token_stage.py`: `TokenStage(_is_deterministic=True)`; `_derive_id` returns `input_sample.id`; writes `{id}.json` with `{"phonemes": [...], "tokens": [...]}` padded to `input_token_length`; `SampleTokens.parent_id = input_sample.id`
- [ ] `set_splitter.py`: `SetManifestSplitter`; shuffles full augmented manifest; writes `train.json`, `val.json`, `test.json`; preserves `AudioSample.id` values unchanged
- [ ] `stages/speech_07_compute_tokens.py`, `speech_08_compute_spectrograms.py`, `speech_09_create_set_manifests.py`
- [ ] Unit tests: `SpectrogramStage` and `TokenStage` skip-unchanged paths; `parent_id` set correctly; `SetManifestSplitter` percentages sum correctly; ids unchanged in split outputs
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-229](https://jodasoft.atlassian.net/browse/ADR-229) Task 7: Model training

Implement `ModelTrainer` and `stages/speech_10_train_model.py`.

- [ ] `KerasBackend` protocol + default implementation: `build_ctc_model`, `train` (using `model.fit()` with CTC loss), `predict`, `save`, `load`
- [ ] `ModelTrainer.train()`: filters spectrogram/token manifests by `parent_id ∈ {s.id for s in train_manifest}`; constructs `tf.data.Dataset` with batching/prefetching; calls `KerasBackend.train` then `save`; returns model path
- [ ] `speech_10_train_model.py` entry-point
- [ ] Unit tests: filters correctly by `parent_id`; `KerasBackend.train` called with correct dataset; `KerasBackend.save` called after training
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-230](https://jodasoft.atlassian.net/browse/ADR-230) Task 8: Model evaluation and test packaging

Implement `ModelEvaluator` and the final two entry-points.

- [ ] `ModelEvaluator._run_predictions()`: shared private logic; same `parent_id` filter as `ModelTrainer`
- [ ] `ModelEvaluator.evaluate()`: calls `_run_predictions()`; writes `evaluation_predictions.txt` (tab-separated reference/hypothesis) and `metrics.json`
- [ ] `ModelEvaluator.package_test_samples()`: calls `_run_predictions()`; filters to hypothesis == reference; zips matching WAV files from `audio_dir`; writes `test_samples.zip`
- [ ] `stages/speech_11_evaluate_model.py` and `speech_12_package_test_samples.py`
- [ ] Unit tests: `evaluate()` writes correct files; `package_test_samples()` includes only correctly-predicted samples; both methods call `_run_predictions()` (shared, not duplicated)
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-231](https://jodasoft.atlassian.net/browse/ADR-231) Task 9: DVC wiring and old script cleanup

Wire all stages in `dvc.yaml`; migrate `params.yaml`; delete old scripts.

**⚠️ Script deletion must happen after Task 3 is merged** — the old `ml/scripts/` tree is the reference for `PhraseVariator`. Delete it only once Task 3's implementation is reviewed and merged.

- [ ] Write `ml/dvc.yaml` from scratch: all 12 stages wired with correct `cmd`, `deps`, `outs`, `params`; `persist: true` on all `ModifierStage` output dirs
- [ ] Write `ml/params.yaml`: all `pipeline.*` keys (including `input_phrases_path`, `variations_per_phrase`, `subsample_rate`, `n_mels`, `time_steps`, `input_token_length`, `epochs`, `batch_size`) and all `stages.*` variation-constraint keys
- [ ] Retain `download_phoneme_dictionary.py` as a non-OOP stage
- [ ] Delete `ml/scripts/` tree (after Task 3 is merged)
- [ ] `dvc repro` runs end-to-end without errors on the dev machine
- [ ] `validate-build` and `validate-tests` pass

---

### [ADR-232](https://jodasoft.atlassian.net/browse/ADR-232) Task 10: E2E CI test

Add the full-pipeline integration test and pytest configuration. The test is plain Python pytest — no BDD framework. The Given/When/Then structure is written as a docstring in the test function for clarity, not as a Gherkin framework construct.

- [ ] `ml/test/e2e_pipeline_test.py` with `@pytest.mark.e2e`; test function docstring documents the Given/When/Then scenario
- [ ] `ml/test/fixtures/ci_phrases.csv` with 10 canonical phrases
- [ ] `pyproject.toml` configured: `addopts = "-m 'not e2e'"` excludes from default runs; CI calls `pytest -m e2e` explicitly
- [ ] Assertions: `conventions.model_path(train_output_dir).exists()`; `metrics["wer"]` is finite
- [ ] `validate-build` and `validate-tests` pass (unit tests only; E2E requires live internet)

## Related Docs

- [`ml/_doc_ml.md`](./ml/_doc_ml.md) — current pipeline architecture and stage descriptions
- [`src/_doc_Projects.md`](./src/_doc_Projects.md) — project boundaries
