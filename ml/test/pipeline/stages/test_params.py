"""Unit tests for PipelineParams and GeneratePhraseParams."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
import yaml

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.stages.params import AddDelaysParams, GeneratePhraseParams, GenerateSamplesParams, PipelineParams


def _write_params(path: Path, data: dict) -> Path:
    params_file = path / "params.yaml"
    params_file.write_text(yaml.dump(data), encoding="utf-8")
    return params_file


_VALID_DATA = {
    "pipeline": {
        "variations_per_phrase": 20,
        "subsample_rate": 200,
    },
    "stages": {
        "generate_phrases": {
            "repeat_modifier_chance": 0.2,
            "pleasantry_chance": 0.3,
            "hesitation_chance": 0.3,
            "spelling_variant_chance": 0.1,
            "case_variant_chance": 0.2,
        },
        "generate_speech_samples": {
            "speech_rate_min": -10,
            "speech_rate_max": 20,
        },
        "add_delays": {
            "prefix_vary_probability": 0.333,
            "prefix_min_s": 0.02,
            "prefix_max_s": 0.15,
            "suffix_vary_probability": 0.333,
            "suffix_min_s": 0.02,
            "suffix_max_s": 0.15,
        },
    },
}


class TestPipelineParamsLoad:
    def test_loads_pipeline_fields(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        assert params.variations_per_phrase == 20
        assert params.subsample_rate == 200

    def test_loads_generate_phrases_fields(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        gp = params.generate_phrases
        assert gp.repeat_modifier_chance == pytest.approx(0.2)
        assert gp.pleasantry_chance == pytest.approx(0.3)
        assert gp.hesitation_chance == pytest.approx(0.3)
        assert gp.spelling_variant_chance == pytest.approx(0.1)
        assert gp.case_variant_chance == pytest.approx(0.2)

    def test_generate_phrases_is_correct_type(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        assert isinstance(params.generate_phrases, GeneratePhraseParams)

    def test_missing_file_raises(self, tmp_path: Path) -> None:
        with pytest.raises(FileNotFoundError):
            PipelineParams.load(tmp_path / "nonexistent.yaml")

    def test_missing_pipeline_key_raises(self, tmp_path: Path) -> None:
        data = {k: v for k, v in _VALID_DATA.items() if k != "pipeline"}
        params_file = _write_params(tmp_path, data)
        with pytest.raises(KeyError):
            PipelineParams.load(params_file)

    def test_missing_stages_key_raises(self, tmp_path: Path) -> None:
        data = {k: v for k, v in _VALID_DATA.items() if k != "stages"}
        params_file = _write_params(tmp_path, data)
        with pytest.raises(KeyError):
            PipelineParams.load(params_file)

    def test_loads_generate_samples_fields(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        gs = params.generate_samples
        assert gs.speech_rate_min == -10
        assert gs.speech_rate_max == 20

    def test_generate_samples_is_correct_type(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        assert isinstance(params.generate_samples, GenerateSamplesParams)

    def test_missing_generate_speech_samples_stage_raises(
        self, tmp_path: Path
    ) -> None:
        data = {
            "pipeline": _VALID_DATA["pipeline"],
            "stages": {"generate_phrases": _VALID_DATA["stages"]["generate_phrases"]},
        }
        params_file = _write_params(tmp_path, data)
        with pytest.raises(KeyError):
            PipelineParams.load(params_file)

    def test_missing_add_delays_stage_raises(self, tmp_path: Path) -> None:
        import copy
        data = copy.deepcopy(_VALID_DATA)
        del data["stages"]["add_delays"]
        params_file = _write_params(tmp_path, data)
        with pytest.raises(KeyError):
            PipelineParams.load(params_file)


class TestAddDelaysParamsLoad:
    def test_loads_add_delays_fields(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        ad = params.add_delays
        assert ad.prefix_vary_probability == pytest.approx(0.333)
        assert ad.prefix_min_s == pytest.approx(0.02)
        assert ad.prefix_max_s == pytest.approx(0.15)
        assert ad.suffix_vary_probability == pytest.approx(0.333)
        assert ad.suffix_min_s == pytest.approx(0.02)
        assert ad.suffix_max_s == pytest.approx(0.15)

    def test_add_delays_is_correct_type(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        assert isinstance(params.add_delays, AddDelaysParams)

    def test_add_delays_fields_are_floats(self, tmp_path: Path) -> None:
        params_file = _write_params(tmp_path, _VALID_DATA)
        params = PipelineParams.load(params_file)

        ad = params.add_delays
        assert isinstance(ad.prefix_vary_probability, float)
        assert isinstance(ad.prefix_min_s, float)
        assert isinstance(ad.prefix_max_s, float)
        assert isinstance(ad.suffix_vary_probability, float)
        assert isinstance(ad.suffix_min_s, float)
        assert isinstance(ad.suffix_max_s, float)
