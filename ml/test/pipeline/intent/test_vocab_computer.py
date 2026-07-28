"""Unit tests for VocabComputer and VocabResult."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest
from pipeline.core.sample import TextSample
from pipeline.intent.vocab_computer import (
    PhonemeNotFoundError,
    VocabComputer,
    VocabResult,
)


def _make_text_sample(content: str, label: str) -> TextSample:
    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return TextSample(seed=0, content_hash=content_hash, content=content, label=label)


class _FakePhonemeProvider:
    """Simple stub returning pre-defined phoneme lists."""

    _DATA: dict[str, list[str]] = {
        "TURN": ["T", "ER", "N"],
        "ON": ["AO", "N"],
        "THE": ["DH", "AH"],
        "TV": ["T", "IY"],
        "VOLUME": ["V", "AH", "L", "Y", "UH", "M"],
        "UP": ["AH", "P"],
    }

    def lookup(self, word: str) -> list[str]:
        result = self._DATA.get(word.upper())
        if result is None:
            raise PhonemeNotFoundError(f"Not found: {word}")
        return result


class TestVocabComputer:
    def test_phoneme_list_covers_expected_phonemes(self, tmp_path: Path) -> None:
        samples = [
            _make_text_sample("turn on the tv", "TV_ON"),
            _make_text_sample("volume up", "VOLUME_UP"),
        ]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        result = computer.compute(manifest, tmp_path)

        expected_phonemes = {"T", "ER", "N", "AO", "DH", "AH", "IY", "V", "L", "Y", "UH", "M", "P"}
        assert expected_phonemes.issubset(set(result.phoneme_list))

    def test_words_to_phonemes_maps_each_content_word(self, tmp_path: Path) -> None:
        samples = [_make_text_sample("turn on the tv", "TV_ON")]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        result = computer.compute(manifest, tmp_path)

        assert "turn" in result.words_to_phonemes
        assert "on" in result.words_to_phonemes
        assert result.words_to_phonemes["turn"] == ["T", "ER", "N"]
        assert result.words_to_phonemes["on"] == ["AO", "N"]

    def test_ctc_blank_idx_equals_len_phoneme_list(self, tmp_path: Path) -> None:
        samples = [_make_text_sample("turn on the tv", "TV_ON")]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        result = computer.compute(manifest, tmp_path)

        assert result.ctc_blank_idx == len(result.phoneme_list)

    def test_phoneme_list_txt_written(self, tmp_path: Path) -> None:
        samples = [_make_text_sample("turn on the tv", "TV_ON")]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        result = computer.compute(manifest, tmp_path)

        phoneme_file = tmp_path / "phoneme_list.txt"
        assert phoneme_file.exists()
        lines = phoneme_file.read_text(encoding="utf-8").splitlines()
        assert set(lines) == set(result.phoneme_list)

    def test_words_to_phonemes_json_written(self, tmp_path: Path) -> None:
        samples = [_make_text_sample("turn on the tv", "TV_ON")]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        result = computer.compute(manifest, tmp_path)

        w2p_file = tmp_path / "words_to_phonemes.json"
        assert w2p_file.exists()
        loaded = json.loads(w2p_file.read_text(encoding="utf-8"))
        assert loaded == result.words_to_phonemes

    def test_phoneme_list_is_sorted(self, tmp_path: Path) -> None:
        samples = [
            _make_text_sample("turn on", "TV_ON"),
            _make_text_sample("volume up", "VOLUME_UP"),
        ]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        result = computer.compute(manifest, tmp_path)

        assert result.phoneme_list == sorted(result.phoneme_list)

    def test_uses_content_not_label(self, tmp_path: Path) -> None:
        """VocabComputer must extract words from content, not label."""
        # Label TV_ON is not in _FakePhonemeProvider as a whole token; content words are
        # If VocabComputer used label, it would raise PhonemeNotFoundError
        samples = [_make_text_sample("turn on", "UNKNOWN_LABEL")]
        manifest = Manifest(samples)
        computer = VocabComputer(_FakePhonemeProvider())
        # Should not raise — content words turn and on are in the fake provider
        result = computer.compute(manifest, tmp_path)
        assert "turn" in result.words_to_phonemes
        assert "on" in result.words_to_phonemes
