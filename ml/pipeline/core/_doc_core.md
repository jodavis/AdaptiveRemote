# core Subpackage Architecture & Design

Summary: Describes the `Sample`/`Manifest`/`ManifestStore` data model, the seed-based
randomisation primitives, and the `ModifierStage`/`RandomizedModifierStage` skip/regenerate/GC
algorithm every per-sample pipeline stage builds on.

## Overview

The `core` subpackage is the foundation every other `ml/pipeline/` subpackage depends on. It has
no knowledge of any concrete stage (TTS, augmentation, featurisation) — it defines the sample
data model, its JSON persistence, the deterministic randomisation math, and the shared
skip/regenerate/GC algorithm that every future per-sample stage inherits rather than
reimplements.

- [`sample.py`](sample.py) — the `Sample` dataclass hierarchy (`Sample`, `SampleWithPath`,
  `TextSample`, `AudioSample`, `SampleSpectrogram`, `SampleTokens`).
- [`manifest.py`](manifest.py) — `Manifest[S]` (typed, indexed in-memory collection) and
  `ManifestStore` (its JSON schema-v1 round-trip persistence).
- [`randomization.py`](randomization.py) — `PassFilter`/`MinMaxFilter`/`NormalFilter`
  (precision-quantized rejection-sampling grids) and `VariationGenerator` (seed/name-hash-derived
  value generation).
- [`modifier_stage.py`](modifier_stage.py) — `ModifierStage[T_in, T_out]` (seedless
  skip/regenerate/GC base) and `RandomizedModifierStage[T_in, T_out]` (adds seed storage and a
  three-case algorithm on top).

## Responsibilities & Boundaries

- **Owns:** sample identity (`content_hash`/`name`), manifest persistence, the randomisation
  formulas, and the skip/regenerate/GC algorithm shared by every per-sample stage.
- **Does not own:** any actual file I/O beyond a stage's own `manifest.json` (audio reads/writes
  are `ml/pipeline/io`'s `AudioReader`/`AudioWriter` seam); any stage-specific logic (what values
  to vary, how to derive an output sample's name) — that belongs to each concrete
  `ModifierStage`/`RandomizedModifierStage` subclass built in later tasks.
- **Integrates with:** every `ml/pipeline/intent/` and `ml/pipeline/speech/` stage, all of which
  either read/write `Manifest[S]` directly or subclass `ModifierStage`/`RandomizedModifierStage`.

## Key Design Decisions

- **Content hash determines sample identity:** `content_hash` — not `name` — is the field every
  skip/regen comparison keys off. Base `ModifierStage`:
  `content_hash = sha256(parent_content_hash + ":" + canonical(applied_values))`.
  `RandomizedModifierStage` extends this with a seed term:
  `content_hash = sha256(parent_content_hash + ":" + str(seed) + ":" + canonical(applied_values))`.
  `canonical(applied_values) = json.dumps(applied_values, sort_keys=True, separators=(",", ":"),
  ensure_ascii=True)`, so hash stability never depends on dict insertion order.
- **Two distinctly-named hooks per stage level, not one overridden with a wider signature:**
  `ModifierStage` declares `_compute_applied_values`/`_generate_output` as its abstract contract.
  `RandomizedModifierStage` does not override these with an added `seed`/`variation_generator`
  parameter — an overriding method that *requires* an extra parameter is a Liskov-substitution
  violation `mypy --strict` rejects, and widening the base signature to carry an always-`None`
  seed would leak seed-related plumbing into the seedless stages (`SpectrogramStage`/
  `TokenStage`), which the spec forbids. Instead, `RandomizedModifierStage` gives the inherited
  hooks concrete stub implementations that raise `NotImplementedError`, and declares its own,
  distinctly-named abstract hooks (`_compute_randomized_applied_values`/
  `_generate_randomized_output`) that carry the extra parameters its own subclasses need. The
  same reasoning applies to content-hash computation: `_compute_content_hash` (base) and
  `_compute_seeded_content_hash` (randomized, delegates to the base one) are two separate methods,
  not one overridden with a wider signature.
- **`RandomizedModifierStage`'s `T_out` is narrowed to `AudioSample`:** `ModifierStage[T_in,
  T_out]` binds `T_out` to the wider `SampleWithPath` (all four current/planned direct
  `ModifierStage` subclasses' outputs need only `path`/`parent_name`/`parent_content_hash`/
  `applied_values`). `RandomizedModifierStage[T_in, T_out]` narrows its own `T_out` to
  `AudioSample` specifically, since every current and planned `RandomizedModifierStage` subclass
  produces `AudioSample` and seed storage genuinely needs the `.seed` field — this avoids an
  unsound cast under `mypy --strict` rather than leaving `T_out` unbound and casting in every
  subclass.
- **GC is always scoped to `self._output_dir.glob("*")`:** never resolved through a shared
  `data_root`. It matches by `Path(sample.path).name` (basename only) against files actually
  present in the stage's own output directory, so it can never enumerate or delete anything from
  a different stage's directory — even when a passthrough sample's `.path` points at an ancestor
  stage's directory. Covered by a dedicated regression test in
  [`test_modifier_stage.py`](../../../test/pipeline/core/test_modifier_stage.py).
- **Fresh seeds come from `os.urandom(8)`,** wrapped in a small `_draw_seed()` method rather than
  an inline call, so tests can substitute a deterministic value via `monkeypatch.setattr` —
  consistent with `VariationGenerator`'s own test-double conventions.
- **Precision-quantized rejection sampling:** `PassFilter` draws candidates from a finite,
  precision-sized grid rather than continuous float interpolation, so most values stay stable
  across small constraint changes — this is what makes `ModifierStage`/`RandomizedModifierStage`'s
  skip-unchanged detection actually fire in practice rather than only on a frozen constraint set.

## Key Classes / Interfaces

| Class | Responsibility |
|---|---|
| `Sample`/`SampleWithPath`/`TextSample`/`AudioSample`/`SampleSpectrogram`/`SampleTokens` | Plain sample dataclasses; `TextSample.__post_init__` sets `name = content_hash` |
| `Manifest[S]` | Typed, in-memory sample collection; `by_name`/`by_content_hash` lookups |
| `ManifestStore` | JSON schema-v1 round-trip persistence for `Manifest[S]` |
| `PassFilter`/`MinMaxFilter`/`NormalFilter` | Precision-quantized acceptance-domain math |
| `VariationGenerator` | Deterministic, seed/name-hash-derived value generation |
| `ModifierStage[T_in, T_out]` | Seedless two-case skip/regenerate + GC base |
| `RandomizedModifierStage[T_in, T_out]` | Adds seed storage + three-case skip/regen-with-stored-seed/new algorithm |

## Data Flow

`transform(input_manifest)` reads the stage's own previous `manifest.json` (if present, from
`self._output_dir`) and indexes it by `parent_content_hash`. For each input sample: compute
applied values (and, for `RandomizedModifierStage`, resolve a seed — stored if a previous output
was found, freshly drawn otherwise), recompute the content hash, and either reuse the previous
output unchanged (skip) or call the stage's own `_generate_output`/`_generate_randomized_output`
override to produce a new one. The resulting `Manifest[T_out]` then drives GC (deleting anything
in `self._output_dir` not referenced by the new sample set) before being written back out as the
stage's new `manifest.json` and returned.

## Testability

Every class in this subpackage is Testable-tier and covered test-first in
[`ml/test/pipeline/core/`](../../../test/pipeline/core/), mirroring this directory's layout
(`test_sample.py`, `test_manifest.py`, `test_manifest_store.py`, `test_randomization.py`,
`test_modifier_stage.py`). Since no concrete `ModifierStage`/`RandomizedModifierStage` subclass
exists yet, `test_modifier_stage.py` defines its own minimal concrete test doubles
(`_PassthroughModifierStage`/`_PassthroughRandomizedModifierStage`) to exercise `transform()`.

## Updating This Document

Update this document only when this subpackage's design or boundaries change (e.g. a new shared
hook is added to `ModifierStage`/`RandomizedModifierStage`, or the content-hash formula changes).
For implementation details, refer to the source files linked above and their inline comments.
