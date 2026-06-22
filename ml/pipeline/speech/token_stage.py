"""TokenStage: convert AudioSample transcripts to phoneme token sequences.

Each AudioSample is transformed into a SampleTokens whose .json file contains
{"phonemes": [...], "tokens": [...]} padded or truncated to input_token_length.

The stage is deterministic (_is_deterministic = True) so output_seed is always
0 and _get_applied_values returns an empty dict.

Token lookup for missing words follows the skip-on-missing behaviour from the
legacy script: words absent from VocabResult.words_to_phonemes are silently
skipped (no error, no contribution to the phoneme sequence).
"""

from __future__ import annotations

import asyncio
import json
from pathlib import Path
from typing import Any, ClassVar

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleTokens
from pipeline.intent.vocab_computer import VocabResult
from pipeline.stages import conventions


class TokenStage(ModifierStage[AudioSample, SampleTokens]):
    """Deterministic stage that converts transcripts to padded phoneme token sequences.

    Output: {id}.json with {"phonemes": [...], "tokens": [...]} where tokens is
    exactly input_token_length long (padded with ctc_blank_idx or truncated).

    A phoneme_to_idx dict is built at construction time from VocabResult.phoneme_list
    for O(1) per-phoneme lookup.

    Words absent from VocabResult.words_to_phonemes are silently skipped.
    """

    _is_deterministic: ClassVar[bool] = True

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
        self._phoneme_to_idx: dict[str, int] = {
            ph: idx for idx, ph in enumerate(vocab.phoneme_list)
        }

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
        phonemes, tokens = self._compute_tokens(input_sample.transcript)

        loop = asyncio.get_running_loop()
        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "json")
        payload = {"phonemes": phonemes, "tokens": tokens}
        await loop.run_in_executor(
            None,
            lambda: output_path.write_text(json.dumps(payload), encoding="utf-8"),
        )

        content_hash = self._compute_content_hash(parent_content_hash, output_seed, applied_values)

        return SampleTokens(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.json"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.transcript,
            parent_id=input_sample.id,
        )

    def _compute_tokens(self, transcript: str) -> tuple[list[str], list[int]]:
        """Convert transcript to (phonemes, padded_tokens).

        Words absent from words_to_phonemes are silently skipped.
        Tokens list is padded to input_token_length with ctc_blank_idx,
        or truncated from the right if longer.
        """
        words = transcript.upper().replace(",", " ").split()
        phonemes: list[str] = []
        for word in words:
            if word in self._vocab.words_to_phonemes:
                phonemes.extend(self._vocab.words_to_phonemes[word])

        tokens = [self._phoneme_to_idx[ph] for ph in phonemes if ph in self._phoneme_to_idx]

        if len(tokens) < self._input_token_length:
            pad_count = self._input_token_length - len(tokens)
            tokens = tokens + [self._vocab.ctc_blank_idx] * pad_count
        else:
            tokens = tokens[: self._input_token_length]

        return phonemes, tokens
