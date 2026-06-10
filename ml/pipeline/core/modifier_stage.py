from __future__ import annotations

import hashlib
import json
import os
from abc import ABC, abstractmethod
from pathlib import Path
from typing import Any, ClassVar, Generic, TypeVar

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import Sample, SampleWithPath

T_in = TypeVar("T_in", bound=Sample)
T_out = TypeVar("T_out", bound=SampleWithPath)


class ModifierStage(ABC, Generic[T_in, T_out]):
    """Abstract base for all per-sample file-transformation stages.

    Subclasses implement _get_applied_values, _generate_output, and _derive_id.
    transform() drives the three-case skip/regen/new algorithm, GC, and manifest write.
    """

    _is_deterministic: ClassVar[bool] = False

    def __init__(self, output_dir: Path, manifest_store: ManifestStore) -> None:
        self._output_dir = output_dir
        self._manifest_store = manifest_store

    async def transform(
        self,
        input_manifest: Manifest[T_in],
        manifest_path: Path,
    ) -> Manifest[T_out]:
        """Run the three-case algorithm over input_manifest; write output manifest.

        Step 1: Read previous manifest (if present) and build parent_content_hash index.
        Step 2: For each input sample, skip / regen / generate-new.
        Step 3: GC — delete output_dir files not in the new output set and not manifest.json.
        Step 4: Write output manifest to manifest_path.
        """
        # Step 1: build previous-output index keyed on parent_content_hash
        prev_by_parent: dict[str, T_out] = {}
        if manifest_path.exists():
            prev = self._manifest_store.read(manifest_path)
            prev_by_parent = {
                out.parent_content_hash: out for out in prev.samples
            }

        # Step 2: process each input sample
        output_samples: list[T_out] = []
        for input_sample in input_manifest.samples:
            prev_out = prev_by_parent.get(input_sample.content_hash)

            if prev_out is not None:
                # 2b: previous output exists for this input — check if constraints changed
                new_applied = self._get_applied_values(
                    input_sample, VariationGenerator(prev_out.seed)
                )
                expected_hash = self._compute_content_hash(
                    input_sample.content_hash, prev_out.seed, new_applied
                )
                if expected_hash == prev_out.content_hash:
                    # Skip: file and id unchanged
                    output_samples.append(prev_out)
                else:
                    # Regen: constraints changed; preserve seed; derive new id
                    new_id = self._derive_id(input_sample, new_applied)
                    result = await self._generate_output(
                        input_sample,
                        output_id=new_id,
                        output_seed=prev_out.seed,
                        applied_values=new_applied,
                        parent_content_hash=input_sample.content_hash,
                    )
                    output_samples.append(result)
            else:
                # 2c: new sample — assign fresh seed and derive id
                if self._is_deterministic:
                    output_seed = 0
                else:
                    output_seed = int.from_bytes(os.urandom(8), "big")
                generator = VariationGenerator(output_seed)
                new_applied = self._get_applied_values(input_sample, generator)
                output_id = self._derive_id(input_sample, new_applied)
                result = await self._generate_output(
                    input_sample,
                    output_id=output_id,
                    output_seed=output_seed,
                    applied_values=new_applied,
                    parent_content_hash=input_sample.content_hash,
                )
                output_samples.append(result)

        # Step 3: GC — flat glob; delete files not in the new output set
        kept_names = {sample.path.name for sample in output_samples}
        if self._output_dir.exists():
            for file in self._output_dir.glob("*"):
                if file.is_file() and file.name != "manifest.json" and file.name not in kept_names:
                    file.unlink()

        # Step 4: write output manifest
        output_manifest: Manifest[T_out] = Manifest(output_samples)
        self._manifest_store.write(output_manifest, manifest_path)

        return output_manifest

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
        """Generate output file; return complete output Sample with all fields set.
        MUST compute content_hash via _compute_content_hash."""
        ...

    @abstractmethod
    def _derive_id(self, input_sample: T_in, applied_values: dict[str, Any]) -> str:
        """Return the output sample id (= filename stem).
        Called for both new samples and regens with changed constraints."""
        ...

    @staticmethod
    def _compute_content_hash(
        parent_content_hash: str, output_seed: int, applied_values: dict[str, Any]
    ) -> str:
        """Single source of truth for the content_hash formula.
        All _generate_output implementations MUST call this method.
        """
        canonical = json.dumps(
            applied_values, sort_keys=True, separators=(",", ":"), ensure_ascii=True
        )
        raw = f"{parent_content_hash}:{output_seed}:{canonical}"
        return hashlib.sha256(raw.encode("utf-8")).hexdigest()
