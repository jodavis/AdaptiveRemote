from __future__ import annotations

import json
import tempfile
from pathlib import Path

import pytest

from pipeline.core.sample import (
    AudioSample,
    SampleSpectrogram,
    SampleTokens,
    TextSample,
)
from pipeline.core.manifest import Manifest, ManifestStore


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _text_sample() -> TextSample:
    return TextSample(
        id="abc123",
        seed=0,
        content_hash="deadbeef",
        content="okay turn on the tv",
        label="TV_ON",
    )


def _audio_sample() -> AudioSample:
    return AudioSample(
        id="TV_ON_Jenny_r77",
        seed=67890,
        content_hash="audiohash",
        path=Path("TV_ON_Jenny_r77.wav"),
        parent_content_hash="texthash",
        transcript="TV_ON",
        applied_values={"voice": "en-US-JennyNeural", "speech_rate": 5},
    )


def _spectrogram_sample() -> SampleSpectrogram:
    return SampleSpectrogram(
        id="TV_ON_Jenny_r77",
        seed=0,
        content_hash="spechash",
        path=Path("TV_ON_Jenny_r77.npy"),
        parent_content_hash="audiohash",
        transcript="TV_ON",
        parent_id="audio-uuid-123",
    )


def _tokens_sample() -> SampleTokens:
    return SampleTokens(
        id="TV_ON_Jenny_r77",
        seed=0,
        content_hash="tokenshash",
        path=Path("TV_ON_Jenny_r77.json"),
        parent_content_hash="audiohash",
        transcript="TV_ON",
        parent_id="audio-uuid-123",
    )


# ---------------------------------------------------------------------------
# TextSample round-trip
# ---------------------------------------------------------------------------

class TestTextSampleRoundTrip:
    def test_round_trip_preserves_all_fields(self, tmp_path: Path) -> None:
        sample = _text_sample()
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([sample]), manifest_path)

        result = store.read(manifest_path)

        assert len(result.samples) == 1
        s = result.samples[0]
        assert isinstance(s, TextSample)
        assert s.id == "abc123"
        assert s.seed == 0
        assert s.content_hash == "deadbeef"
        assert s.content == "okay turn on the tv"
        assert s.label == "TV_ON"

    def test_seed_serialised_as_zero(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_text_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert raw["samples"][0]["seed"] == 0

    def test_json_omits_path_parent_content_hash_applied_values(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_text_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())
        entry = raw["samples"][0]

        assert "path" not in entry
        assert "parent_content_hash" not in entry
        assert "applied_values" not in entry

    def test_json_schema_version_is_one(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_text_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert raw["version"] == 1
        assert raw["sample_type"] == "text"


# ---------------------------------------------------------------------------
# AudioSample round-trip
# ---------------------------------------------------------------------------

class TestAudioSampleRoundTrip:
    def test_round_trip_preserves_all_fields(self, tmp_path: Path) -> None:
        sample = _audio_sample()
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([sample]), manifest_path)

        result = store.read(manifest_path)

        assert len(result.samples) == 1
        s = result.samples[0]
        assert isinstance(s, AudioSample)
        assert s.id == "TV_ON_Jenny_r77"
        assert s.seed == 67890
        assert s.content_hash == "audiohash"
        assert s.path == Path("TV_ON_Jenny_r77.wav")
        assert s.parent_content_hash == "texthash"
        assert s.transcript == "TV_ON"
        assert s.applied_values == {"voice": "en-US-JennyNeural", "speech_rate": 5}

    def test_applied_values_int_type_preserved(self, tmp_path: Path) -> None:
        sample = AudioSample(
            id="id",
            seed=1,
            content_hash="h",
            path=Path("id.wav"),
            parent_content_hash="ph",
            transcript="TV_ON",
            applied_values={"speech_rate": 5},
        )
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([sample]), manifest_path)

        result = store.read(manifest_path)
        s = result.samples[0]

        assert isinstance(s.applied_values["speech_rate"], int)

    def test_applied_values_float_type_preserved(self, tmp_path: Path) -> None:
        sample = AudioSample(
            id="id",
            seed=1,
            content_hash="h",
            path=Path("id.wav"),
            parent_content_hash="ph",
            transcript="TV_ON",
            applied_values={"noise_volume": 0.45},
        )
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([sample]), manifest_path)

        result = store.read(manifest_path)
        s = result.samples[0]

        assert isinstance(s.applied_values["noise_volume"], float)

    def test_path_stored_as_bare_filename(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_audio_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())
        # Must be filename only, no directory component
        assert raw["samples"][0]["path"] == "TV_ON_Jenny_r77.wav"


# ---------------------------------------------------------------------------
# SampleSpectrogram round-trip
# ---------------------------------------------------------------------------

class TestSampleSpectrogramRoundTrip:
    def test_round_trip_preserves_all_fields(self, tmp_path: Path) -> None:
        sample = _spectrogram_sample()
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([sample]), manifest_path)

        result = store.read(manifest_path)

        assert len(result.samples) == 1
        s = result.samples[0]
        assert isinstance(s, SampleSpectrogram)
        assert s.id == "TV_ON_Jenny_r77"
        assert s.seed == 0
        assert s.content_hash == "spechash"
        assert s.path == Path("TV_ON_Jenny_r77.npy")
        assert s.parent_content_hash == "audiohash"
        assert s.transcript == "TV_ON"
        assert s.parent_id == "audio-uuid-123"

    def test_parent_id_serialised_in_json(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_spectrogram_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert raw["samples"][0]["parent_id"] == "audio-uuid-123"

    def test_applied_values_omitted_from_json(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_spectrogram_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert "applied_values" not in raw["samples"][0]

    def test_sample_type_is_spectrogram(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_spectrogram_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert raw["sample_type"] == "spectrogram"


# ---------------------------------------------------------------------------
# SampleTokens round-trip
# ---------------------------------------------------------------------------

class TestSampleTokensRoundTrip:
    def test_round_trip_preserves_all_fields(self, tmp_path: Path) -> None:
        sample = _tokens_sample()
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([sample]), manifest_path)

        result = store.read(manifest_path)

        assert len(result.samples) == 1
        s = result.samples[0]
        assert isinstance(s, SampleTokens)
        assert s.id == "TV_ON_Jenny_r77"
        assert s.seed == 0
        assert s.content_hash == "tokenshash"
        assert s.path == Path("TV_ON_Jenny_r77.json")
        assert s.parent_content_hash == "audiohash"
        assert s.transcript == "TV_ON"
        assert s.parent_id == "audio-uuid-123"

    def test_sample_type_is_tokens(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_tokens_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert raw["sample_type"] == "tokens"

    def test_applied_values_omitted_from_json(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_tokens_sample()]), manifest_path)

        raw = json.loads(manifest_path.read_text())

        assert "applied_values" not in raw["samples"][0]


# ---------------------------------------------------------------------------
# ManifestStore.read() dispatch by sample_type
# ---------------------------------------------------------------------------

class TestManifestStoreReadDispatch:
    def test_reads_text_sample_type(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_text_sample()]), manifest_path)

        result = store.read(manifest_path)

        assert all(isinstance(s, TextSample) for s in result.samples)

    def test_reads_audio_sample_type(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_audio_sample()]), manifest_path)

        result = store.read(manifest_path)

        assert all(isinstance(s, AudioSample) for s in result.samples)

    def test_reads_spectrogram_sample_type(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_spectrogram_sample()]), manifest_path)

        result = store.read(manifest_path)

        assert all(isinstance(s, SampleSpectrogram) for s in result.samples)

    def test_reads_tokens_sample_type(self, tmp_path: Path) -> None:
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([_tokens_sample()]), manifest_path)

        result = store.read(manifest_path)

        assert all(isinstance(s, SampleTokens) for s in result.samples)

    def test_reads_multiple_samples(self, tmp_path: Path) -> None:
        s1 = _audio_sample()
        s2 = AudioSample(
            id="TV_OFF_Jenny_r80",
            seed=11111,
            content_hash="hash2",
            path=Path("TV_OFF_Jenny_r80.wav"),
            parent_content_hash="texthash2",
            transcript="TV_OFF",
            applied_values={"voice": "en-US-JennyNeural", "speech_rate": -20},
        )
        store = ManifestStore()
        manifest_path = tmp_path / "manifest.json"
        store.write(Manifest([s1, s2]), manifest_path)

        result = store.read(manifest_path)

        assert len(result.samples) == 2


# ---------------------------------------------------------------------------
# Manifest lookup methods
# ---------------------------------------------------------------------------

class TestManifestLookup:
    def test_by_content_hash_returns_matching_sample(self) -> None:
        sample = _text_sample()
        manifest = Manifest([sample])

        result = manifest.by_content_hash("deadbeef")

        assert result is sample

    def test_by_content_hash_returns_none_when_not_found(self) -> None:
        manifest = Manifest([_text_sample()])

        result = manifest.by_content_hash("notfound")

        assert result is None

    def test_by_id_returns_matching_sample(self) -> None:
        sample = _text_sample()
        manifest = Manifest([sample])

        result = manifest.by_id("abc123")

        assert result is sample

    def test_by_id_returns_none_when_not_found(self) -> None:
        manifest = Manifest([_text_sample()])

        result = manifest.by_id("notfound")

        assert result is None

    def test_samples_property_returns_tuple(self) -> None:
        manifest = Manifest([_text_sample()])

        assert isinstance(manifest.samples, tuple)

    def test_empty_manifest(self) -> None:
        manifest = Manifest([])

        assert manifest.samples == ()
        assert manifest.by_id("x") is None
        assert manifest.by_content_hash("x") is None
