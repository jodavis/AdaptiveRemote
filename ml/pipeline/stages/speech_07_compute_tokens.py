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


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Compute phoneme tokens from WAV audio samples"
    )
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--vocab-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    # Reconstruct VocabResult from files written by the vocab stage
    phoneme_list = (args.vocab_dir / "phoneme_list.txt").read_text(encoding="utf-8").splitlines()
    with open(args.vocab_dir / "words_to_phonemes.json", encoding="utf-8") as f:
        words_to_phonemes = json.load(f)
    vocab = VocabResult(
        phoneme_list=phoneme_list,
        words_to_phonemes=words_to_phonemes,
        ctc_blank_idx=len(phoneme_list),
    )

    store = ManifestStore()
    input_manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

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
