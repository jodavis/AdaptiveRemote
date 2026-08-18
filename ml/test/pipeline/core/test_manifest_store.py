"""Unit tests for `ManifestStore`.

Per `ml/_spec_OopPipeline.md`'s Component Breakdown, `ManifestStore` is Testable: JSON
round-trip persistence (schema v1) with type-dispatch serialise/deserialise, raising
`ValueError` on an empty manifest or mixed sample types within one manifest -- both called out
in the spec as "load-bearing correctness checks, not incidental validation."
"""

from __future__ import annotations

import json
from dataclasses import replace
from pathlib import Path

import pytest

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample, Sample, TextSample

_DEFAULT_AUDIO_SAMPLE = AudioSample(
    name="TV_ON_r1",
    content_hash="hash-audio",
    path="speech_01_generate_samples/TV_ON_r1.wav",
    parent_name="parent-name",
    parent_content_hash="parent-hash",
    applied_values={"voice": "Jenny", "speech_rate": 1.0},
    transcript="turn on the tv",
    label="TV_ON",
    seed=42,
)


def _build_audio_sample(
    *,
    parent_name: str = _DEFAULT_AUDIO_SAMPLE.parent_name,
    parent_content_hash: str = _DEFAULT_AUDIO_SAMPLE.parent_content_hash,
) -> AudioSample:
    """Default `AudioSample` fixture, overridable for the lineage fields tests vary."""
    return replace(
        _DEFAULT_AUDIO_SAMPLE, parent_name=parent_name, parent_content_hash=parent_content_hash
    )


class TestManifestStore:
    def test_ManifestStore_Write_EmptyManifest_RaisesValueError(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest: Manifest[Sample] = Manifest([])

        with pytest.raises(ValueError):
            store.write(manifest, tmp_path / "manifest.json")

    def test_ManifestStore_Write_MixedSampleTypes_RaisesValueError(self, tmp_path: Path) -> None:
        store = ManifestStore()
        text_sample = TextSample(content="turn on the tv", label="TV_ON")
        audio_sample = _build_audio_sample(
            parent_name=text_sample.name, parent_content_hash=text_sample.content_hash
        )
        manifest: Manifest[Sample] = Manifest([text_sample, audio_sample])

        with pytest.raises(ValueError):
            store.write(manifest, tmp_path / "manifest.json")

    def test_ManifestStore_Write_ValidManifest_WritesSchemaVersion1(self, tmp_path: Path) -> None:
        store = ManifestStore()
        text_sample = TextSample(content="turn on the tv", label="TV_ON")
        manifest: Manifest[TextSample] = Manifest([text_sample])
        path = tmp_path / "manifest.json"

        store.write(manifest, path)

        document = json.loads(path.read_text())
        assert document["schema_version"] == 1

    def test_ManifestStore_Write_ValidManifest_WritesSampleTypeName(self, tmp_path: Path) -> None:
        store = ManifestStore()
        text_sample = TextSample(content="turn on the tv", label="TV_ON")
        manifest: Manifest[TextSample] = Manifest([text_sample])
        path = tmp_path / "manifest.json"

        store.write(manifest, path)

        document = json.loads(path.read_text())
        assert document["sample_type"] == "TextSample"

    def test_ManifestStore_WriteThenRead_TextSampleManifest_RoundTripsEquivalentSample(
        self, tmp_path: Path
    ) -> None:
        store = ManifestStore()
        text_sample = TextSample(content="turn on the tv", label="TV_ON")
        manifest: Manifest[TextSample] = Manifest([text_sample])
        path = tmp_path / "manifest.json"
        store.write(manifest, path)

        result = store.read(path)

        assert result.by_name(text_sample.name) == text_sample

    def test_ManifestStore_WriteThenRead_AudioSampleManifest_RoundTripsEquivalentSample(
        self, tmp_path: Path
    ) -> None:
        store = ManifestStore()
        audio_sample = _build_audio_sample()
        manifest: Manifest[AudioSample] = Manifest([audio_sample])
        path = tmp_path / "manifest.json"
        store.write(manifest, path)

        result = store.read(path)

        assert result.by_name("TV_ON_r1") == audio_sample

    def test_ManifestStore_WriteThenRead_MultipleSamples_RoundTripsEverySample(
        self, tmp_path: Path
    ) -> None:
        store = ManifestStore()
        first = TextSample(content="turn on the tv", label="TV_ON")
        second = TextSample(content="turn off the tv", label="TV_OFF")
        manifest: Manifest[TextSample] = Manifest([first, second])
        path = tmp_path / "manifest.json"
        store.write(manifest, path)

        result = store.read(path)

        assert len(result) == 2
        assert result.by_name(first.name) == first
        assert result.by_name(second.name) == second

    def test_ManifestStore_Read_UnknownSampleType_RaisesKeyError(self, tmp_path: Path) -> None:
        store = ManifestStore()
        path = tmp_path / "manifest.json"
        path.write_text(
            json.dumps({"schema_version": 1, "sample_type": "NotARealSampleType", "samples": []})
        )

        with pytest.raises(KeyError):
            store.read(path)
