"""Unit tests for pipeline.stages.conventions."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.stages import conventions


class TestConventions:
    def test_all_seven_functions(self, tmp_path: Path) -> None:
        assert conventions.manifest_path(tmp_path) == tmp_path / "manifest.json"
        assert conventions.split_manifest_path(tmp_path, "train") == tmp_path / "train_manifest.json"
        assert conventions.split_manifest_path(tmp_path, "val") == tmp_path / "val_manifest.json"
        assert conventions.split_manifest_path(tmp_path, "test") == tmp_path / "test_manifest.json"
        assert conventions.sample_file_path(tmp_path, "abc123", "wav") == tmp_path / "abc123.wav"
        assert conventions.sample_file_path(tmp_path, "abc123", "npy") == tmp_path / "abc123.npy"
        assert conventions.model_path(tmp_path, "speech_to_text") == tmp_path / "speech_to_text_model.keras"
        assert conventions.evaluation_predictions_path(tmp_path) == tmp_path / "evaluation_predictions.txt"
        assert conventions.evaluation_metrics_path(tmp_path) == tmp_path / "metrics.json"
        assert conventions.test_samples_path(tmp_path) == tmp_path / "test_samples.zip"

    def test_params_path_returns_params_yaml(self, tmp_path: Path) -> None:
        assert conventions.params_path(tmp_path) == tmp_path / "params.yaml"
