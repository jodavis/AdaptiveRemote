"""Unit tests for PhraseVariator."""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path
from typing import Callable

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import TextSample
from pipeline.intent.phrase_variator import PhraseVariator


_BASE_PHRASES: list[tuple[str, str]] = [
    ("turn on the TV", "TV_ON"),
    ("turn off the TV", "TV_OFF"),
    ("volume up", "VOLUME_UP"),
]

_DEFAULT_CHANCES = {
    "repeat_modifier_chance": 0.3,
    "pleasantry_chance": 0.3,
    "hesitation_chance": 0.3,
    "spelling_variant_chance": 0.1,
    "case_variant_chance": 0.2,
}


def _factory(seed: int) -> VariationGenerator:
    return VariationGenerator(seed)


def _make_variator(**overrides) -> PhraseVariator:
    chances = {**_DEFAULT_CHANCES, **overrides}
    return PhraseVariator(
        vgen_factory=_factory,
        **chances,
    )


class TestPhraseVariatorDeterminism:
    def test_same_factory_and_params_produce_same_output(self) -> None:
        v1 = _make_variator().generate(_BASE_PHRASES, 5)
        v2 = _make_variator().generate(_BASE_PHRASES, 5)
        assert [s.content for s in v1] == [s.content for s in v2]
        assert [s.label for s in v1] == [s.label for s in v2]

    def test_different_factories_with_different_state_produce_different_output(self) -> None:
        """Two factories that return generators with offset seeds produce different output."""
        def factory_a(seed: int) -> VariationGenerator:
            return VariationGenerator(seed)

        def factory_b(seed: int) -> VariationGenerator:
            return VariationGenerator(seed + 999_999)

        v1 = PhraseVariator(vgen_factory=factory_a, **_DEFAULT_CHANCES).generate(_BASE_PHRASES, 10)
        v2 = PhraseVariator(vgen_factory=factory_b, **_DEFAULT_CHANCES).generate(_BASE_PHRASES, 10)
        contents1 = [s.content for s in v1]
        contents2 = [s.content for s in v2]
        assert contents1 != contents2


class TestPhraseVariatorOutput:
    def test_returns_text_samples(self) -> None:
        variants = _make_variator().generate(_BASE_PHRASES, 3)
        assert all(isinstance(s, TextSample) for s in variants)

    def test_labels_match_commands(self) -> None:
        variants = _make_variator().generate(_BASE_PHRASES, 3)
        for sample in variants:
            assert sample.label in {c for _, c in _BASE_PHRASES}

    def test_seed_is_zero(self) -> None:
        variants = _make_variator().generate(_BASE_PHRASES, 3)
        assert all(s.seed == 0 for s in variants)

    def test_content_hash_matches_content(self) -> None:
        variants = _make_variator().generate(_BASE_PHRASES, 3)
        for s in variants:
            expected = hashlib.sha256(s.content.encode("utf-8")).hexdigest()
            assert s.content_hash == expected

    def test_generate_produces_up_to_variations_per_phrase(self) -> None:
        variants = _make_variator().generate(_BASE_PHRASES, 5)
        # Each phrase attempts 5 variants, sanity check may reject some — count should be <= 15
        assert len(variants) <= len(_BASE_PHRASES) * 5

    def test_content_is_non_empty_string(self) -> None:
        variants = _make_variator().generate(_BASE_PHRASES, 5)
        assert all(isinstance(s.content, str) and len(s.content) > 0 for s in variants)


class TestSanityCheck:
    def _make_var(self, surface: str, base: str = "turn on the TV", canonical: str = "TV_ON") -> dict:
        return {
            "surface_form": surface,
            "base_phrase": base,
            "canonical_label": canonical,
            "transformations": "none",
            "repeat_count": 1,
        }

    def test_valid_variant_passes(self) -> None:
        variator = _make_variator()
        var = self._make_var("turn on the TV")
        valid, issues = variator.sanity_check([var])
        assert len(valid) == 1
        assert len(issues) == 0

    def test_empty_surface_form_rejected(self) -> None:
        variator = _make_variator()
        var = self._make_var("")
        valid, issues = variator.sanity_check([var])
        assert len(valid) == 0
        assert len(issues) == 1

    def test_too_short_surface_form_rejected(self) -> None:
        variator = _make_variator()
        var = self._make_var("x")
        valid, issues = variator.sanity_check([var])
        assert len(valid) == 0

    def test_no_letters_rejected(self) -> None:
        variator = _make_variator()
        var = self._make_var("123 456")
        valid, issues = variator.sanity_check([var])
        assert len(valid) == 0
        assert any("No letters" in i for i in issues)

    def test_base_phrase_not_recognizable_rejected(self) -> None:
        variator = _make_variator()
        # Surface has completely different tokens from base
        var = self._make_var("completely different xyz words", base="turn on the TV")
        valid, issues = variator.sanity_check([var])
        assert len(valid) == 0
        assert any("not recognizable" in i for i in issues)

    def test_too_long_surface_form_rejected(self) -> None:
        variator = _make_variator()
        long_surface = "turn " + "on " * 100
        var = self._make_var(long_surface)
        valid, issues = variator.sanity_check([var])
        assert len(valid) == 0


class TestPhraseVariatorMatchesOriginal:
    """Verify output is stable across runs."""

    def test_snapshot_determinism(self) -> None:
        """Spot-check that the first variant for a known phrase is stable across runs."""
        phrases = [("turn on the TV", "TV_ON")]
        variants = _make_variator().generate(phrases, 1)
        variants2 = _make_variator().generate(phrases, 1)
        if variants and variants2:
            assert variants[0].content == variants2[0].content
