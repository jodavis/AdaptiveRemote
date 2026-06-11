"""Path conventions for all DVC stage entry-points.

All pipeline stages resolve their file paths through this module so that path
changes require updating only one place.
"""

from __future__ import annotations

from pathlib import Path


def manifest_path(output_dir: Path) -> Path:
    return output_dir / "manifest.json"


def split_manifest_path(output_dir: Path, split: str) -> Path:
    """split: 'train', 'val', or 'test'."""
    return output_dir / f"{split}_manifest.json"


def sample_file_path(output_dir: Path, sample_id: str, ext: str) -> Path:
    """ext has no leading dot, e.g. 'wav', 'npy', 'json'."""
    return output_dir / f"{sample_id}.{ext}"


def model_path(output_dir: Path, model_name: str) -> Path:
    return output_dir / f"{model_name}_model.keras"


def evaluation_predictions_path(output_dir: Path) -> Path:
    return output_dir / "evaluation_predictions.txt"


def evaluation_metrics_path(output_dir: Path) -> Path:
    """JSON file written by ModelEvaluator: {"wer": <float>}."""
    return output_dir / "metrics.json"


def test_samples_path(output_dir: Path) -> Path:
    """Zip written by ModelEvaluator.package_test_samples(): known-good audio fixtures."""
    return output_dir / "test_samples.zip"
