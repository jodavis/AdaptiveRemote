from __future__ import annotations

from pathlib import Path

import pytest

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample, Sample, TextSample


def test_write_then_read_single_text_sample_returns_equivalent_manifest(tmp_path: Path) -> None:
    # Arrange
    sample = TextSample(content_hash="hash_1", content="Turn on the TV", label="power_on")
    manifest = Manifest[TextSample]([sample])
    store = ManifestStore()
    path = tmp_path / "manifest.json"

    # Act
    store.write(manifest, path)
    result = store.read(path, TextSample)

    # Assert
    assert list(result) == [sample]


def test_write_empty_manifest_raises_value_error(tmp_path: Path) -> None:
    # Arrange
    manifest = Manifest[TextSample]([])
    store = ManifestStore()
    path = tmp_path / "manifest.json"

    # Act
    with pytest.raises(ValueError):
        store.write(manifest, path)


def test_write_mixed_sample_types_raises_value_error(tmp_path: Path) -> None:
    # Arrange
    text_sample = TextSample(content_hash="hash_1", content="Turn on the TV", label="power_on")
    audio_sample = AudioSample(
        name="sample_1",
        content_hash="hash_2",
        path="audio/sample_1.wav",
        transcript="Turn on the TV",
        label="power_on",
        seed=42,
    )
    manifest: Manifest[Sample] = Manifest([text_sample, audio_sample])
    store = ManifestStore()
    path = tmp_path / "manifest.json"

    # Act
    with pytest.raises(ValueError):
        store.write(manifest, path)
