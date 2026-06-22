"""Unit tests for SetManifestSplitter."""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path

import pytest

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


def _make_manifest(count: int, prefix: str = "sample") -> Manifest[AudioSample]:
    samples = [_make_audio_sample(f"{prefix}_{i:03d}") for i in range(count)]
    return Manifest(samples)


def _make_splitter(seed: int = 42) -> SetManifestSplitter:
    return SetManifestSplitter(seed=seed)


# ---------------------------------------------------------------------------
# TestValidation
# ---------------------------------------------------------------------------


class TestValidation:
    def test_percentages_not_summing_to_100_raises_value_error(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        with pytest.raises(ValueError):
            splitter.split(manifest, train_pct=50, val_pct=20, test_pct=20, output_dir=tmp_path)

    def test_percentages_summing_to_100_does_not_raise(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        # Should not raise
        splitter.split(manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path)

    def test_all_zeros_raises_value_error(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        with pytest.raises(ValueError):
            splitter.split(manifest, train_pct=0, val_pct=0, test_pct=0, output_dir=tmp_path)

    def test_sum_of_101_raises_value_error(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        with pytest.raises(ValueError):
            splitter.split(manifest, train_pct=70, val_pct=20, test_pct=11, output_dir=tmp_path)


# ---------------------------------------------------------------------------
# TestSplitCounts
# ---------------------------------------------------------------------------


class TestSplitCounts:
    def test_total_count_equals_input_count(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(100)
        train, val, test = splitter.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path
        )
        assert len(train.samples) + len(val.samples) + len(test.samples) == 100

    def test_split_counts_match_percentages(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(100)
        train, val, test = splitter.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path
        )
        assert len(train.samples) == 70
        assert len(val.samples) == 20
        assert len(test.samples) == 10

    def test_split_with_different_percentages(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(100)
        train, val, test = splitter.split(
            manifest, train_pct=80, val_pct=10, test_pct=10, output_dir=tmp_path
        )
        assert len(train.samples) == 80
        assert len(val.samples) == 10
        assert len(test.samples) == 10

    def test_split_small_manifest(self, tmp_path: Path) -> None:
        """With 10 samples at 70/20/10, counts are 7/2/1."""
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        train, val, test = splitter.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path
        )
        assert len(train.samples) + len(val.samples) + len(test.samples) == 10


# ---------------------------------------------------------------------------
# TestIdPreservation
# ---------------------------------------------------------------------------


class TestIdPreservation:
    def test_all_input_ids_appear_in_exactly_one_split(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(100)
        input_ids = {s.id for s in manifest.samples}
        train, val, test = splitter.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path
        )
        train_ids = {s.id for s in train.samples}
        val_ids = {s.id for s in val.samples}
        test_ids = {s.id for s in test.samples}

        # No duplicates across splits
        assert len(train_ids & val_ids) == 0
        assert len(train_ids & test_ids) == 0
        assert len(val_ids & test_ids) == 0

        # All input IDs accounted for
        assert train_ids | val_ids | test_ids == input_ids

    def test_sample_ids_unchanged_in_splits(self, tmp_path: Path) -> None:
        """Sample id values are preserved from input — not reassigned."""
        splitter = _make_splitter()
        manifest = _make_manifest(30)
        input_ids = {s.id for s in manifest.samples}
        train, val, test = splitter.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path
        )
        all_output_ids = (
            {s.id for s in train.samples}
            | {s.id for s in val.samples}
            | {s.id for s in test.samples}
        )
        assert all_output_ids == input_ids


# ---------------------------------------------------------------------------
# TestOutputFiles
# ---------------------------------------------------------------------------


class TestOutputFiles:
    def test_writes_train_manifest_json(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        splitter.split(manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path)
        assert (tmp_path / "train_manifest.json").exists()

    def test_writes_val_manifest_json(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        splitter.split(manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path)
        assert (tmp_path / "val_manifest.json").exists()

    def test_writes_test_manifest_json(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        splitter.split(manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path)
        assert (tmp_path / "test_manifest.json").exists()

    def test_output_files_use_conventions_split_manifest_path(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        splitter.split(manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path)
        # Confirm these match what conventions.split_manifest_path returns
        assert (conventions.split_manifest_path(tmp_path, "train")).exists()
        assert (conventions.split_manifest_path(tmp_path, "val")).exists()
        assert (conventions.split_manifest_path(tmp_path, "test")).exists()

    def test_split_returns_manifest_tuple(self, tmp_path: Path) -> None:
        splitter = _make_splitter()
        manifest = _make_manifest(10)
        result = splitter.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path
        )
        assert isinstance(result, tuple)
        assert len(result) == 3


# ---------------------------------------------------------------------------
# TestDeterminism
# ---------------------------------------------------------------------------


class TestDeterminism:
    def test_same_seed_produces_same_splits(self, tmp_path: Path) -> None:
        """Two splitters with the same seed produce identical splits."""
        manifest = _make_manifest(50)

        splitter1 = SetManifestSplitter(seed=42)
        train1, val1, test1 = splitter1.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path / "run1"
        )

        splitter2 = SetManifestSplitter(seed=42)
        train2, val2, test2 = splitter2.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path / "run2"
        )

        assert [s.id for s in train1.samples] == [s.id for s in train2.samples]
        assert [s.id for s in val1.samples] == [s.id for s in val2.samples]
        assert [s.id for s in test1.samples] == [s.id for s in test2.samples]

    def test_different_seeds_produce_different_splits(self, tmp_path: Path) -> None:
        """Different seeds should produce different orderings."""
        manifest = _make_manifest(50)

        splitter1 = SetManifestSplitter(seed=42)
        train1, _, _ = splitter1.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path / "run1"
        )

        splitter2 = SetManifestSplitter(seed=99)
        train2, _, _ = splitter2.split(
            manifest, train_pct=70, val_pct=20, test_pct=10, output_dir=tmp_path / "run2"
        )

        # With 50 samples and different seeds, orderings almost certainly differ
        assert [s.id for s in train1.samples] != [s.id for s in train2.samples]
