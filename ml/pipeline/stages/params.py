"""Typed params objects deserialised from params.yaml.

Load the shared params file once via PipelineParams.load(path) and pass the
relevant sub-object (e.g. GeneratePhraseParams) to each stage or component.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import yaml


@dataclass
class GeneratePhraseParams:
    repeat_modifier_chance: float
    pleasantry_chance: float
    hesitation_chance: float
    spelling_variant_chance: float
    case_variant_chance: float


@dataclass
class PipelineParams:
    variations_per_phrase: int
    subsample_rate: int
    generate_phrases: GeneratePhraseParams

    @classmethod
    def load(cls, path: Path) -> "PipelineParams":
        with open(path, encoding="utf-8") as f:
            raw = yaml.safe_load(f)

        pipeline = raw["pipeline"]
        stage_raw = raw["stages"]["generate_phrases"]

        return cls(
            variations_per_phrase=int(pipeline["variations_per_phrase"]),
            subsample_rate=int(pipeline["subsample_rate"]),
            generate_phrases=GeneratePhraseParams(
                repeat_modifier_chance=float(stage_raw["repeat_modifier_chance"]),
                pleasantry_chance=float(stage_raw["pleasantry_chance"]),
                hesitation_chance=float(stage_raw["hesitation_chance"]),
                spelling_variant_chance=float(stage_raw["spelling_variant_chance"]),
                case_variant_chance=float(stage_raw["case_variant_chance"]),
            ),
        )
