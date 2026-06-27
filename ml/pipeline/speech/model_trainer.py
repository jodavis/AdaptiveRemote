"""ModelTrainer: train a CTC speech-to-text model from featurised manifests.

MachineLearningModelBuilder is the injectable abstraction over the training backend.
TensorflowModelBuilder (in tensorflow_backend.py) is the production implementation.
Test stubs implement MachineLearningModelBuilder and MachineLearningModel without TensorFlow.

ModelTrainer.train() is synchronous. The entry-point calls it directly.
"""

from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

import numpy as np

from pipeline.core.manifest import Manifest
from pipeline.core.sample import AudioSample, SampleSpectrogram, SampleTokens
from pipeline.intent.vocab_computer import VocabResult
from pipeline.speech.manifest_filter import lookup_sample_triplets
from pipeline.speech.ml_model import MachineLearningModelBuilder
from pipeline.stages import conventions

_logger = logging.getLogger(__name__)


class ModelTrainer:
    """Trains a CTC speech-to-text model from featurised manifests.

    Responsibilities:
    - Delegate manifest filtering to lookup_sample_triplets.
    - Load spectrogram .npy and token .json files for matched samples.
    - Construct a tf.data.Dataset with batching and prefetching.
    - Call MachineLearningModelBuilder.build_ctc_model, then model.train and model.save.
    - Return the saved model path.
    """

    def __init__(
        self,
        backend: MachineLearningModelBuilder,
        n_mels: int,
        time_steps: int,
        epochs: int,
        batch_size: int,
    ) -> None:
        self._backend = backend
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
        (covering all splits).  train_manifest is the train-split subset.
        """
        triplets = lookup_sample_triplets(train_manifest, spectrogram_manifest, token_manifest)

        pairs: list[tuple[np.ndarray, np.ndarray]] = []
        for _audio, spec_sample, tok_sample in triplets:
            spec_array = np.load(str(spectrogram_dir / spec_sample.path))
            tok_data = json.loads((token_dir / tok_sample.path).read_text(encoding="utf-8"))
            tok_array = np.array(tok_data["tokens"], dtype=np.int32)
            pairs.append((spec_array, tok_array))

        if not pairs:
            _logger.warning(
                "ModelTrainer.train(): no (spectrogram, token) pairs found for train split — "
                "model will be trained on zero samples."
            )

        num_classes = vocab.ctc_blank_idx + 1  # ctc_blank_idx = len(phoneme_list)
        model = self._backend.build_ctc_model(num_classes, self._n_mels, self._time_steps)

        dataset = self._build_dataset(pairs, self._batch_size)
        per_epoch_loss = model.train(dataset, self._epochs)
        _logger.info("Training complete; per-epoch loss: %s", per_epoch_loss)

        output_dir.mkdir(parents=True, exist_ok=True)
        model_path = conventions.model_path(output_dir, "speech_to_text")
        model.save(model_path)

        return model_path

    def _build_dataset(
        self, pairs: list[tuple[np.ndarray, np.ndarray]], batch_size: int
    ) -> Any:
        """Construct a batched and prefetched tf.data.Dataset from (spec, tokens) pairs.

        Deferred TF import so the module is importable without TF installed.
        """
        if not pairs:
            return []

        import tensorflow as tf  # deferred: unit tests without TF can import module

        specs = np.stack([p[0] for p in pairs], axis=0)
        toks = np.stack([p[1] for p in pairs], axis=0)

        dataset = tf.data.Dataset.from_tensor_slices((specs, toks))
        dataset = dataset.batch(batch_size).prefetch(tf.data.AUTOTUNE)
        return dataset
