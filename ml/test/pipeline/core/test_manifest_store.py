from __future__ import annotations

from pathlib import Path

import pytest

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample, Sample, SampleSpectrogram, SampleTokens, TextSample


def test_write_then_read_single_text_sample_returns_equivalent_manifest(tmp_path: Path) -> None:
    # Arrange
    sample = TextSample(content_hash="hash_1", content="Turn on the TV", label="power_on")
    manifest = Manifest[TextSample]([sample])
    store = ManifestStore()
    path = tmp_path / "manifest.json"

    # Act
    store.write(manifest, path)
    result = store.read(path)

    # Assert
    assert list(result) == [sample]


def test_write_then_read_audio_sample_manifest_dispatches_to_audio_sample_type(tmp_path: Path) -> None:
    # Arrange
    sample = AudioSample(
        name="sample_1",
        content_hash="hash_2",
        path="audio/sample_1.wav",
        transcript="Turn on the TV",
        label="power_on",
        seed=42,
    )
    manifest = Manifest[AudioSample]([sample])
    store = ManifestStore()
    path = tmp_path / "manifest.json"

    # Act
    store.write(manifest, path)
    result = store.read(path)

    # Assert
    (result_sample,) = list(result)
    assert isinstance(result_sample, AudioSample)
    assert result_sample == sample


@pytest.mark.parametrize("sample_type", [SampleSpectrogram, SampleTokens])
def test_write_then_read_path_only_sample_dispatches_to_matching_type(
    tmp_path: Path, sample_type: type[SampleSpectrogram] | type[SampleTokens]
) -> None:
    # Arrange
    sample = sample_type(name="sample_1", content_hash="hash_2", path="derived/sample_1.bin")
    manifest = Manifest([sample])
    store = ManifestStore()
    path = tmp_path / "manifest.json"

    # Act
    store.write(manifest, path)
    result = store.read(path)

    # Assert
    (result_sample,) = list(result)
    assert isinstance(result_sample, sample_type)
    assert result_sample == sample


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
