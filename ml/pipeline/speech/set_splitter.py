"""SetManifestSplitter: split an augmented manifest into train/val/test sets.

Shuffles with seed=42 for reproducibility. Percentages must sum to 100.
Writes train_manifest.json, val_manifest.json, test_manifest.json via
conventions.split_manifest_path.
"""

from __future__ import annotations

import random
from pathlib import Path

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample
from pipeline.stages import conventions


class SetManifestSplitter:
    """Split a fully-augmented manifest into train/val/test sets.

    Shuffles the samples with seed=42 then writes three manifest files.
    train_pct + val_pct + test_pct must equal 100.
    """

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        train_pct: int,
        val_pct: int,
        test_pct: int,
    ) -> None:
        if train_pct + val_pct + test_pct != 100:
            raise ValueError(
                f"train_pct + val_pct + test_pct must equal 100, "
                f"got {train_pct + val_pct + test_pct}"
            )
        self._output_dir = output_dir
        self._manifest_store = manifest_store
        self._train_pct = train_pct
        self._val_pct = val_pct
        self._test_pct = test_pct

    def split(self, manifest: Manifest[AudioSample]) -> None:
        """Shuffle and write train/val/test manifest files."""
        samples = list(manifest.samples)
        rng = random.Random(42)
        rng.shuffle(samples)

        n = len(samples)
        n_train = int(n * self._train_pct / 100)
        n_val = int(n * self._val_pct / 100)
        # test gets the remainder to ensure total == n
        n_test = n - n_train - n_val

        train_samples = samples[:n_train]
        val_samples = samples[n_train : n_train + n_val]
        test_samples = samples[n_train + n_val :]

        self._output_dir.mkdir(parents=True, exist_ok=True)

        self._manifest_store.write(
            Manifest(train_samples),
            conventions.split_manifest_path(self._output_dir, "train"),
        )
        self._manifest_store.write(
            Manifest(val_samples),
            conventions.split_manifest_path(self._output_dir, "val"),
        )
        self._manifest_store.write(
            Manifest(test_samples),
            conventions.split_manifest_path(self._output_dir, "test"),
        )
