"""DVC entry-point: generate phrase variants and write Manifest[TextSample].

Usage:
    python intent_01_generate_phrases.py
        --input-phrases PATH
        --output-dir DIR

All tunable parameters (variations_per_phrase, subsample_rate, and probability
floats) are read from ml/params.yaml.
"""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

# Ensure ml/ is on the path when invoked directly
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.intent.phrase_variator import PhraseVariator
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate phrase variants")
    parser.add_argument("--input-phrases", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    # Load DVC params
    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    # Read input CSV
    base_phrases: list[tuple[str, str]] = []
    with open(args.input_phrases, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            base_phrases.append((row["phrase"], row["command"]))

    # Generate variants
    variator = PhraseVariator(
        vgen_factory=lambda seed: VariationGenerator(seed),
        params=params.generate_phrases,
    )
    variants = variator.generate(base_phrases, params.variations_per_phrase)

    # Apply subsample filter
    subsampled = [s for i, s in enumerate(variants) if i % params.subsample_rate == 0]

    # Write manifest
    args.output_dir.mkdir(parents=True, exist_ok=True)
    store = ManifestStore()
    store.write(Manifest(subsampled), conventions.manifest_path(args.output_dir))


if __name__ == "__main__":
    main()
