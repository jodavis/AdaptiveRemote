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
class GenerateSamplesParams:
    speech_rate_min: int
    speech_rate_max: int


@dataclass
class AddDelaysParams:
    prefix_vary_probability: float
    prefix_min_s: float
    prefix_max_s: float
    suffix_vary_probability: float
    suffix_min_s: float
    suffix_max_s: float


@dataclass
class PipelineParams:
    variations_per_phrase: int
    subsample_rate: int
    generate_phrases: GeneratePhraseParams
    generate_samples: GenerateSamplesParams
    add_delays: AddDelaysParams

    @classmethod
    def load(cls, path: Path) -> "PipelineParams":
        with open(path, encoding="utf-8") as f:
            raw = yaml.safe_load(f)

        pipeline = raw["pipeline"]
        stage_phrases = raw["stages"]["generate_phrases"]
        stage_samples = raw["stages"]["generate_speech_samples"]
        stage_delays = raw["stages"]["add_delays"]

        return cls(
            variations_per_phrase=int(pipeline["variations_per_phrase"]),
            subsample_rate=int(pipeline["subsample_rate"]),
            generate_phrases=GeneratePhraseParams(
                repeat_modifier_chance=float(stage_phrases["repeat_modifier_chance"]),
                pleasantry_chance=float(stage_phrases["pleasantry_chance"]),
                hesitation_chance=float(stage_phrases["hesitation_chance"]),
                spelling_variant_chance=float(stage_phrases["spelling_variant_chance"]),
                case_variant_chance=float(stage_phrases["case_variant_chance"]),
            ),
            generate_samples=GenerateSamplesParams(
                speech_rate_min=int(stage_samples["speech_rate_min"]),
                speech_rate_max=int(stage_samples["speech_rate_max"]),
            ),
            add_delays=AddDelaysParams(
                prefix_vary_probability=float(stage_delays["prefix_vary_probability"]),
                prefix_min_s=float(stage_delays["prefix_min_s"]),
                prefix_max_s=float(stage_delays["prefix_max_s"]),
                suffix_vary_probability=float(stage_delays["suffix_vary_probability"]),
                suffix_min_s=float(stage_delays["suffix_min_s"]),
                suffix_max_s=float(stage_delays["suffix_max_s"]),
            ),
        )
