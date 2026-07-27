from __future__ import annotations

import pytest

from pipeline.core.manifest import Manifest
from pipeline.core.sample import TextSample


def test_by_name_sample_present_returns_sample() -> None:
    # Arrange
    sample = TextSample(content_hash="hash_1", content="Turn on the TV")
    manifest = Manifest[TextSample]([sample])

    # Act
    result = manifest.by_name("hash_1")

    # Assert
    assert result == sample


def test_add_duplicate_name_raises_value_error() -> None:
    # Arrange
    sample1 = TextSample(content_hash="hash_1", content="Turn on the TV")
    sample2 = TextSample(content_hash="hash_1", content="Turn on the television")
    manifest = Manifest[TextSample]([sample1])

    # Act
    with pytest.raises(ValueError) as exc_info:
        manifest.add(sample2)

    # Assert
    assert "hash_1" in str(exc_info.value)


def test_by_content_hash_sample_present_returns_sample() -> None:
    # Arrange
    sample = TextSample(content_hash="hash_1", content="Turn on the TV")
    manifest = Manifest[TextSample]([sample])

    # Act
    result = manifest.by_content_hash("hash_1")

    # Assert
    assert result == sample


def test_by_name_name_missing_raises_key_error() -> None:
    # Arrange
    manifest = Manifest[TextSample]([])

    # Act
    with pytest.raises(KeyError):
        manifest.by_name("missing")


def test_by_content_hash_content_hash_missing_raises_key_error() -> None:
    # Arrange
    manifest = Manifest[TextSample]([])

    # Act
    with pytest.raises(KeyError):
        manifest.by_content_hash("missing")


def test_len_empty_manifest_returns_zero() -> None:
    # Arrange
    manifest = Manifest[TextSample]([])

    # Act
    result = len(manifest)

    # Assert
    assert result == 0


def test_len_multiple_samples_returns_sample_count() -> None:
    # Arrange
    sample1 = TextSample(content_hash="hash_1", content="Turn on the TV")
    sample2 = TextSample(content_hash="hash_2", content="Turn off the TV")
    manifest = Manifest[TextSample]([sample1, sample2])

    # Act
    result = len(manifest)

    # Assert
    assert result == 2


def test_iter_multiple_samples_yields_all_samples_in_insertion_order() -> None:
    # Arrange
    sample1 = TextSample(content_hash="hash_1", content="Turn on the TV")
    sample2 = TextSample(content_hash="hash_2", content="Turn off the TV")
    manifest = Manifest[TextSample]([sample1, sample2])

    # Act
    result = list(manifest)

    # Assert
    assert result == [sample1, sample2]
