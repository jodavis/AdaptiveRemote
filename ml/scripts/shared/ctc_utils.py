"""
CTC decoding utilities shared between 09_evaluate_model.py and
10_evaluate_test_samples.py.  Both scripts previously contained identical
copies of these functions.
"""
import numpy as np


def ctc_greedy_decode(pred, blank: int) -> list[list[int]]:
    """Greedy CTC decode.

    Args:
        pred: array of shape (batch, time, classes)
        blank: index of the CTC blank token

    Returns:
        List of decoded index lists (one per batch item).
    """
    pred_ids = np.argmax(pred, axis=-1)
    decoded = []
    for seq in pred_ids:
        prev = blank
        out: list[int] = []
        for idx in seq:
            if idx != prev and idx != blank:
                out.append(int(idx))
            prev = idx
        decoded.append(out)
    return decoded


def indices_to_words(indices: list[int], vocab_list: list[str]) -> list[str]:
    return [vocab_list[i] if i < len(vocab_list) else '[UNK]' for i in indices]


def trim_at_blank(seq: np.ndarray, vocab_list: list[str]) -> np.ndarray:
    """Trim sequence at first index that is out-of-vocab (>= len(vocab_list))."""
    idxs = np.where(seq >= len(vocab_list))[0]
    return seq[:idxs[0]] if len(idxs) > 0 else seq


def tokens_to_text(tokens: list[int], vocab_list: list[str]) -> str:
    return ''.join([vocab_list[idx] for idx in tokens if idx < len(vocab_list)])
