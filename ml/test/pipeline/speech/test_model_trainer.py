"""Unit tests for ModelTrainer.

Tests use inner-class stubs (not unittest.mock) for KerasBackend,
following the pattern used by other test files in this directory.

ModelTrainer.train() is synchronous — no asyncio.run() needed.
"""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path
from typing import Any

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest
from pipeline.core.sample import AudioSample, SampleSpectrogram, SampleTokens
from pipeline.intent.vocab_computer import VocabResult
from pipeline.speech.model_trainer import DefaultKerasBackend, ModelTrainer
from pipeline.stages import conventions


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str,
    transcript: str = "TV_ON",
) -> AudioSample:
    content_hash = hashlib.sha256(f"{sample_id}:audio".encode("utf-8")).hexdigest()
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=content_hash,
        path=Path(f"{sample_id}.wav"),
        parent_content_hash="parent_hash",
        transcript=transcript,
        applied_values={},
    )


def _make_spectrogram_sample(parent_id: str, n_mels: int = 2, time_steps: int = 3) -> SampleSpectrogram:
    content_hash = hashlib.sha256(f"{parent_id}:spec".encode("utf-8")).hexdigest()
    return SampleSpectrogram(
        id=parent_id,
        seed=0,
        content_hash=content_hash,
        path=Path(f"{parent_id}.npy"),
        parent_content_hash="spec_parent_hash",
        transcript="TV_ON",
        parent_id=parent_id,
    )


def _make_token_sample(parent_id: str, tokens: list[int] | None = None) -> SampleTokens:
    if tokens is None:
        tokens = [0, 1, 2]
    content_hash = hashlib.sha256(f"{parent_id}:tok".encode("utf-8")).hexdigest()
    return SampleTokens(
        id=parent_id,
        seed=0,
        content_hash=content_hash,
        path=Path(f"{parent_id}.json"),
        parent_content_hash="tok_parent_hash",
        transcript="TV_ON",
        parent_id=parent_id,
    )


def _make_vocab() -> VocabResult:
    phoneme_list = ["AA", "EH", "IH"]
    return VocabResult(
        phoneme_list=phoneme_list,
        words_to_phonemes={"TV_ON": ["AA", "EH"]},
        ctc_blank_idx=len(phoneme_list),
    )


def _write_npy(path: Path, n_mels: int = 2, time_steps: int = 3) -> None:
    """Write a minimal .npy file for a spectrogram sample."""
    path.parent.mkdir(parents=True, exist_ok=True)
    np.save(str(path), np.zeros((n_mels, time_steps), dtype=np.float32))


def _write_json_tokens(path: Path, tokens: list[int] | None = None) -> None:
    """Write a minimal .json file for a token sample."""
    import json

    if tokens is None:
        tokens = [0, 1, 2]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"phonemes": ["AA", "EH", "IH"], "tokens": tokens}), encoding="utf-8")


# ---------------------------------------------------------------------------
# Stub KerasBackend implementations
# ---------------------------------------------------------------------------


class _RecordingKerasBackend:
    """Stub that records calls without invoking TensorFlow."""

    def __init__(self, model_obj: object | None = None) -> None:
        self._model = model_obj or object()
        self.build_ctc_model_calls: list[tuple[int, int, int]] = []
        self.train_calls: list[tuple[Any, Any, int]] = []
        self.save_calls: list[tuple[Any, Path]] = []
        self.per_epoch_loss: list[float] = [0.5, 0.4, 0.3]

    def build_ctc_model(self, num_classes: int, n_mels: int, time_steps: int) -> Any:
        self.build_ctc_model_calls.append((num_classes, n_mels, time_steps))
        return self._model

    def train(self, model: Any, dataset: Any, epochs: int) -> list[float]:
        self.train_calls.append((model, dataset, epochs))
        return self.per_epoch_loss

    def predict(self, model: Any, dataset: Any) -> np.ndarray:
        return np.zeros((1, 3))

    def save(self, model: Any, path: Path) -> None:
        self.save_calls.append((model, path))

    def load(self, path: Path) -> Any:
        return self._model


class _CapturingDatasetBackend(_RecordingKerasBackend):
    """Stub that captures the dataset items passed to train()."""

    def __init__(self) -> None:
        super().__init__()
        self.captured_items: list[Any] = []

    def train(self, model: Any, dataset: Any, epochs: int) -> list[float]:
        self.captured_items = list(dataset)
        return super().train(model, dataset, epochs)


# ---------------------------------------------------------------------------
# TestFiltersByParentId
# ---------------------------------------------------------------------------


class TestFiltersByParentId:
    """ModelTrainer.train() must only include samples whose parent_id is in train_manifest."""

    def test_only_train_parent_ids_included(self, tmp_path: Path) -> None:
        """Dataset passed to KerasBackend.train contains only train-split entries."""
        n_mels, time_steps = 2, 3

        train_sample = _make_audio_sample("train_001")
        val_sample = _make_audio_sample("val_001")

        train_manifest: Manifest[AudioSample] = Manifest([train_sample])

        spec_train = _make_spectrogram_sample("train_001", n_mels, time_steps)
        spec_val = _make_spectrogram_sample("val_001", n_mels, time_steps)
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec_train, spec_val])

        tok_train = _make_token_sample("train_001")
        tok_val = _make_token_sample("val_001")
        token_manifest: Manifest[SampleTokens] = Manifest([tok_train, tok_val])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec_train.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok_train.path)

        backend = _CapturingDatasetBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        vocab = _make_vocab()
        trainer.train(
            train_manifest=train_manifest,
            vocab=vocab,
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        # One spectrogram+token pair for the train sample only
        assert len(backend.captured_items) == 1

    def test_excluded_val_sample_not_in_dataset(self, tmp_path: Path) -> None:
        """A sample in spectrogram_manifest but NOT in train_manifest is excluded."""
        n_mels, time_steps = 2, 3

        train_sample = _make_audio_sample("train_001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])

        # Only a val sample in spectrogram/token manifests — no train sample
        spec_val = _make_spectrogram_sample("val_only_999", n_mels, time_steps)
        tok_val = _make_token_sample("val_only_999")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec_val])
        token_manifest: Manifest[SampleTokens] = Manifest([tok_val])

        # Write files for spec / token that shouldn't be loaded
        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        # Write the spectrogram for the train sample (no file, so dataset will be empty)
        # — but since train_sample is NOT in spectrogram_manifest, dataset must be empty
        backend = _CapturingDatasetBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        vocab = _make_vocab()
        trainer.train(
            train_manifest=train_manifest,
            vocab=vocab,
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert len(backend.captured_items) == 0

    def test_multiple_train_samples_all_included(self, tmp_path: Path) -> None:
        """All train-split entries appear in the dataset."""
        n_mels, time_steps = 2, 3

        train_a = _make_audio_sample("train_a")
        train_b = _make_audio_sample("train_b")
        train_manifest: Manifest[AudioSample] = Manifest([train_a, train_b])

        spec_a = _make_spectrogram_sample("train_a", n_mels, time_steps)
        spec_b = _make_spectrogram_sample("train_b", n_mels, time_steps)
        tok_a = _make_token_sample("train_a")
        tok_b = _make_token_sample("train_b")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec_a, spec_b])
        token_manifest: Manifest[SampleTokens] = Manifest([tok_a, tok_b])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec_a.path, n_mels, time_steps)
        _write_npy(spectrogram_dir / spec_b.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok_a.path)
        _write_json_tokens(token_dir / tok_b.path)

        backend = _CapturingDatasetBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        vocab = _make_vocab()
        trainer.train(
            train_manifest=train_manifest,
            vocab=vocab,
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert len(backend.captured_items) == 2


# ---------------------------------------------------------------------------
# TestCallsKerasBackendTrain
# ---------------------------------------------------------------------------


class TestCallsKerasBackendTrain:
    """KerasBackend.train is called with the dataset, epochs count, and the built model."""

    def test_train_called_once(self, tmp_path: Path) -> None:
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=5,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert len(backend.train_calls) == 1

    def test_train_called_with_correct_epoch_count(self, tmp_path: Path) -> None:
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        expected_epochs = 7
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=expected_epochs,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        _, _, actual_epochs = backend.train_calls[0]
        assert actual_epochs == expected_epochs

    def test_train_called_with_model_from_build_ctc_model(self, tmp_path: Path) -> None:
        """The model passed to backend.train is the same object returned by build_ctc_model."""
        n_mels, time_steps = 2, 3
        sentinel_model = object()
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend(model_obj=sentinel_model)
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        actual_model, _, _ = backend.train_calls[0]
        assert actual_model is sentinel_model


# ---------------------------------------------------------------------------
# TestCallsKerasBackendSave
# ---------------------------------------------------------------------------


class TestCallsKerasBackendSave:
    """KerasBackend.save is called after training with the conventions model path."""

    def test_save_called_once(self, tmp_path: Path) -> None:
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert len(backend.save_calls) == 1

    def test_save_called_with_conventions_model_path(self, tmp_path: Path) -> None:
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        _, saved_path = backend.save_calls[0]
        expected_path = conventions.model_path(output_dir, "speech_to_text")
        assert saved_path == expected_path

    def test_save_called_with_trained_model(self, tmp_path: Path) -> None:
        """The model passed to save is the same object returned by build_ctc_model."""
        n_mels, time_steps = 2, 3
        sentinel_model = object()
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend(model_obj=sentinel_model)
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        saved_model, _ = backend.save_calls[0]
        assert saved_model is sentinel_model

    def test_save_called_after_train(self, tmp_path: Path) -> None:
        """save() is called after train() — ordering verified via call log."""
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        call_log: list[str] = []

        class _OrderTrackingBackend(_RecordingKerasBackend):
            def train(self, model: Any, dataset: Any, epochs: int) -> list[float]:
                call_log.append("train")
                return super().train(model, dataset, epochs)

            def save(self, model: Any, path: Path) -> None:
                call_log.append("save")
                super().save(model, path)

        backend = _OrderTrackingBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert call_log == ["train", "save"]


# ---------------------------------------------------------------------------
# TestReturnsModelPath
# ---------------------------------------------------------------------------


class TestReturnsModelPath:
    """ModelTrainer.train() returns the path from conventions.model_path."""

    def test_returns_conventions_model_path(self, tmp_path: Path) -> None:
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        result = trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        expected = conventions.model_path(output_dir, "speech_to_text")
        assert result == expected

    def test_returns_path_reflecting_output_dir(self, tmp_path: Path) -> None:
        """Returned path is under the supplied output_dir."""
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "my_output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        result = trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert result.parent == output_dir


# ---------------------------------------------------------------------------
# TestBuildCtcModelArgs
# ---------------------------------------------------------------------------


class TestBuildCtcModelArgs:
    """build_ctc_model receives the correct num_classes, n_mels, and time_steps."""

    def test_num_classes_equals_phoneme_count_plus_one(self, tmp_path: Path) -> None:
        """num_classes = len(phoneme_list) + 1 (the +1 is the CTC blank token)."""
        n_mels, time_steps = 2, 3
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        phoneme_list = ["AA", "EH", "IH", "OW"]
        vocab = VocabResult(
            phoneme_list=phoneme_list,
            words_to_phonemes={"TV_ON": ["AA", "EH"]},
            ctc_blank_idx=len(phoneme_list),
        )

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=vocab,
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        assert len(backend.build_ctc_model_calls) == 1
        num_classes, _, _ = backend.build_ctc_model_calls[0]
        # num_classes = len(phoneme_list) + 1 (CTC blank = ctc_blank_idx is already len(phoneme_list))
        assert num_classes == len(phoneme_list) + 1

    def test_n_mels_and_time_steps_passed_to_build(self, tmp_path: Path) -> None:
        n_mels, time_steps = 4, 8
        train_sample = _make_audio_sample("s001")
        train_manifest: Manifest[AudioSample] = Manifest([train_sample])
        spec = _make_spectrogram_sample("s001", n_mels, time_steps)
        tok = _make_token_sample("s001")
        spectrogram_manifest: Manifest[SampleSpectrogram] = Manifest([spec])
        token_manifest: Manifest[SampleTokens] = Manifest([tok])

        spectrogram_dir = tmp_path / "spectrograms"
        token_dir = tmp_path / "tokens"
        output_dir = tmp_path / "output"

        _write_npy(spectrogram_dir / spec.path, n_mels, time_steps)
        _write_json_tokens(token_dir / tok.path)

        backend = _RecordingKerasBackend()
        trainer = ModelTrainer(
            keras_backend=backend,
            n_mels=n_mels,
            time_steps=time_steps,
            epochs=1,
            batch_size=1,
        )

        trainer.train(
            train_manifest=train_manifest,
            vocab=_make_vocab(),
            spectrogram_manifest=spectrogram_manifest,
            token_manifest=token_manifest,
            spectrogram_dir=spectrogram_dir,
            token_dir=token_dir,
            output_dir=output_dir,
        )

        _, called_n_mels, called_time_steps = backend.build_ctc_model_calls[0]
        assert called_n_mels == n_mels
        assert called_time_steps == time_steps


# ---------------------------------------------------------------------------
# TestDefaultKerasBackendImportable
# ---------------------------------------------------------------------------


class TestDefaultKerasBackendImportable:
    """DefaultKerasBackend can be imported and instantiated without TensorFlow installed."""

    def test_can_instantiate_without_tensorflow(self) -> None:
        """Import and instantiate without raising ImportError (TF import is deferred)."""
        # This will fail only if someone adds a module-level TF import
        backend = DefaultKerasBackend()
        assert backend is not None
