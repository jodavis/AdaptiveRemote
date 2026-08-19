"""`ModifierStage[T_in, T_out]`/`RandomizedModifierStage[T_in, T_out]`: the shared per-sample
skip/regenerate/GC algorithm every pipeline stage that transforms files one-by-one inherits from.

Per `ml/_spec_OopPipeline.md`'s "ModifierStage for all per-sample file transformations" decision,
`ModifierStage` is the seedless base -- skip-unchanged (matched by `parent_content_hash`) and GC
only, used directly by the two deterministic featurisation stages (`SpectrogramStage`,
`TokenStage`). `RandomizedModifierStage` layers seed generation/storage and a three-case
skip/regen-with-stored-seed/new-sample algorithm on top, for the four stages that vary samples via
`VariationGenerator`. See "Content hash determines sample identity" and "Previous output manifest
as the seed store" for the exact formulas and case tables this module implements.

Two abstract hooks are needed per level -- computing the values that would be applied if a sample
were (re)generated (cheap, no I/O, called for every input sample so the base class can decide
skip vs. regenerate) and actually generating the output (expensive I/O, only called when
regeneration is needed). `RandomizedModifierStage` cannot simply *override* `ModifierStage`'s
`_compute_applied_values`/`_generate_output` with a wider (seed-carrying) signature -- an
overriding method that adds a required parameter is a Liskov-substitution violation `mypy
--strict` rejects (`[override]`), and widening the base signature to carry an always-`None` seed
parameter would leak seed-related plumbing into the seedless stages, which the spec explicitly
forbids ("must never be forced to carry seed-related fields/logic"). Instead,
`RandomizedModifierStage` gives its own concrete (non-abstract) implementations of the inherited
hooks that simply raise `NotImplementedError`, and declares its own distinctly-named abstract
hooks (`_compute_randomized_applied_values`/`_generate_randomized_output`) that carry the extra
`seed`/`variation_generator` parameters its own subclasses need. The same reasoning applies to
`_compute_content_hash`: the base's own implementation never takes a `seed` parameter at all;
`RandomizedModifierStage._compute_seeded_content_hash` is a separate, distinctly-named method
(delegating to the base one) rather than an override.
"""

from __future__ import annotations

import hashlib
import json
import os
from abc import ABC, abstractmethod
from pathlib import Path
from typing import Any, Generic, TypeVar

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, Sample, SampleWithPath

T_in = TypeVar("T_in", bound=Sample)
T_out = TypeVar("T_out", bound=SampleWithPath)
T_out_seeded = TypeVar("T_out_seeded", bound=AudioSample)


class ModifierStage(ABC, Generic[T_in, T_out]):
    """Seedless base: two-case skip/regenerate algorithm keyed on `parent_content_hash`, plus GC
    scoped to `self._output_dir.glob("*")` only -- never resolved through a `data_root`, since
    GC's job is to clean up this stage's own persisted output directory, not to reason about
    where a passthrough sample's bytes actually live."""

    def __init__(self, output_dir: Path, manifest_store: ManifestStore) -> None:
        self._output_dir = output_dir
        self._manifest_store = manifest_store

    @abstractmethod
    def _compute_applied_values(self, input_sample: T_in) -> dict[str, Any]:
        """Cheap, I/O-free computation of the values that would be applied if `input_sample`
        were (re)generated right now. Called for every input sample -- including ones that end
        up skipped -- so it must never perform file or network I/O; that belongs in
        `_generate_output`."""
        ...

    def _compute_content_hash(
        self, parent_content_hash: str, applied_values: dict[str, Any]
    ) -> str:
        """`content_hash = sha256(parent_content_hash + ":" + canonical(applied_values))`, per
        the spec's "Content hash determines sample identity" decision. `canonical` sorts keys and
        uses compact, ASCII-only separators so hash stability never depends on dict insertion
        order or `json.dumps`'s default formatting."""
        canonical = json.dumps(
            applied_values, sort_keys=True, separators=(",", ":"), ensure_ascii=True
        )
        return hashlib.sha256(f"{parent_content_hash}:{canonical}".encode("utf-8")).hexdigest()

    @abstractmethod
    async def _generate_output(
        self, input_sample: T_in, applied_values: dict[str, Any], content_hash: str
    ) -> T_out:
        """Expensive I/O to actually produce a new output sample. Only called for samples that
        are new or whose recomputed `content_hash` no longer matches the previous run's output.
        The override must set `parent_name=input_sample.name`,
        `parent_content_hash=input_sample.content_hash`, `applied_values=applied_values`, and
        `content_hash=content_hash` on the returned object exactly as given -- it owns deriving
        the sample's own `name` and `path` (and any subtype-specific fields)."""
        ...

    async def transform(self, input_manifest: Manifest[T_in]) -> Manifest[T_out]:
        """Per `ml/_spec_OopPipeline.md`'s "Previous output manifest as the seed store" decision:
        for each input sample, look up a previous output indexed by `parent_content_hash`. Skip
        (reuse the previous output unchanged) when one is found and recomputing its content hash
        with this run's current applied values still matches; otherwise (re)generate."""
        previous_by_parent_hash = self._read_previous_by_parent_hash()

        outputs: list[T_out] = []
        for input_sample in input_manifest:
            applied_values = self._compute_applied_values(input_sample)
            content_hash = self._compute_content_hash(input_sample.content_hash, applied_values)
            previous_output = previous_by_parent_hash.get(input_sample.content_hash)

            if previous_output is not None and previous_output.content_hash == content_hash:
                outputs.append(previous_output)
            else:
                outputs.append(
                    await self._generate_output(input_sample, applied_values, content_hash)
                )

        output_manifest = Manifest(outputs)
        self._garbage_collect(output_manifest)
        self._manifest_store.write(output_manifest, self._output_dir / "manifest.json")
        return output_manifest

    def _read_previous_by_parent_hash(self) -> dict[str, T_out]:
        previous_manifest_path = self._output_dir / "manifest.json"
        if not previous_manifest_path.exists():
            return {}

        previous_manifest = self._manifest_store.read(previous_manifest_path)
        return {sample.parent_content_hash: sample for sample in previous_manifest}

    def _garbage_collect(self, output_manifest: Manifest[T_out]) -> None:
        """Delete anything in `self._output_dir` that isn't `manifest.json` and isn't referenced
        (by basename) by `output_manifest` -- never touches any other directory, even when a
        passthrough sample's `path` points at an ancestor stage's directory, since the glob is
        always scoped to `self._output_dir` itself."""
        expected_names = {Path(sample.path).name for sample in output_manifest}
        for existing_path in self._output_dir.glob("*"):
            if existing_path.name == "manifest.json":
                continue
            if existing_path.name not in expected_names:
                existing_path.unlink()


class RandomizedModifierStage(ModifierStage[T_in, T_out_seeded]):
    """Adds seed storage and a three-case skip/regen-with-stored-seed/new-sample algorithm on top
    of `ModifierStage`. `T_out` is narrowed to `AudioSample` (rather than the wider
    `SampleWithPath` `ModifierStage` accepts) because every current and planned
    `RandomizedModifierStage` subclass produces `AudioSample`, and seed storage genuinely needs
    the `.seed` field -- narrowing here avoids an unsound cast under `mypy --strict` rather than
    leaving `T_out` unbound and casting in every subclass."""

    def _compute_applied_values(self, input_sample: T_in) -> dict[str, Any]:
        raise NotImplementedError(
            "RandomizedModifierStage subclasses implement _compute_randomized_applied_values, "
            "not _compute_applied_values"
        )

    @abstractmethod
    def _compute_randomized_applied_values(
        self, input_sample: T_in, variation_generator: VariationGenerator
    ) -> dict[str, Any]:
        """Cheap, I/O-free computation of the values that would be applied if `input_sample`
        were (re)generated with `variation_generator` right now. Called for every input sample,
        with a `VariationGenerator` built from either the stored seed (a previous output was
        found) or a freshly-drawn one (no previous output) -- see `transform()`."""
        ...

    def _compute_seeded_content_hash(
        self, parent_content_hash: str, applied_values: dict[str, Any], seed: int
    ) -> str:
        """`content_hash = sha256(parent_content_hash + ":" + str(seed) + ":" +
        canonical(applied_values))`, per the spec's seed-extended formula. Delegates to the
        base's own `_compute_content_hash` by folding `seed` into the parent-hash term, so the
        formula is defined in exactly one place."""
        return self._compute_content_hash(f"{parent_content_hash}:{seed}", applied_values)

    async def _generate_output(
        self, input_sample: T_in, applied_values: dict[str, Any], content_hash: str
    ) -> T_out_seeded:
        raise NotImplementedError(
            "RandomizedModifierStage subclasses implement _generate_randomized_output, "
            "not _generate_output"
        )

    @abstractmethod
    async def _generate_randomized_output(
        self,
        input_sample: T_in,
        applied_values: dict[str, Any],
        content_hash: str,
        seed: int,
    ) -> T_out_seeded:
        """Expensive I/O to actually produce a new output sample, using `seed` (stored or fresh,
        per `transform()`'s case). Same field-population contract as `ModifierStage
        ._generate_output`, plus setting `seed=seed` on the returned object."""
        ...

    def _draw_seed(self) -> int:
        """Fresh seed for a genuinely new sample, per the spec's "Seed-based randomisation with
        pass filters" decision. A separate method (rather than an inline `os.urandom` call) so
        tests can substitute a deterministic value via `monkeypatch.setattr`, consistent with
        `VariationGenerator`'s own test doubles in `test_randomization.py`."""
        return int.from_bytes(os.urandom(8), "big")

    async def transform(self, input_manifest: Manifest[T_in]) -> Manifest[T_out_seeded]:
        """Per `ml/_spec_OopPipeline.md`'s "Previous output manifest as the seed store" decision:
        three cases per input sample -- skip (stored seed reproduces the same content hash),
        regenerate with the stored seed (constraints changed), or new sample (no previous output
        at all, so a fresh seed is drawn)."""
        previous_by_parent_hash = self._read_previous_by_parent_hash()

        outputs: list[T_out_seeded] = []
        for input_sample in input_manifest:
            previous_output = previous_by_parent_hash.get(input_sample.content_hash)
            seed = previous_output.seed if previous_output is not None else self._draw_seed()
            variation_generator = VariationGenerator(seed)
            applied_values = self._compute_randomized_applied_values(
                input_sample, variation_generator
            )
            content_hash = self._compute_seeded_content_hash(
                input_sample.content_hash, applied_values, seed
            )

            if previous_output is not None and previous_output.content_hash == content_hash:
                outputs.append(previous_output)
            else:
                outputs.append(
                    await self._generate_randomized_output(
                        input_sample, applied_values, content_hash, seed
                    )
                )

        output_manifest = Manifest(outputs)
        self._garbage_collect(output_manifest)
        self._manifest_store.write(output_manifest, self._output_dir / "manifest.json")
        return output_manifest
