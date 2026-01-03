"""
Phoneme dictionary helpers shared between 02_compute_vocab.py and tests.
"""
from pathlib import Path


def load_phoneme_dict(phoneme_dict_dir: Path) -> dict[str, list[str]]:
    """Load CMU phoneme dictionary from *phoneme_dict_dir*/cmudict-0.7b.

    Stress numerals are stripped from each phoneme (e.g. ``AH0`` → ``AH``).
    First entry wins on duplicate headwords.  A custom entry for ``NETFLIX``
    is always added.
    """
    phoneme_dict: dict[str, list[str]] = {}
    dict_path = phoneme_dict_dir / "cmudict-0.7b"
    with open(dict_path, encoding='latin-1') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or line.startswith(";;;"):
                continue
            parts = line.split()
            word = parts[0]
            phonemes = [''.join(c for c in p if c.isalpha()) for p in parts[1:]]
            if word not in phoneme_dict:
                phoneme_dict[word] = phonemes
    phoneme_dict['NETFLIX'] = ['N', 'EH', 'T', 'F', 'L', 'IH', 'K', 'S']
    return phoneme_dict


def build_trie(word_to_phonemes: dict[str, list[str]]) -> dict:
    """Build a trie mapping phoneme sequences to words."""
    root: dict = {}
    for word, phonemes in word_to_phonemes.items():
        node = root
        for phoneme in phonemes:
            node = node.setdefault(phoneme, {})
        node.setdefault("$", []).append(word)
    return root
