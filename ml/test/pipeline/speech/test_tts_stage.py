"""Unit tests for TtsSampleGenerator."""

from __future__ import annotations

import asyncio
import hashlib
import sys
from pathlib import Path
from typing import Any

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, TextSample
from pipeline.speech.tts_stage import TtsSampleGenerator


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_text_sample(
    content: str = "turn on the tv", label: str = "TV_ON"
) -> TextSample:
    h = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return TextSample(seed=0, content_hash=h, content=content, label=label)


def _content_hash(parent: str, seed: int, av: dict[str, Any]) -> str:
    return ModifierStage._compute_content_hash(parent, seed, av)


class _RecordingTtsProvider:
    """Stub TtsProvider that records synthesize() calls and writes dummy file bytes."""

    def __init__(self) -> None:
        self.calls: list[tuple[str, str, str, Path]] = []

    async def synthesize(
        self, text: str, voice: str, rate: str, output_path: Path
    ) -> None:
        self.calls.append((text, voice, rate, output_path))
        output_path.write_bytes(b"dummy_wav")


def _make_stage(
    output_dir: Path,
    *,
    voices: list[str] | None = None,
    speech_rate_min: int = -10,
    speech_rate_max: int = 20,
    tts_provider: _RecordingTtsProvider | None = None,
) -> tuple[TtsSampleGenerator, _RecordingTtsProvider]:
    if voices is None:
        voices = ["en-US-JennyNeural", "en-US-AriaNeural"]
    if tts_provider is None:
        tts_provider = _RecordingTtsProvider()
    stage = TtsSampleGenerator(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        tts_provider=tts_provider,
        voices=voices,
        speech_rate_min=speech_rate_min,
        speech_rate_max=speech_rate_max,
    )
    return stage, tts_provider


# ---------------------------------------------------------------------------
# TestAppliedValues
# ---------------------------------------------------------------------------


class TestAppliedValues:
    def test_applied_values_has_voice_key_as_str(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_text_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["voice"], str)

    def test_applied_values_has_speech_rate_key_as_int(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_text_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["speech_rate"], int)

    def test_applied_values_has_exactly_two_keys(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_text_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert set(av.keys()) == {"voice", "speech_rate"}

    def test_voice_is_selected_from_voices_list(self, tmp_path: Path) -> None:
        voices = ["en-US-JennyNeural", "en-US-AriaNeural"]
        stage, _ = _make_stage(tmp_path, voices=voices)
        sample = _make_text_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["voice"] in voices

    def test_speech_rate_within_bounds(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, speech_rate_min=-5, speech_rate_max=10)
        sample = _make_text_sample()
        for seed in range(20):
            av = stage._get_applied_values(sample, VariationGenerator(seed))
            assert -5 <= av["speech_rate"] <= 10

    def test_speech_rate_is_not_a_formatted_string(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_text_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        # speech_rate is a raw int — no "%" in its value
        assert "%" not in str(av["speech_rate"])

    def test_same_seed_produces_same_applied_values(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path)
        sample = _make_text_sample()
        av1 = stage._get_applied_values(sample, VariationGenerator(99))
        av2 = stage._get_applied_values(sample, VariationGenerator(99))
        assert av1 == av2

    def test_different_seeds_may_produce_different_applied_values(
        self, tmp_path: Path
    ) -> None:
        stage, _ = _make_stage(
            tmp_path, voices=["en-US-JennyNeural", "en-US-AriaNeural"]
        )
        sample = _make_text_sample()
        results = {
            str(stage._get_applied_values(sample, VariationGenerator(s)))
            for s in range(50)
        }
        # With two voices and a range of rates, should see variation
        assert len(results) > 1


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_uses_label_as_prefix(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(label="TV_ON")
        result = stage._derive_id(sample, {"voice": "en-US-JennyNeural", "speech_rate": 0})
        assert result.startswith("TV_ON_")

    def test_derive_id_strips_neural_from_voice_short(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(label="TV_ON")
        result = stage._derive_id(sample, {"voice": "en-US-JennyNeural", "speech_rate": 0})
        assert "Neural" not in result

    def test_derive_id_formats_rate_plus_100(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(label="TV_ON")
        result = stage._derive_id(sample, {"voice": "en-US-JennyNeural", "speech_rate": -23})
        assert result == "TV_ON_Jenny_r77"

    def test_derive_id_positive_rate(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-AriaNeural"])
        sample = _make_text_sample(label="VOLUME_UP")
        result = stage._derive_id(sample, {"voice": "en-US-AriaNeural", "speech_rate": 10})
        assert result == "VOLUME_UP_Aria_r110"

    def test_derive_id_uses_label_not_content(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(
            content="um please turn on the tv", label="TV_ON"
        )
        result = stage._derive_id(sample, {"voice": "en-US-JennyNeural", "speech_rate": 0})
        assert result.startswith("TV_ON_")
        assert "um" not in result
        assert "please" not in result

    def test_derive_id_zero_rate_gives_r100(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(label="TV_ON")
        result = stage._derive_id(sample, {"voice": "en-US-JennyNeural", "speech_rate": 0})
        assert result == "TV_ON_Jenny_r100"


# ---------------------------------------------------------------------------
# TestTransformTtsProviderCalls
# ---------------------------------------------------------------------------


class TestTransformTtsProviderCalls:
    def test_tts_provider_called_with_input_content(self, tmp_path: Path) -> None:
        stage, provider = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(content="please turn on the tv", label="TV_ON")

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        assert len(provider.calls) == 1
        text, _voice, _rate, _path = provider.calls[0]
        assert text == "please turn on the tv"

    def test_tts_provider_called_with_correct_voice(self, tmp_path: Path) -> None:
        stage, provider = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample()

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _text, voice, _rate, _path = provider.calls[0]
        assert voice == "en-US-JennyNeural"

    def test_rate_string_formatted_as_plus_sign_percent(self, tmp_path: Path) -> None:
        # Force speech_rate=5 by setting min==max
        stage, provider = _make_stage(
            tmp_path,
            voices=["en-US-JennyNeural"],
            speech_rate_min=5,
            speech_rate_max=5,
        )
        sample = _make_text_sample()

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _text, _voice, rate, _path = provider.calls[0]
        assert rate == "+5%"

    def test_rate_string_negative_rate_formatted_correctly(
        self, tmp_path: Path
    ) -> None:
        stage, provider = _make_stage(
            tmp_path,
            voices=["en-US-JennyNeural"],
            speech_rate_min=-5,
            speech_rate_max=-5,
        )
        sample = _make_text_sample()

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _text, _voice, rate, _path = provider.calls[0]
        assert rate == "-5%"

    def test_rate_string_zero_formatted_as_plus_zero(self, tmp_path: Path) -> None:
        stage, provider = _make_stage(
            tmp_path,
            voices=["en-US-JennyNeural"],
            speech_rate_min=0,
            speech_rate_max=0,
        )
        sample = _make_text_sample()

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))

        _text, _voice, rate, _path = provider.calls[0]
        assert rate == "+0%"


# ---------------------------------------------------------------------------
# TestTransformAudioSampleFields
# ---------------------------------------------------------------------------


class TestTransformAudioSampleFields:
    def test_audio_sample_transcript_equals_input_label(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(content="um please turn on the tv", label="TV_ON")

        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        assert result.samples[0].transcript == "TV_ON"

    def test_audio_sample_transcript_not_content(self, tmp_path: Path) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample(content="um please turn on the tv", label="TV_ON")

        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        assert result.samples[0].transcript != sample.content

    def test_applied_values_in_output_contains_int_speech_rate(
        self, tmp_path: Path
    ) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample()

        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        av = result.samples[0].applied_values
        assert isinstance(av["speech_rate"], int)
        assert isinstance(av["voice"], str)

    def test_applied_values_does_not_contain_rate_string(
        self, tmp_path: Path
    ) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample()

        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        av = result.samples[0].applied_values
        # No formatted rate string stored — only raw int
        for value in av.values():
            assert "%" not in str(value)


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------


class TestSkipPath:
    def test_skip_path_does_not_call_tts_provider(self, tmp_path: Path) -> None:
        stage, provider = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample()

        # First run — generates
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        initial_call_count = len(provider.calls)
        assert initial_call_count == 1

        # Second run — same input, same constraints → skip
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(provider.calls) == initial_call_count

    def test_skip_path_preserves_audio_sample_unchanged(
        self, tmp_path: Path
    ) -> None:
        stage, _ = _make_stage(tmp_path, voices=["en-US-JennyNeural"])
        sample = _make_text_sample()

        result1 = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )
        result2 = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )

        s1 = result1.samples[0]
        s2 = result2.samples[0]
        assert s2.id == s1.id
        assert s2.seed == s1.seed
        assert s2.content_hash == s1.content_hash
