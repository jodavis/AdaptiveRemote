"""Unit tests for SetManifestSplitter."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample
from pipeline.speech.set_splitter import SetManifestSplitter
from pipeline.stages import conventions


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(sample_id: str, transcript: str = "TV_ON") -> AudioSample:
    content = f"{sample_id}:audio"
    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=content_hash,
        path=Path(f"{sample_id}.wav"),
        parent_content_hash="parent_hash",
        transcript=transcript,
        applied_values={},
    )


def _make_manifest(n: int) -> Manifest[AudioSample]:
    return Manifest([_make_audio_sample(f"sample_{i:04d}") for i in range(n)])


def _make_splitter(
    output_dir: Path,
    *,
    train_pct: int = 80,
    val_pct: int = 10,
    test_pct: int = 10,
) -> SetManifestSplitter:
    return SetManifestSplitter(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        train_pct=train_pct,
        val_pct=val_pct,
        test_pct=test_pct,
    )


# ---------------------------------------------------------------------------
# TestSplit
# ---------------------------------------------------------------------------


class TestSplit:
    def test_writes_train_manifest(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path)
        manifest = _make_manifest(10)
        splitter.split(manifest)
        assert conventions.split_manifest_path(tmp_path, "train").exists()

    def test_writes_val_manifest(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path)
        manifest = _make_manifest(10)
        splitter.split(manifest)
        assert conventions.split_manifest_path(tmp_path, "val").exists()

    def test_writes_test_manifest(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path)
        manifest = _make_manifest(10)
        splitter.split(manifest)
        assert conventions.split_manifest_path(tmp_path, "test").exists()

    def test_total_samples_equals_input(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path, train_pct=70, val_pct=20, test_pct=10)
        manifest = _make_manifest(100)
        splitter.split(manifest)
        store = ManifestStore()
        train = store.read(conventions.split_manifest_path(tmp_path, "train"))
        val = store.read(conventions.split_manifest_path(tmp_path, "val"))
        test = store.read(conventions.split_manifest_path(tmp_path, "test"))
        total = len(train.samples) + len(val.samples) + len(test.samples)
        assert total == 100

    def test_sample_ids_preserved(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path)
        manifest = _make_manifest(10)
        original_ids = {s.id for s in manifest.samples}
        splitter.split(manifest)
        store = ManifestStore()
        all_ids: set[str] = set()
        for split in ["train", "val", "test"]:
            m = store.read(conventions.split_manifest_path(tmp_path, split))
            for s in m.samples:
                all_ids.add(s.id)
        assert all_ids == original_ids

    def test_no_duplicate_samples_across_splits(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path)
        manifest = _make_manifest(20)
        splitter.split(manifest)
        store = ManifestStore()
        all_ids: list[str] = []
        for split in ["train", "val", "test"]:
            m = store.read(conventions.split_manifest_path(tmp_path, split))
            all_ids.extend(s.id for s in m.samples)
        assert len(all_ids) == len(set(all_ids))

    def test_train_set_largest(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path, train_pct=80, val_pct=10, test_pct=10)
        manifest = _make_manifest(100)
        splitter.split(manifest)
        store = ManifestStore()
        train = store.read(conventions.split_manifest_path(tmp_path, "train"))
        val = store.read(conventions.split_manifest_path(tmp_path, "val"))
        test = store.read(conventions.split_manifest_path(tmp_path, "test"))
        assert len(train.samples) > len(val.samples)
        assert len(train.samples) > len(test.samples)

    def test_percentages_must_sum_to_100(self, tmp_path: Path) -> None:
        import pytest
        with pytest.raises(ValueError, match="100"):
            SetManifestSplitter(
                output_dir=tmp_path,
                manifest_store=ManifestStore(),
                train_pct=70,
                val_pct=20,
                test_pct=5,
            )

    def test_shuffles_with_seed_42(self, tmp_path: Path) -> None:
        """Two separate runs produce the same split (seed=42 is fixed)."""
        manifest = _make_manifest(30)
        store = ManifestStore()

        out1 = tmp_path / "run1"
        out1.mkdir()
        _make_splitter(out1).split(manifest)
        train1 = [s.id for s in store.read(conventions.split_manifest_path(out1, "train")).samples]

        out2 = tmp_path / "run2"
        out2.mkdir()
        _make_splitter(out2).split(manifest)
        train2 = [s.id for s in store.read(conventions.split_manifest_path(out2, "train")).samples]

        assert train1 == train2

    def test_split_sizes_proportional_to_percentages(self, tmp_path: Path) -> None:
        splitter = _make_splitter(tmp_path, train_pct=60, val_pct=20, test_pct=20)
        manifest = _make_manifest(100)
        splitter.split(manifest)
        store = ManifestStore()
        train = store.read(conventions.split_manifest_path(tmp_path, "train"))
        val = store.read(conventions.split_manifest_path(tmp_path, "val"))
        test = store.read(conventions.split_manifest_path(tmp_path, "test"))
        assert len(train.samples) == 60
        assert len(val.samples) == 20
        assert len(test.samples) == 20
