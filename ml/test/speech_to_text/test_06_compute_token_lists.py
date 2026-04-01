"""Tests for compute_tokens() in 06_compute_token_lists.py."""
import importlib.util
import sys
import types
from pathlib import Path

import pytest

from shared import constants

# ---------------------------------------------------------------------------
# Module loading — digit-prefixed filename requires importlib
# ---------------------------------------------------------------------------

SCRIPT_PATH = (
    Path(__file__).parent.parent.parent
    / "scripts/speech_to_text/06_compute_token_lists.py"
)


def _load_module():
    spec = importlib.util.spec_from_file_location("script_06", SCRIPT_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_mod = _load_module()
compute_tokens = _mod.compute_tokens

# Minimal vocab and mapping for tests
VOCAB = ['AH', 'B', 'K', 'G', 'OW', 'AE']  # indices 0-5; pad = 6
W2P = {
    'GO':   ['G', 'OW'],
    'BACK': ['B', 'AE', 'K'],
}
PAD = len(VOCAB)  # 6


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

def test_known_word_produces_correct_phonemes():
    phonemes, tokens = compute_tokens(VOCAB, W2P, "back")
    assert phonemes == ['B', 'AE', 'K']


def test_unknown_word_is_skipped():
    phonemes, tokens = compute_tokens(VOCAB, W2P, "hello")
    assert phonemes == []


def test_tokens_padded_to_input_token_length():
    phonemes, tokens = compute_tokens(VOCAB, W2P, "go")  # 2 phonemes
    assert len(tokens) == constants.INPUT_TOKEN_LENGTH
    # Remainder should be pad value
    assert all(t == PAD for t in tokens[2:])


def test_tokens_truncated_at_input_token_length():
    # Build a transcription that maps to more than INPUT_TOKEN_LENGTH phonemes
    long_vocab = [chr(ord('A') + i) for i in range(30)]
    long_w2p = {'LONG': long_vocab}
    phonemes, tokens = compute_tokens(long_vocab, long_w2p, "long")
    assert len(tokens) == constants.INPUT_TOKEN_LENGTH


def test_comma_treated_as_space():
    phonemes, tokens = compute_tokens(VOCAB, W2P, "go,back")
    assert phonemes == ['G', 'OW', 'B', 'AE', 'K']


def test_case_insensitive():
    phonemes_lower, _ = compute_tokens(VOCAB, W2P, "go back")
    phonemes_upper, _ = compute_tokens(VOCAB, W2P, "GO BACK")
    assert phonemes_lower == phonemes_upper


def test_empty_transcription_all_padding():
    phonemes, tokens = compute_tokens(VOCAB, W2P, "")
    assert phonemes == []
    assert len(tokens) == constants.INPUT_TOKEN_LENGTH
    assert all(t == PAD for t in tokens)
