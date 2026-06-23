from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.intent.vocab_computer import VocabResult
from pipeline.speech.model_trainer import DefaultKerasBackend, ModelTrainer
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


def _load_vocab(vocab_dir: Path) -> VocabResult:
    """Reconstruct VocabResult from phoneme_list.txt and words_to_phonemes.json."""
    phoneme_list = (vocab_dir / "phoneme_list.txt").read_text(encoding="utf-8").splitlines()
    with open(vocab_dir / "words_to_phonemes.json", encoding="utf-8") as f:
        words_to_phonemes: dict[str, list[str]] = json.load(f)
    ctc_blank_idx = len(phoneme_list)
    return VocabResult(
        phoneme_list=phoneme_list,
        words_to_phonemes=words_to_phonemes,
        ctc_blank_idx=ctc_blank_idx,
    )


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Train a CTC speech-to-text model from featurised manifests"
    )
    parser.add_argument("--train-manifest-dir", required=True, type=Path)
    parser.add_argument("--spectrogram-manifest-dir", required=True, type=Path)
    parser.add_argument("--token-manifest-dir", required=True, type=Path)
    parser.add_argument("--vocab-dir", required=True, type=Path)
    parser.add_argument("--spectrogram-dir", required=True, type=Path)
    parser.add_argument("--token-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    store = ManifestStore()
    train_manifest = store.read(conventions.split_manifest_path(args.train_manifest_dir, "train"))
    spectrogram_manifest = store.read(conventions.manifest_path(args.spectrogram_manifest_dir))
    token_manifest = store.read(conventions.manifest_path(args.token_manifest_dir))

    vocab = _load_vocab(args.vocab_dir)

    args.output_dir.mkdir(parents=True, exist_ok=True)

    trainer = ModelTrainer(
        keras_backend=DefaultKerasBackend(),
        n_mels=params.compute_spectrograms.n_mels,
        time_steps=params.compute_spectrograms.time_steps,
        epochs=params.train_model.epochs,
        batch_size=params.train_model.batch_size,
    )

    model_path = trainer.train(
        train_manifest=train_manifest,
        vocab=vocab,
        spectrogram_manifest=spectrogram_manifest,
        token_manifest=token_manifest,
        spectrogram_dir=args.spectrogram_dir,
        token_dir=args.token_dir,
        output_dir=args.output_dir,
    )

    print(f"Model saved to: {model_path}")


if __name__ == "__main__":
    main()
