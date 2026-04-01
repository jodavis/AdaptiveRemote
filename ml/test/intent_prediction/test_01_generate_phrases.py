"""
Tests for VariationGenerator helpers in 01_generate_phrases.py.

The script has a proper if __name__ == "__main__" guard so it's safe to import
directly; no importlib gymnastics needed.
"""
import importlib.util
import sys
from pathlib import Path

import pytest

# ---------------------------------------------------------------------------
# Module loading
# ---------------------------------------------------------------------------

SCRIPT_PATH = (
    Path(__file__).parent.parent.parent
    / "scripts/intent_prediction/01_generate_phrases.py"
)


def _load_module():
    spec = importlib.util.spec_from_file_location("script_01_phrases", SCRIPT_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_mod = _load_module()
VariationGenerator = _mod.VariationGenerator


# ---------------------------------------------------------------------------
# _normalize_commas_and_whitespace
# ---------------------------------------------------------------------------

@pytest.mark.parametrize("text, expected", [
    # Consecutive commas collapsed; surrounding whitespace consumed by regex
    ("hello , , world", "hello,world"),
    # Leading comma removed
    (", hello", "hello"),
    # Trailing comma removed
    ("hello,", "hello"),
    # Extra whitespace collapsed
    ("hello   world", "hello world"),
    # Space before comma removed
    ("hello , world", "hello, world"),
    # Empty string passes through
    ("", ""),
    # Normal text unchanged
    ("go back", "go back"),
])
def test_normalize_commas_and_whitespace(text, expected):
    gen = VariationGenerator.__new__(VariationGenerator)
    gen.generated = set()
    gen.existing_variations = []
    result = gen._normalize_commas_and_whitespace(text)
    assert result == expected


# ---------------------------------------------------------------------------
# _tokenize
# ---------------------------------------------------------------------------

def test_tokenize_alphanumeric_splitting():
    gen = VariationGenerator.__new__(VariationGenerator)
    result = gen._tokenize("Hello, World! 42")
    assert result == ['hello', 'world', '42']


def test_tokenize_empty_string():
    gen = VariationGenerator.__new__(VariationGenerator)
    assert gen._tokenize("") == []


# ---------------------------------------------------------------------------
# sanity_check
# ---------------------------------------------------------------------------

def _make_var(surface, canonical, base, transformations="none"):
    return {
        'surface_form': surface,
        'canonical_label': canonical,
        'base_phrase': base,
        'transformations': transformations,
    }


def test_sanity_check_accepts_valid_variation():
    gen = VariationGenerator.__new__(VariationGenerator)
    gen.generated = set()
    gen.existing_variations = []
    var = _make_var("go back", "Back", "go back")
    valid, issues = gen.sanity_check([var])
    assert len(valid) == 1
    assert issues == []


def test_sanity_check_rejects_empty_surface():
    gen = VariationGenerator.__new__(VariationGenerator)
    gen.generated = set()
    gen.existing_variations = []
    var = _make_var("", "Back", "go back")
    valid, issues = gen.sanity_check([var])
    assert valid == []
    assert len(issues) == 1


def test_sanity_check_rejects_single_char():
    gen = VariationGenerator.__new__(VariationGenerator)
    gen.generated = set()
    gen.existing_variations = []
    var = _make_var("g", "Back", "go back")
    valid, issues = gen.sanity_check([var])
    assert valid == []


def test_sanity_check_rejects_no_letters():
    gen = VariationGenerator.__new__(VariationGenerator)
    gen.generated = set()
    gen.existing_variations = []
    var = _make_var("123", "Back", "go back")
    valid, issues = gen.sanity_check([var])
    assert valid == []


def test_sanity_check_rejects_disjoint_tokens():
    gen = VariationGenerator.__new__(VariationGenerator)
    gen.generated = set()
    gen.existing_variations = []
    # base_phrase is "go back" but surface_form shares no tokens
    var = _make_var("volume up please", "Back", "go back")
    valid, issues = gen.sanity_check([var])
    assert valid == []
    assert any("not recognizable" in i for i in issues)
