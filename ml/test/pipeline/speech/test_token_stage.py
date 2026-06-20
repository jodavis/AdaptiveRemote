"""Unit tests for TokenStage."""

from __future__ import annotations

import asyncio
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.sample import AudioSample
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
        phoneme_list = ["AA", "EH", "IH", "OW", "UH"]
    if words_to_phonemes is None:
        # TV_ON => T V AX N, mapped to phonemes in phoneme_list
        words_to_phonemes = {
            "TV_ON": ["AA", "EH"],
            "MUTE": ["IH", "OW"],
        }
    return VocabResult(
        phoneme_list=phoneme_list,
        words_to_phonemes=words_to_phonemes,
        ctc_blank_idx=len(phoneme_list),
    )


def _make_stage(
    output_dir: Path,
    *,
    vocab: VocabResult | None = None,
    input_token_length: int = 10,
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
    def test_is_deterministic_class_var_is_true(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        assert stage._is_deterministic is True

    def test_is_deterministic_is_class_variable(self) -> None:
        assert TokenStage._is_deterministic is True


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_returns_input_sample_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {})
        assert result == "TV_ON_Jenny_r100"

    def test_derive_id_returns_input_sample_id_different_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MUTE_Bob_r95")
        result = stage._derive_id(sample, {})
        assert result == "MUTE_Bob_r95"

    def test_derive_id_ignores_applied_values(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = stage._derive_id(sample, {"some_key": "some_value"})
        assert result == "MY_SAMPLE"


# ---------------------------------------------------------------------------
# TestGetAppliedValues
# ---------------------------------------------------------------------------


class TestGetAppliedValues:
    def test_get_applied_values_returns_empty_dict(self, tmp_path: Path) -> None:
        from pipeline.core.randomization import VariationGenerator

        stage = _make_stage(tmp_path)
        sample = _make_audio_sample()
        result = stage._get_applied_values(sample, VariationGenerator(0))
        assert result == {}


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_parent_id_equals_input_sample_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100", transcript="TV_ON")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].parent_id == "TV_ON_Jenny_r100"

    def test_parent_id_equals_input_sample_id_different_sample(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MUTE_Bob_r95", transcript="MUTE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].parent_id == "MUTE_Bob_r95"

    def test_output_json_has_phonemes_key(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path, input_token_length=10)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "TV_ON_Jenny_r100.json"
        assert json_path.exists()
        data = json.loads(json_path.read_text(encoding="utf-8"))
        assert "phonemes" in data

    def test_output_json_has_tokens_key(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path, input_token_length=10)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "TV_ON_Jenny_r100.json"
        data = json.loads(json_path.read_text(encoding="utf-8"))
        assert "tokens" in data

    def test_tokens_padded_to_input_token_length(self, tmp_path: Path) -> None:
        """Tokens list is exactly input_token_length long."""
        input_token_length = 10
        stage = _make_stage(tmp_path, input_token_length=input_token_length)
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "TV_ON_Jenny_r100.json"
        data = json.loads(json_path.read_text(encoding="utf-8"))
        assert len(data["tokens"]) == input_token_length

    def test_tokens_truncated_to_input_token_length(self, tmp_path: Path) -> None:
        """When phonemes exceed input_token_length, tokens are truncated to length."""
        phoneme_list = ["A", "B", "C", "D", "E", "F", "G", "H"]
        words_to_phonemes = {"LONG": ["A", "B", "C", "D", "E", "F", "G", "H"]}
        vocab = VocabResult(
            phoneme_list=phoneme_list,
            words_to_phonemes=words_to_phonemes,
            ctc_blank_idx=len(phoneme_list),
        )
        input_token_length = 4
        stage = TokenStage(
            output_dir=tmp_path,
            manifest_store=ManifestStore(),
            vocab=vocab,
            input_token_length=input_token_length,
        )
        sample = _make_audio_sample(sample_id="LONG_Jenny_r100", transcript="LONG")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "LONG_Jenny_r100.json"
        data = json.loads(json_path.read_text(encoding="utf-8"))
        assert len(data["tokens"]) == input_token_length

    def test_tokens_padding_uses_ctc_blank_idx(self, tmp_path: Path) -> None:
        """Padding tokens use ctc_blank_idx (= len(phoneme_list))."""
        phoneme_list = ["AA", "EH"]
        words_to_phonemes = {"TV_ON": ["AA"]}  # Only 1 phoneme
        vocab = VocabResult(
            phoneme_list=phoneme_list,
            words_to_phonemes=words_to_phonemes,
            ctc_blank_idx=len(phoneme_list),
        )
        input_token_length = 5
        stage = TokenStage(
            output_dir=tmp_path,
            manifest_store=ManifestStore(),
            vocab=vocab,
            input_token_length=input_token_length,
        )
        sample = _make_audio_sample(transcript="TV_ON")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "TV_ON_Jenny_r100.json"
        data = json.loads(json_path.read_text(encoding="utf-8"))
        # Token 0 = index of "AA" = 0; tokens 1..4 = ctc_blank_idx = 2
        assert data["tokens"][0] == 0
        assert data["tokens"][1:] == [2, 2, 2, 2]

    def test_missing_word_is_skipped(self, tmp_path: Path) -> None:
        """Words absent from words_to_phonemes are silently skipped."""
        phoneme_list = ["AA", "EH"]
        words_to_phonemes = {"KNOWN": ["AA"]}
        vocab = VocabResult(
            phoneme_list=phoneme_list,
            words_to_phonemes=words_to_phonemes,
            ctc_blank_idx=len(phoneme_list),
        )
        input_token_length = 5
        stage = TokenStage(
            output_dir=tmp_path,
            manifest_store=ManifestStore(),
            vocab=vocab,
            input_token_length=input_token_length,
        )
        # UNKNOWN not in words_to_phonemes — should be skipped
        sample = _make_audio_sample(transcript="UNKNOWN KNOWN")
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "TV_ON_Jenny_r100.json"
        data = json.loads(json_path.read_text(encoding="utf-8"))
        # Only "KNOWN" contributes 1 phoneme ("AA" = index 0); rest padded
        assert data["tokens"][0] == 0
        assert data["tokens"][1:] == [2, 2, 2, 2]

    def test_output_sample_id_equals_input_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].id == "TV_ON_Jenny_r100"

    def test_transcript_preserved(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(transcript="CHANNEL_UP")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].transcript == "CHANNEL_UP"

    def test_output_file_has_json_extension(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].path == Path("MY_SAMPLE.json")


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------


class TestSkipPath:
    def test_skip_path_does_not_write_json_on_second_run(self, tmp_path: Path) -> None:
        """Second transform() call does not regenerate JSON (skip-unchanged)."""
        stage = _make_stage(tmp_path, input_token_length=10)
        sample = _make_audio_sample(transcript="TV_ON")

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        json_path = tmp_path / "TV_ON_Jenny_r100.json"
        # Record mtime after first run
        mtime_after_first = json_path.stat().st_mtime

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        mtime_after_second = json_path.stat().st_mtime
        # File should not have been rewritten
        assert mtime_after_second == mtime_after_first

    def test_skip_path_preserves_output_sample_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path, input_token_length=10)
        sample = _make_audio_sample(sample_id="MY_SAMPLE", transcript="TV_ON")

        result1 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        result2 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result2.samples[0].id == result1.samples[0].id

    def test_skip_path_preserves_parent_id(self, tmp_path: Path) -> None:
        stage = _make_stage(tmp_path, input_token_length=10)
        sample = _make_audio_sample(sample_id="MY_SAMPLE", transcript="TV_ON")

        result1 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        result2 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result2.samples[0].parent_id == result1.samples[0].parent_id
