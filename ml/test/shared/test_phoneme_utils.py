"""Tests for shared.phoneme_utils using real cmudict-format files in tmp_path."""
import pytest

from shared.phoneme_utils import load_phoneme_dict, build_trie


def _write_cmudict(tmp_path, lines):
    d = tmp_path / "cmudict-0.7b"
    d.write_text('\n'.join(lines) + '\n', encoding='latin-1')
    return tmp_path


# --- load_phoneme_dict ---

def test_load_phoneme_dict_parses_basic_entry(tmp_path):
    _write_cmudict(tmp_path, ["HELLO  HH AH0 L OW1"])
    result = load_phoneme_dict(tmp_path)
    assert result['HELLO'] == ['HH', 'AH', 'L', 'OW']


def test_load_phoneme_dict_strips_stress_numerals(tmp_path):
    _write_cmudict(tmp_path, ["TEST  T EH1 S T"])
    result = load_phoneme_dict(tmp_path)
    assert result['TEST'] == ['T', 'EH', 'S', 'T']


def test_load_phoneme_dict_skips_comment_lines(tmp_path):
    _write_cmudict(tmp_path, [
        ";;; This is a comment",
        "BACK  B AE1 K",
    ])
    result = load_phoneme_dict(tmp_path)
    assert ';;;' not in result
    assert result['BACK'] == ['B', 'AE', 'K']


def test_load_phoneme_dict_first_entry_wins_on_duplicate(tmp_path):
    _write_cmudict(tmp_path, [
        "READ  R IY1 D",
        "READ  R EH1 D",
    ])
    result = load_phoneme_dict(tmp_path)
    assert result['READ'] == ['R', 'IY', 'D']


def test_load_phoneme_dict_netflix_always_present(tmp_path):
    _write_cmudict(tmp_path, ["HELLO  HH AH0 L OW1"])
    result = load_phoneme_dict(tmp_path)
    assert 'NETFLIX' in result
    assert result['NETFLIX'] == ['N', 'EH', 'T', 'F', 'L', 'IH', 'K', 'S']


# --- build_trie ---

def test_build_trie_single_word():
    trie = build_trie({'BACK': ['B', 'AE', 'K']})
    assert trie['B']['AE']['K']['$'] == ['BACK']


def test_build_trie_shared_prefix():
    trie = build_trie({
        'BACK': ['B', 'AE', 'K'],
        'BAD':  ['B', 'AE', 'D'],
    })
    # Both share B -> AE prefix
    assert 'K' in trie['B']['AE']
    assert 'D' in trie['B']['AE']


def test_build_trie_empty_input():
    trie = build_trie({})
    assert trie == {}
