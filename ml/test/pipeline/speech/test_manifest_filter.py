"""Unit tests for lookup_sample_triplets in manifest_filter.py."""

from __future__ import annotations

import hashlib
import logging
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest
from pipeline.core.sample import AudioSample, SampleSpectrogram, SampleTokens
from pipeline.speech.manifest_filter import lookup_sample_triplets


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _audio(sample_id: str) -> AudioSample:
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=hashlib.sha256(f"{sample_id}:a".encode()).hexdigest(),
        path=Path(f"{sample_id}.wav"),
        parent_content_hash="ph",
        transcript="T",
        applied_values={},
    )


def _spec(parent_id: str) -> SampleSpectrogram:
    return SampleSpectrogram(
        id=parent_id,
        seed=0,
        content_hash=hashlib.sha256(f"{parent_id}:s".encode()).hexdigest(),
        path=Path(f"{parent_id}.npy"),
        parent_content_hash="ph",
        transcript="T",
        parent_id=parent_id,
    )


def _tok(parent_id: str) -> SampleTokens:
    return SampleTokens(
        id=parent_id,
        seed=0,
        content_hash=hashlib.sha256(f"{parent_id}:t".encode()).hexdigest(),
        path=Path(f"{parent_id}.json"),
        parent_content_hash="ph",
        transcript="T",
        parent_id=parent_id,
    )


# ---------------------------------------------------------------------------
# TestAllSamplesMatched
# ---------------------------------------------------------------------------


class TestAllSamplesMatched:
    """When every split sample has a matching spec and token, all triples are returned."""

    def test_single_sample_returns_one_triple(self) -> None:
        split = Manifest([_audio("a")])
        specs = Manifest([_spec("a")])
        toks = Manifest([_tok("a")])

        result = lookup_sample_triplets(split, specs, toks)

        assert len(result) == 1
        audio, spec, tok = result[0]
        assert audio.id == "a"
        assert spec.parent_id == "a"
        assert tok.parent_id == "a"

    def test_multiple_samples_returns_all_triples(self) -> None:
        split = Manifest([_audio("a"), _audio("b"), _audio("c")])
        specs = Manifest([_spec("a"), _spec("b"), _spec("c")])
        toks = Manifest([_tok("a"), _tok("b"), _tok("c")])

        result = lookup_sample_triplets(split, specs, toks)

        ids = {r[0].id for r in result}
        assert ids == {"a", "b", "c"}

    def test_triple_components_match_by_parent_id(self) -> None:
        split = Manifest([_audio("x")])
        specs = Manifest([_spec("x")])
        toks = Manifest([_tok("x")])

        result = lookup_sample_triplets(split, specs, toks)

        audio, spec, tok = result[0]
        assert audio is split.by_id("x")
        assert spec is specs.by_id("x")
        assert tok is toks.by_id("x")


# ---------------------------------------------------------------------------
# TestEmptySplitManifest
# ---------------------------------------------------------------------------


class TestEmptySplitManifest:
    """Empty split manifest produces an empty result."""

    def test_empty_split_returns_empty_list(self) -> None:
        split: Manifest[AudioSample] = Manifest([])
        specs = Manifest([_spec("a")])
        toks = Manifest([_tok("a")])

        result = lookup_sample_triplets(split, specs, toks)

        assert result == []


# ---------------------------------------------------------------------------
# TestExtraManifestEntries
# ---------------------------------------------------------------------------


class TestExtraManifestEntries:
    """Spectrogram/token entries not in split_manifest are excluded from the result."""

    def test_extra_spec_entries_excluded(self) -> None:
        split = Manifest([_audio("train")])
        specs = Manifest([_spec("train"), _spec("val"), _spec("test")])
        toks = Manifest([_tok("train")])

        result = lookup_sample_triplets(split, specs, toks)

        assert len(result) == 1
        assert result[0][0].id == "train"

    def test_extra_token_entries_excluded(self) -> None:
        split = Manifest([_audio("train")])
        specs = Manifest([_spec("train")])
        toks = Manifest([_tok("train"), _tok("val"), _tok("test")])

        result = lookup_sample_triplets(split, specs, toks)

        assert len(result) == 1
        assert result[0][0].id == "train"


# ---------------------------------------------------------------------------
# TestMissingSpectrogram
# ---------------------------------------------------------------------------


class TestMissingSpectrogram:
    """Samples missing a spectrogram entry are skipped with a warning."""

    def test_missing_spec_sample_skipped(self) -> None:
        split = Manifest([_audio("a"), _audio("b")])
        specs = Manifest([_spec("a")])  # "b" has no spectrogram
        toks = Manifest([_tok("a"), _tok("b")])

        result = lookup_sample_triplets(split, specs, toks)

        assert len(result) == 1
        assert result[0][0].id == "a"

    def test_missing_spec_logs_warning(self, caplog: pytest.LogCaptureFixture) -> None:
        split = Manifest([_audio("missing_spec")])
        specs: Manifest[SampleSpectrogram] = Manifest([])
        toks = Manifest([_tok("missing_spec")])

        with caplog.at_level(logging.WARNING, logger="pipeline.speech.manifest_filter"):
            lookup_sample_triplets(split, specs, toks)

        assert any("missing_spec" in r.message for r in caplog.records)


# ---------------------------------------------------------------------------
# TestMissingToken
# ---------------------------------------------------------------------------


class TestMissingToken:
    """Samples missing a token entry are skipped with a warning."""

    def test_missing_token_sample_skipped(self) -> None:
        split = Manifest([_audio("a"), _audio("b")])
        specs = Manifest([_spec("a"), _spec("b")])
        toks = Manifest([_tok("a")])  # "b" has no token

        result = lookup_sample_triplets(split, specs, toks)

        assert len(result) == 1
        assert result[0][0].id == "a"

    def test_missing_token_logs_warning(self, caplog: pytest.LogCaptureFixture) -> None:
        split = Manifest([_audio("missing_tok")])
        specs = Manifest([_spec("missing_tok")])
        toks: Manifest[SampleTokens] = Manifest([])

        with caplog.at_level(logging.WARNING, logger="pipeline.speech.manifest_filter"):
            lookup_sample_triplets(split, specs, toks)

        assert any("missing_tok" in r.message for r in caplog.records)


# ---------------------------------------------------------------------------
# TestMissingBoth
# ---------------------------------------------------------------------------


class TestMissingBoth:
    """Samples missing both spec and token are skipped."""

    def test_missing_both_returns_empty(self) -> None:
        split = Manifest([_audio("lonely")])
        specs: Manifest[SampleSpectrogram] = Manifest([])
        toks: Manifest[SampleTokens] = Manifest([])

        result = lookup_sample_triplets(split, specs, toks)

        assert result == []


# ---------------------------------------------------------------------------
# TestPartialMatches
# ---------------------------------------------------------------------------


class TestPartialMatches:
    """Mixed present/absent entries: only fully matched samples appear in the result."""

    def test_partial_matches_returns_only_complete(self) -> None:
        split = Manifest([_audio("full"), _audio("no_spec"), _audio("no_tok")])
        specs = Manifest([_spec("full"), _spec("no_tok")])
        toks = Manifest([_tok("full"), _tok("no_spec")])

        result = lookup_sample_triplets(split, specs, toks)

        assert len(result) == 1
        assert result[0][0].id == "full"
