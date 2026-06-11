"""DVC entry-point: generate phrase variants and write Manifest[TextSample].

Usage:
    python intent_01_generate_phrases.py
        --input-phrases PATH
        --output-dir DIR
        --variations-per-phrase N
        --subsample-rate N
"""

from __future__ import annotations

import argparse
import csv
import random
import sys
from pathlib import Path

# Ensure ml/ is on the path when invoked directly
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.intent.phrase_variator import PhraseVariator
from pipeline.stages import conventions


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate phrase variants")
    parser.add_argument("--input-phrases", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--variations-per-phrase", required=True, type=int)
    parser.add_argument("--subsample-rate", required=True, type=int)
    args = parser.parse_args()

    # Read input CSV
    base_phrases: list[tuple[str, str]] = []
    with open(args.input_phrases, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            base_phrases.append((row["phrase"], row["command"]))

    # Generate variants
    variator = PhraseVariator(random.Random(42))
    variants = variator.generate(base_phrases, args.variations_per_phrase)

    # Apply subsample filter
    subsampled = [s for i, s in enumerate(variants) if i % args.subsample_rate == 0]

    # Write manifest
    args.output_dir.mkdir(parents=True, exist_ok=True)
    store = ManifestStore()
    store.write(Manifest(subsampled), conventions.manifest_path(args.output_dir))


if __name__ == "__main__":
    main()
