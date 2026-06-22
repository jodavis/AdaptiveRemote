"""SetManifestSplitter: split a fully-augmented manifest into train/val/test subsets.

Shuffles input AudioSample objects using the provided seed, then slices
by percentage. AudioSample.id values are preserved unchanged in each split.
Split manifests are written via ManifestStore using conventions.split_manifest_path.
"""

from __future__ import annotations

import random
from pathlib import Path

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample
from pipeline.stages import conventions


class SetManifestSplitter:
    """Splits a fully-augmented manifest into train, val, and test subsets.

    Percentages must sum to exactly 100. Samples are shuffled deterministically
    using the provided seed before slicing.

    Split manifests are written to:
      output_dir/train_manifest.json
      output_dir/val_manifest.json
      output_dir/test_manifest.json

    AudioSample.id values from the input are preserved unchanged.
    """

    def __init__(self, seed: int = 42) -> None:
        self._seed = seed

    def split(
        self,
        manifest: Manifest[AudioSample],
        train_pct: int,
        val_pct: int,
        test_pct: int,
        output_dir: Path,
    ) -> tuple[Manifest[AudioSample], Manifest[AudioSample], Manifest[AudioSample]]:
        """Shuffle and split manifest; write three manifest files; return split manifests.

        Raises ValueError if train_pct + val_pct + test_pct != 100.
        """
        if train_pct + val_pct + test_pct != 100:
            raise ValueError(
                f"Percentages must sum to 100, got "
                f"{train_pct} + {val_pct} + {test_pct} = {train_pct + val_pct + test_pct}"
            )

        samples = list(manifest.samples)
        rng = random.Random(self._seed)
        rng.shuffle(samples)

        n = len(samples)
        train_end = int(n * train_pct / 100)
        val_end = train_end + int(n * val_pct / 100)

        train_samples = samples[:train_end]
        val_samples = samples[train_end:val_end]
        test_samples = samples[val_end:]

        train_manifest: Manifest[AudioSample] = Manifest(train_samples)
        val_manifest: Manifest[AudioSample] = Manifest(val_samples)
        test_manifest: Manifest[AudioSample] = Manifest(test_samples)

        output_dir.mkdir(parents=True, exist_ok=True)
        store = ManifestStore()
        store.write(train_manifest, conventions.split_manifest_path(output_dir, "train"))
        store.write(val_manifest, conventions.split_manifest_path(output_dir, "val"))
        store.write(test_manifest, conventions.split_manifest_path(output_dir, "test"))

        return train_manifest, val_manifest, test_manifest
