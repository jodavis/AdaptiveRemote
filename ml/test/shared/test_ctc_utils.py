"""Pure-numpy tests for shared.ctc_utils — no mocking needed."""
import numpy as np
import pytest

from shared.ctc_utils import ctc_greedy_decode, indices_to_words, trim_at_blank, tokens_to_text

VOCAB = ['AH', 'B', 'K', 'T']
BLANK = len(VOCAB)  # 4


# --- ctc_greedy_decode ---

def _make_pred(seq, num_classes=5):
    """Build a (1, len(seq), num_classes) one-hot pred array."""
    pred = np.zeros((1, len(seq), num_classes))
    for t, idx in enumerate(seq):
        pred[0, t, idx] = 1.0
    return pred


def test_ctc_greedy_decode_collapses_repeats():
    # AH AH B -> AH B
    pred = _make_pred([0, 0, 1])
    assert ctc_greedy_decode(pred, blank=BLANK) == [[0, 1]]


def test_ctc_greedy_decode_removes_blanks():
    # AH BLANK B -> AH B
    pred = _make_pred([0, BLANK, 1])
    assert ctc_greedy_decode(pred, blank=BLANK) == [[0, 1]]


def test_ctc_greedy_decode_all_blank():
    pred = _make_pred([BLANK, BLANK])
    assert ctc_greedy_decode(pred, blank=BLANK) == [[]]


def test_ctc_greedy_decode_batch_of_two():
    pred = np.zeros((2, 3, 5))
    pred[0, 0, 0] = 1.0  # AH
    pred[0, 1, BLANK] = 1.0
    pred[0, 2, 1] = 1.0  # B
    pred[1, 0, 2] = 1.0  # K
    pred[1, 1, 2] = 1.0  # K (repeat → collapsed)
    pred[1, 2, 3] = 1.0  # T
    result = ctc_greedy_decode(pred, blank=BLANK)
    assert result[0] == [0, 1]
    assert result[1] == [2, 3]


# --- indices_to_words ---

def test_indices_to_words_known_index():
    assert indices_to_words([0, 2], VOCAB) == ['AH', 'K']


def test_indices_to_words_out_of_range_returns_unk():
    assert indices_to_words([99], VOCAB) == ['[UNK]']


# --- trim_at_blank ---

def test_trim_at_blank_trims_at_first_oob():
    # VOCAB has 4 entries; index 4+ is out-of-vocab (pad/blank)
    seq = np.array([0, 1, 4, 2])  # AH B PAD K -> should trim to [AH, B]
    result = trim_at_blank(seq, VOCAB)
    np.testing.assert_array_equal(result, [0, 1])


def test_trim_at_blank_no_pad_returns_full():
    seq = np.array([0, 1, 2, 3])
    result = trim_at_blank(seq, VOCAB)
    np.testing.assert_array_equal(result, [0, 1, 2, 3])


# --- tokens_to_text ---

def test_tokens_to_text_joins_phonemes():
    assert tokens_to_text([0, 1, 2], VOCAB) == 'AHBK'


def test_tokens_to_text_skips_oob():
    # index 99 is out of vocab
    assert tokens_to_text([0, 99, 2], VOCAB) == 'AHK'
