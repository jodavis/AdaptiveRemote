"""Protocol types for machine learning model building and inference in the speech pipeline.

MachineLearningModel: protocol implemented by trained model objects (train, predict, save).
MachineLearningModelBuilder: protocol for constructing and loading MachineLearningModel instances.

Shared between training, evaluation, and testing stages.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Protocol

import numpy as np


class MachineLearningModel(Protocol):
    """A trained machine learning model that supports inference and persistence."""

    def train(self, dataset: Any, epochs: int) -> list[float]:
        """Fit the model on a pre-batched, pre-prefetched dataset.

        Returns per-epoch loss values.
        """
        ...

    def predict(self, dataset: Any) -> np.ndarray:
        """Run inference on a dataset; return raw model output."""
        ...

    def save(self, path: Path) -> None:
        """Persist the model to path."""
        ...


class MachineLearningModelBuilder(Protocol):
    """Constructs and loads MachineLearningModel instances."""

    def build_ctc_model(self, num_classes: int, n_mels: int, time_steps: int) -> MachineLearningModel:
        """Construct and compile a CTC model for speech recognition.

        Args:
            num_classes: output classes = len(phoneme_list) + 1 (CTC blank).
            n_mels: mel-spectrogram height (first axis of each input).
            time_steps: spectrogram width (second axis of each input).
        """
        ...

    def load(self, path: Path) -> MachineLearningModel:
        """Load a model previously saved with MachineLearningModel.save()."""
        ...
