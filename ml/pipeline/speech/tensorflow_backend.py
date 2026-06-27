"""TensorFlow/Keras implementation of MachineLearningModel and MachineLearningModelBuilder.

Import this module only from production entry-points, not from unit tests.
TensorflowModel wraps a compiled tf.keras.Model and implements MachineLearningModel.
TensorflowModelBuilder constructs and loads TensorflowModel instances.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

import numpy as np
import tensorflow as tf


class TensorflowModel:
    """MachineLearningModel backed by a compiled tf.keras.Model."""

    def __init__(self, model: Any) -> None:
        self._model = model

    def train(self, dataset: Any, epochs: int) -> list[float]:
        """Fit the model and return per-epoch loss values."""
        history = self._model.fit(dataset, epochs=epochs)
        loss_values: list[float] = history.history.get("loss", [])
        return [float(v) for v in loss_values]

    def predict(self, dataset: Any) -> np.ndarray:
        """Run model.predict() and return the raw output array."""
        return self._model.predict(dataset)

    def save(self, path: Path) -> None:
        """Save the model in Keras .keras format."""
        path.parent.mkdir(parents=True, exist_ok=True)
        self._model.save(str(path))


class TensorflowModelBuilder:
    """MachineLearningModelBuilder that creates TensorflowModel instances using TensorFlow/Keras.

    CTC loss is applied via model.compile(loss='ctc') — available in Keras 3 / TF 2.x.
    The (n_mels, time_steps) shape used throughout the pipeline is transposed for LSTM
    input, which expects (time, features).
    """

    def build_ctc_model(self, num_classes: int, n_mels: int, time_steps: int) -> TensorflowModel:
        """Build and compile a simple CTC model for speech recognition.

        Architecture:
          - Input: (time_steps, n_mels) — transposed spectrogram
          - Bidirectional LSTM
          - Dense(num_classes, softmax) output
          - CTC loss via model.compile(loss='ctc')
        """
        inputs = tf.keras.Input(shape=(time_steps, n_mels), name="spectrogram")
        x = tf.keras.layers.Bidirectional(
            tf.keras.layers.LSTM(128, return_sequences=True)
        )(inputs)
        outputs = tf.keras.layers.Dense(num_classes, activation="softmax", name="logits")(x)
        model = tf.keras.Model(inputs=inputs, outputs=outputs)
        model.compile(optimizer="adam", loss="ctc")
        return TensorflowModel(model)

    def load(self, path: Path) -> TensorflowModel:
        """Load a Keras model from a .keras file."""
        return TensorflowModel(tf.keras.models.load_model(str(path)))
