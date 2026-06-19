"""TokenStage: compute phoneme tokens from AudioSample transcripts.

Deterministic stage (seed=0). Writes {id}.json with phonemes and tokens
padded to input_token_length. Token index 0 is used for padding.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleTokens
from pipeline.intent.vocab_computer import VocabResult
from pipeline.stages import conventions


class TokenStage(ModifierStage[AudioSample, SampleTokens]):
    """Compute phoneme tokens from AudioSample transcripts.

    Writes {id}.json with:
      phonemes: list[str] — phoneme strings padded with "" to input_token_length
      tokens:   list[int] — phoneme indices padded with 0 to input_token_length

    Token index 0 is used for padding (index into phoneme_list).
    """

    _is_deterministic: bool = True

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        vocab: VocabResult,
        input_token_length: int,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._vocab = vocab
        self._input_token_length = input_token_length

    def _get_applied_values(
        self, sample: AudioSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        return {}

    def _derive_id(self, input_sample: AudioSample, applied_values: dict[str, Any]) -> str:
        return input_sample.id

    async def _generate_output(
        self,
        input_sample: AudioSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> SampleTokens:
        # Collect phonemes for each word in the transcript
        phonemes: list[str] = []
        for word in input_sample.transcript.split():
            word_phonemes = self._vocab.words_to_phonemes.get(word, [])
            phonemes.extend(word_phonemes)

        # Compute token indices (1-based lookup into phoneme_list)
        tokens = [self._vocab.phoneme_list.index(p) for p in phonemes]

        # Pad to input_token_length
        pad_len = max(0, self._input_token_length - len(phonemes))
        padded_phonemes = phonemes + [""] * pad_len
        padded_tokens = tokens + [0] * pad_len

        # Truncate if longer than input_token_length
        padded_phonemes = padded_phonemes[: self._input_token_length]
        padded_tokens = padded_tokens[: self._input_token_length]

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "json")
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump({"phonemes": padded_phonemes, "tokens": padded_tokens}, f)

        content_hash = self._compute_content_hash(
            parent_content_hash, output_seed, applied_values
        )

        return SampleTokens(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.json"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.transcript,
            parent_id=input_sample.id,
        )
