"""Unit tests for `Manifest[S]`.

Per `ml/_spec_OopPipeline.md`'s Component Breakdown, `Manifest[S]` is Testable: a typed
collection with by_name/by_content_hash lookup that raises `ValueError` on duplicate sample
names -- called out in the spec as "a load-bearing correctness check, not incidental
validation," so it gets direct test coverage alongside the lookup methods.
"""

from __future__ import annotations

import pytest

from pipeline.core.manifest import Manifest
from pipeline.core.sample import Sample


class TestManifest:
    def test_Manifest_ByName_ReturnsMatchingSample(self) -> None:
        sample = Sample(name="a", content_hash="hash-a")
        manifest = Manifest([sample])

        result = manifest.by_name("a")

        assert result is sample

    def test_Manifest_ByContentHash_ReturnsMatchingSample(self) -> None:
        sample = Sample(name="a", content_hash="hash-a")
        manifest = Manifest([sample])

        result = manifest.by_content_hash("hash-a")

        assert result is sample

    def test_Manifest_Init_DuplicateName_RaisesValueError(self) -> None:
        first = Sample(name="a", content_hash="hash-a")
        second = Sample(name="a", content_hash="hash-b")

        with pytest.raises(ValueError):
            Manifest([first, second])

    def test_Manifest_ByName_MissingName_RaisesKeyError(self) -> None:
        manifest: Manifest[Sample] = Manifest([])

        with pytest.raises(KeyError):
            manifest.by_name("missing")

    def test_Manifest_ByContentHash_MissingContentHash_RaisesKeyError(self) -> None:
        manifest: Manifest[Sample] = Manifest([])

        with pytest.raises(KeyError):
            manifest.by_content_hash("missing")

    def test_Manifest_Init_EmptyIterable_ConstructsEmptyManifest(self) -> None:
        manifest: Manifest[Sample] = Manifest([])

        assert len(manifest) == 0

    def test_Manifest_Len_MultipleSamples_ReturnsSampleCount(self) -> None:
        first = Sample(name="a", content_hash="hash-a")
        second = Sample(name="b", content_hash="hash-b")

        manifest = Manifest([first, second])

        assert len(manifest) == 2

    def test_Manifest_Iter_MultipleSamples_YieldsEverySample(self) -> None:
        first = Sample(name="a", content_hash="hash-a")
        second = Sample(name="b", content_hash="hash-b")
        manifest = Manifest([first, second])

        result = list(manifest)

        assert result == [first, second]

    def test_Manifest_Samples_ReturnsAllSamplesAsAList(self) -> None:
        first = Sample(name="a", content_hash="hash-a")
        second = Sample(name="b", content_hash="hash-b")
        manifest = Manifest([first, second])

        result = manifest.samples

        assert result == [first, second]
