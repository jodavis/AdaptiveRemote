from __future__ import annotations

import json
import string
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

from pipeline.core.manifest import Manifest
from pipeline.core.sample import TextSample


class PhonemeProvider(Protocol):
    """Looks up phonemes for a single word."""

    def lookup(self, word: str) -> list[str]:
        """Return the phoneme list for word.

        Raises PhonemeNotFoundError if word is not in dictionary.
        """
        ...


class PhonemeNotFoundError(Exception):
    """Raised when a word is not found in the phoneme dictionary."""


@dataclass
class VocabResult:
    """Vocabulary extracted from a TextSample manifest.

    phoneme_list: sorted unique phonemes across all label words.
    words_to_phonemes: maps each label word to its phoneme list.
    ctc_blank_idx: index of the CTC blank token = len(phoneme_list).
    """

    phoneme_list: list[str]
    words_to_phonemes: dict[str, list[str]]
    ctc_blank_idx: int


class VocabComputer:
    """Extracts phoneme vocabulary from TextSample.label values.

    Uses TextSample.label (speech_to_detect / canonical command name) — NOT
    TextSample.content (surface form). This is an intentional change from the
    existing pipeline which used the surface form.

    Label words are canonical command names like TV_ON, VOLUME_UP. No digit-
    to-word substitution is needed or performed.
    """

    def __init__(self, phoneme_provider: PhonemeProvider) -> None:
        self._provider = phoneme_provider

    def compute(self, manifest: Manifest[TextSample], output_dir: Path) -> VocabResult:
        """Extract phoneme vocabulary and write output files.

        Writes to output_dir:
          phoneme_list.txt  — one phoneme per line, sorted
          words_to_phonemes.json — {"WORD": ["P", "H", "O", "N", "E", "M", "E", "S"], ...}
        """
        # Collect unique words from all labels
        words: set[str] = set()
        for sample in manifest.samples:
            for word in sample.content.split(" "):
                # Filter out punctuation except apostrophes
                filtered_word = "".join(
                    c for c in word if c not in string.punctuation or c == "'"
                )
                if filtered_word:
                    words.add(filtered_word)

        words_to_phonemes: dict[str, list[str]] = {}
        all_phonemes: set[str] = set()
        for word in sorted(words):
            phonemes = self._provider.lookup(word)
            words_to_phonemes[word] = phonemes
            all_phonemes.update(phonemes)

        phoneme_list = sorted(all_phonemes)
        ctc_blank_idx = len(phoneme_list)

        output_dir.mkdir(parents=True, exist_ok=True)
        (output_dir / "phoneme_list.txt").write_text(
            "\n".join(phoneme_list), encoding="utf-8"
        )
        with open(output_dir / "words_to_phonemes.json", "w", encoding="utf-8") as f:
            json.dump(words_to_phonemes, f)

        return VocabResult(
            phoneme_list=phoneme_list,
            words_to_phonemes=words_to_phonemes,
            ctc_blank_idx=ctc_blank_idx,
        )
