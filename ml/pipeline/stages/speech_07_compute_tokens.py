from __future__ import annotations

import argparse
import asyncio
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.intent.vocab_computer import VocabResult
from pipeline.speech.token_stage import TokenStage
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
        description="Convert AudioSample transcripts to phoneme token sequences"
    )
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--vocab-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    store = ManifestStore()
    input_manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

    vocab = _load_vocab(args.vocab_dir)

    args.output_dir.mkdir(parents=True, exist_ok=True)

    stage = TokenStage(
        output_dir=args.output_dir,
        manifest_store=store,
        vocab=vocab,
        input_token_length=params.compute_tokens.input_token_length,
    )

    asyncio.run(
        stage.transform(
            input_manifest,
            conventions.manifest_path(args.output_dir),
        )
    )


if __name__ == "__main__":
    main()
