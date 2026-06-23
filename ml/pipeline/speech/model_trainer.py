"""ModelTrainer: train a CTC speech-to-text model from featurised manifests.

KerasBackend is a Protocol so that tests can supply a stub without TensorFlow.
DefaultKerasBackend is the production implementation; it defers all
``import tensorflow as tf`` calls inside method bodies so that this module is
importable in environments that do not have TensorFlow installed (e.g. test
runners on CI without a GPU image).

ModelTrainer.train() is synchronous. The entry-point calls it directly.
"""

from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any, Protocol

import numpy as np

from pipeline.core.manifest import Manifest
from pipeline.core.sample import AudioSample, SampleSpectrogram, SampleTokens
from pipeline.intent.vocab_computer import VocabResult
from pipeline.stages import conventions

_logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# KerasBackend protocol
# ---------------------------------------------------------------------------


class KerasBackend(Protocol):
    """Injectable abstraction over TensorFlow/Keras.

    The default implementation (DefaultKerasBackend) defers all TF imports
    inside method bodies. Test stubs can implement this protocol without TF.
    """

    def build_ctc_model(self, num_classes: int, n_mels: int, time_steps: int) -> Any:
        """Construct and compile a CTC model.

        Args:
            num_classes: number of output classes = len(phoneme_list) + 1
                         (the +1 is the CTC blank token).
            n_mels: mel-spectrogram height (first axis of each input).
            time_steps: spectrogram width (second axis of each input).

        Returns:
            A compiled Keras model.
        """
        ...

    def train(
        self,
        model: Any,
        dataset: Any,
        epochs: int,
    ) -> list[float]:
        """Fit the model on an already-batched, already-prefetched dataset.

        Args:
            model: compiled Keras model from build_ctc_model.
            dataset: tf.data.Dataset yielding (spectrogram, tokens) batches.
                     ModelTrainer is responsible for batching and prefetching
                     before calling this method.
            epochs: number of training epochs.

        Returns:
            Per-epoch loss values (logged by ModelTrainer but not written to disk).
        """
        ...

    def predict(self, model: Any, dataset: Any) -> np.ndarray:
        """Run inference on a dataset; return raw model output."""
        ...

    def save(self, model: Any, path: Path) -> None:
        """Persist the model to *path* (Keras .keras format)."""
        ...

    def load(self, path: Path) -> Any:
        """Load a model previously saved with save()."""
        ...


# ---------------------------------------------------------------------------
# DefaultKerasBackend — production TF/Keras implementation
# ---------------------------------------------------------------------------


class DefaultKerasBackend:
    """Production KerasBackend that wraps TensorFlow/Keras.

    All ``import tensorflow as tf`` calls are deferred to individual method
    bodies so that importing this module does not require TF to be installed.

    CTC loss is implemented via a custom training step using
    ``tf.keras.losses.CTC`` (TF 2.x / Keras 3 API).  The model is built as a
    standard Sequential/Functional graph; the CTC loss is applied inside a
    subclassed Model so that ``model.fit()`` can be called with
    (spectrogram, label) batches directly, without a separate label-length input.
    """

    def build_ctc_model(self, num_classes: int, n_mels: int, time_steps: int) -> Any:
        """Build and compile a simple CTC model for speech recognition.

        Architecture:
          - Input: (time_steps, n_mels) — transposed spectrogram
          - Bidirectional LSTM
          - Dense(num_classes, softmax) output
          - CTC loss via model.compile(loss='ctc')

        The (n_mels, time_steps) shape used throughout the pipeline is
        transposed here because recurrent layers expect (time, features).
        """
        import tensorflow as tf  # deferred: unit tests without TF can import module

        inputs = tf.keras.Input(shape=(time_steps, n_mels), name="spectrogram")
        x = tf.keras.layers.Bidirectional(
            tf.keras.layers.LSTM(128, return_sequences=True)
        )(inputs)
        outputs = tf.keras.layers.Dense(num_classes, activation="softmax", name="logits")(x)
        model = tf.keras.Model(inputs=inputs, outputs=outputs)
        # Use 'ctc' string loss — available in Keras 3 / TF 2.x
        model.compile(optimizer="adam", loss="ctc")
        return model

    def train(
        self,
        model: Any,
        dataset: Any,
        epochs: int,
    ) -> list[float]:
        """Fit the model and return per-epoch loss values."""
        import tensorflow as tf  # deferred

        history = model.fit(dataset, epochs=epochs)
        loss_values: list[float] = history.history.get("loss", [])
        return [float(v) for v in loss_values]

    def predict(self, model: Any, dataset: Any) -> np.ndarray:
        """Run model.predict() and return the raw output array."""
        import tensorflow as tf  # deferred

        return model.predict(dataset)

    def save(self, model: Any, path: Path) -> None:
        """Save the model in Keras .keras format."""
        import tensorflow as tf  # deferred

        path.parent.mkdir(parents=True, exist_ok=True)
        model.save(str(path))

    def load(self, path: Path) -> Any:
        """Load a Keras model from a .keras file."""
        import tensorflow as tf  # deferred

        return tf.keras.models.load_model(str(path))


# ---------------------------------------------------------------------------
# ModelTrainer
# ---------------------------------------------------------------------------


class ModelTrainer:
    """Trains a CTC speech-to-text model from featurised manifests.

    Responsibilities:
    - Filter spectrogram/token manifests to the train split by parent_id.
    - Load spectrogram .npy and token .json files for filtered samples.
    - Construct a tf.data.Dataset with batching and prefetching.
    - Call KerasBackend.build_ctc_model, .train, and .save.
    - Return the saved model path.
    """

    def __init__(
        self,
        keras_backend: KerasBackend,
        n_mels: int,
        time_steps: int,
        epochs: int,
        batch_size: int,
    ) -> None:
        self._backend = keras_backend
        self._n_mels = n_mels
        self._time_steps = time_steps
        self._epochs = epochs
        self._batch_size = batch_size

    def train(
        self,
        train_manifest: Manifest[AudioSample],
        vocab: VocabResult,
        spectrogram_manifest: Manifest[SampleSpectrogram],
        token_manifest: Manifest[SampleTokens],
        spectrogram_dir: Path,
        token_dir: Path,
        output_dir: Path,
    ) -> Path:
        """Train a CTC model and return the saved model path.

        spectrogram_manifest and token_manifest are the FULL combined manifests
        (covering all splits).  train_manifest is the train-split subset.  Only
        samples whose parent_id is in {s.id for s in train_manifest.samples} are
        used for training.
        """
        train_ids: set[str] = {s.id for s in train_manifest.samples}

        # Build parent_id → sample lookup dicts for the filtered sets
        spec_by_parent: dict[str, SampleSpectrogram] = {
            s.parent_id: s
            for s in spectrogram_manifest.samples
            if s.parent_id in train_ids
        }
        tok_by_parent: dict[str, SampleTokens] = {
            s.parent_id: s
            for s in token_manifest.samples
            if s.parent_id in train_ids
        }

        # Load arrays for samples that appear in both filtered manifests
        pairs: list[tuple[np.ndarray, np.ndarray]] = []
        for parent_id in spec_by_parent:
            if parent_id not in tok_by_parent:
                _logger.warning("No token sample for parent_id %s — skipping", parent_id)
                continue
            spec_sample = spec_by_parent[parent_id]
            tok_sample = tok_by_parent[parent_id]

            spec_array = np.load(str(spectrogram_dir / spec_sample.path))
            tok_data = json.loads((token_dir / tok_sample.path).read_text(encoding="utf-8"))
            tok_array = np.array(tok_data["tokens"], dtype=np.int32)

            pairs.append((spec_array, tok_array))

        num_classes = vocab.ctc_blank_idx + 1  # ctc_blank_idx = len(phoneme_list)

        model = self._backend.build_ctc_model(num_classes, self._n_mels, self._time_steps)

        dataset = self._build_dataset(pairs, self._batch_size)

        per_epoch_loss = self._backend.train(model, dataset, self._epochs)
        _logger.info("Training complete; per-epoch loss: %s", per_epoch_loss)

        output_dir.mkdir(parents=True, exist_ok=True)
        model_path = conventions.model_path(output_dir, "speech_to_text")
        self._backend.save(model, model_path)

        return model_path

    def _build_dataset(
        self, pairs: list[tuple[np.ndarray, np.ndarray]], batch_size: int
    ) -> Any:
        """Construct a batched and prefetched tf.data.Dataset from (spec, tokens) pairs.

        Deferred TF import so the module is importable without TF installed.
        The dataset yields (spectrogram, tokens) batches where spectrogram
        shape is (batch, n_mels, time_steps) and tokens shape is
        (batch, input_token_length).
        """
        if not pairs:
            # Return an empty iterable for zero-sample edge case
            return []

        import tensorflow as tf  # deferred: unit tests without TF can import module

        specs = np.stack([p[0] for p in pairs], axis=0)
        toks = np.stack([p[1] for p in pairs], axis=0)

        dataset = tf.data.Dataset.from_tensor_slices((specs, toks))
        dataset = dataset.batch(batch_size).prefetch(tf.data.AUTOTUNE)
        return dataset
