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

import yaml

# Ensure ml/ is on the path when invoked directly
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.intent.phrase_variator import PhraseVariator
from pipeline.stages import conventions

_PARAMS_PATH = Path(__file__).parents[2] / "params.yaml"


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate phrase variants")
    parser.add_argument("--input-phrases", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    # Load DVC params
    with open(_PARAMS_PATH, encoding="utf-8") as f:
        params = yaml.safe_load(f)

    variations_per_phrase: int = params["pipeline"]["variations_per_phrase"]
    subsample_rate: int = params["pipeline"]["subsample_rate"]
    stage_params = params["stages"]["generate_phrases"]
    repeat_modifier_chance: float = stage_params["repeat_modifier_chance"]
    pleasantry_chance: float = stage_params["pleasantry_chance"]
    hesitation_chance: float = stage_params["hesitation_chance"]
    spelling_variant_chance: float = stage_params["spelling_variant_chance"]
    case_variant_chance: float = stage_params["case_variant_chance"]

    # Read input CSV
    base_phrases: list[tuple[str, str]] = []
    with open(args.input_phrases, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            base_phrases.append((row["phrase"], row["command"]))

    # Generate variants
    variator = PhraseVariator(
        vgen_factory=lambda seed: VariationGenerator(seed),
        repeat_modifier_chance=repeat_modifier_chance,
        pleasantry_chance=pleasantry_chance,
        hesitation_chance=hesitation_chance,
        spelling_variant_chance=spelling_variant_chance,
        case_variant_chance=case_variant_chance,
    )
    variants = variator.generate(base_phrases, variations_per_phrase)

    # Apply subsample filter
    subsampled = [s for i, s in enumerate(variants) if i % subsample_rate == 0]

    # Write manifest
    args.output_dir.mkdir(parents=True, exist_ok=True)
    store = ManifestStore()
    store.write(Manifest(subsampled), conventions.manifest_path(args.output_dir))


if __name__ == "__main__":
    main()
