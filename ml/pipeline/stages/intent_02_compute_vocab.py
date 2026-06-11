"""DVC entry-point: compute phoneme vocabulary from Manifest[TextSample].

Usage:
    python intent_02_compute_vocab.py
        --input-manifest-dir DIR
        --output-dir DIR
        --phoneme-dict PATH
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.intent.vocab_computer import PhonemeNotFoundError, VocabComputer
from pipeline.stages import conventions


class _CmuPhonemeProvider:
    """Reads the CMU Pronouncing Dictionary and provides phoneme lookups."""

    def __init__(self, dict_path: Path) -> None:
        self._lookup: dict[str, list[str]] = {}
        with open(dict_path, encoding="latin-1") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith(";;;"):
                    continue
                parts = line.split()
                if len(parts) < 2:
                    continue
                word = parts[0].rstrip("(0123456789)")
                # Strip stress digits from phonemes
                phonemes = [p.rstrip("012") for p in parts[1:]]
                # Only store first pronunciation
                if word not in self._lookup:
                    self._lookup[word] = phonemes

    def lookup(self, word: str) -> list[str]:
        key = word.upper()
        result = self._lookup.get(key)
        if result is None:
            raise PhonemeNotFoundError(f"Word not found in CMU dict: {word!r}")
        return result


def main() -> None:
    parser = argparse.ArgumentParser(description="Compute phoneme vocabulary")
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--phoneme-dict", required=True, type=Path)
    args = parser.parse_args()

    store = ManifestStore()
    manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

    provider = _CmuPhonemeProvider(args.phoneme_dict)
    computer = VocabComputer(provider)
    computer.compute(manifest, args.output_dir)  # type: ignore[arg-type]


if __name__ == "__main__":
    main()
