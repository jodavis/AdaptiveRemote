"""Unit tests for TokenStage."""

from __future__ import annotations

import asyncio
import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleTokens
from pipeline.intent.vocab_computer import VocabResult
from pipeline.speech.token_stage import TokenStage


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str = "TV_ON_Jenny_r100",
    transcript: str = "TV_ON",
) -> AudioSample:
    content = f"{sample_id}:audio"
    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=content_hash,
        path=Path(f"{sample_id}.wav"),
        parent_content_hash="parent_hash",
        transcript=transcript,
        applied_values={},
    )


def _make_vocab(
    phoneme_list: list[str] | None = None,
    words_to_phonemes: dict[str, list[str]] | None = None,
) -> VocabResult:
    if phoneme_list is None:
        phoneme_list = ["AH", "N", "T", "V"]
    if words_to_phonemes is None:
        words_to_phonemes = {"TV_ON": ["T", "V", "AH", "N"]}
    return VocabResult(
        phoneme_list=phoneme_list,
        words_to_phonemes=words_to_phonemes,
        ctc_blank_idx=len(phoneme_list),
    )


def _make_stage(
    output_dir: Path,
    *,
    vocab: VocabResult | None = None,
    input_token_length: int = 20,
) -> TokenStage:
    if vocab is None:
        vocab = _make_vocab()
    return TokenStage(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        vocab=vocab,
        input_token_length=input_token_length,
    )


# ---------------------------------------------------------------------------
# TestIsDeterministic
# ---------------------------------------------------------------------------


class TestIsDeterministic:
    def test_is_deterministic_true(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        assert stage._is_deterministic is True


# ---------------------------------------------------------------------------
# TestGetAppliedValues
# ---------------------------------------------------------------------------


class TestGetAppliedValues:
    def test_get_applied_values_returns_empty_dict(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample()
        result = stage._get_applied_values(sample, VariationGenerator(0))
        assert result == {}


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_returns_input_sample_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {})
        assert result == "TV_ON_Jenny_r100"

    def test_derive_id_returns_unchanged_id_for_different_samples(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="VOLUME_UP_Aria_r110")
        result = stage._derive_id(sample, {})
        assert result == "VOLUME_UP_Aria_r110"


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_output_json_file_written(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert (tmp_path / f"{sample.id}.json").exists()

    def test_output_json_has_phonemes_key(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        assert "phonemes" in data

    def test_output_json_has_tokens_key(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        assert "tokens" in data

    def test_tokens_padded_to_input_token_length(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path, input_token_length=20)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        assert len(data["tokens"]) == 20

    def test_phonemes_padded_to_input_token_length(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path, input_token_length=20)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        assert len(data["phonemes"]) == 20

    def test_tokens_padding_value_is_ctc_blank_idx(self, tmp_path: Path) -> None:
        """Padding uses vocab.ctc_blank_idx (== len(phoneme_list)), not 0."""
        phoneme_list = ["AH", "N", "T", "V"]
        vocab = _make_vocab(
            phoneme_list=phoneme_list,
            words_to_phonemes={"TV_ON": ["T", "V"]},
        )
        stage = _make_stage(tmp_path, vocab=vocab, input_token_length=10)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        # Only 2 phonemes for "TV_ON"; remaining 8 should equal ctc_blank_idx == 4
        pad_idx = vocab.ctc_blank_idx
        assert data["tokens"][2:] == [pad_idx] * 8

    def test_unknown_transcript_word_raises_key_error(self, tmp_path: Path) -> None:
        """A word not in vocab.words_to_phonemes raises KeyError immediately."""
        vocab = _make_vocab(
            phoneme_list=["AH", "N", "T", "V"],
            words_to_phonemes={"TV_ON": ["T", "V"]},
        )
        stage = _make_stage(tmp_path, vocab=vocab, input_token_length=10)
        sample = _make_audio_sample(transcript="UNKNOWN_WORD")
        with pytest.raises(KeyError, match="UNKNOWN_WORD"):
            asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

    def test_tokens_are_phoneme_indices(self, tmp_path: Path) -> None:
        """Token values are indices into phoneme_list."""
        phoneme_list = ["AH", "N", "T", "V"]
        vocab = _make_vocab(
            phoneme_list=phoneme_list,
            words_to_phonemes={"TV_ON": ["T", "V", "AH", "N"]},
        )
        stage = _make_stage(tmp_path, vocab=vocab, input_token_length=10)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        expected = [phoneme_list.index("T"), phoneme_list.index("V"),
                    phoneme_list.index("AH"), phoneme_list.index("N")]
        assert data["tokens"][:4] == expected

    def test_output_sample_parent_id_equals_input_sample_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].parent_id == "MY_SAMPLE"

    def test_output_sample_id_equals_input_sample_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].id == "MY_SAMPLE"

    def test_output_sample_transcript_preserved(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(transcript="TV_ON")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].transcript == "TV_ON"

    def test_output_sample_is_sample_tokens(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample()
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert isinstance(result.samples[0], SampleTokens)

    def test_phonemes_list_contains_phoneme_strings(self, tmp_path: Path) -> None:
        phoneme_list = ["AH", "N", "T", "V"]
        vocab = _make_vocab(
            phoneme_list=phoneme_list,
            words_to_phonemes={"TV_ON": ["T", "V"]},
        )
        stage = _make_stage(tmp_path, vocab=vocab, input_token_length=5)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        assert data["phonemes"][0] == "T"
        assert data["phonemes"][1] == "V"

    def test_phonemes_padding_is_empty_string(self, tmp_path: Path) -> None:
        """Phoneme padding uses empty string for slots after the actual phonemes."""
        vocab = _make_vocab(
            phoneme_list=["AH", "N", "T", "V"],
            words_to_phonemes={"TV_ON": ["T", "V"]},
        )
        stage = _make_stage(tmp_path, vocab=vocab, input_token_length=5)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        # slots 2,3,4 are padding
        assert data["phonemes"][2:] == ["", "", ""]

    def test_multi_word_transcript_concatenates_phonemes(self, tmp_path: Path) -> None:
        phoneme_list = ["AH", "N", "T", "V", "AH0", "P"]
        vocab = _make_vocab(
            phoneme_list=phoneme_list,
            words_to_phonemes={
                "TV_ON": ["T", "V"],
                "VOLUME_UP": ["V", "AH0", "P"],
            },
        )
        stage = _make_stage(tmp_path, vocab=vocab, input_token_length=10)
        sample = _make_audio_sample(transcript="TV_ON VOLUME_UP")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        data = json.loads((tmp_path / f"{sample.id}.json").read_text())
        # TV_ON=[T,V] + VOLUME_UP=[V,AH0,P] = 5 phonemes
        assert data["phonemes"][:5] == ["T", "V", "V", "AH0", "P"]
