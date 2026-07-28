"""
Shared I/O helpers used by scripts 06, 07, 08, 09, 10.

Centralising these patterns means a format change (e.g. npy → JSON for
spectrograms) only requires one code change instead of four.
"""
import json
from pathlib import Path

import numpy as np
import pandas as pd


def write_token_list(output_dir: Path, wav_stem: str, phonemes: list, tokens: list) -> None:
    out_path = output_dir / f"{wav_stem}_tokens.json"
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump({'phonemes': phonemes, 'tokens': tokens}, f)


def read_token_list(token_list_dir: Path, wav_stem: str) -> np.ndarray:
    tokens_file = token_list_dir / f"{wav_stem}_tokens.json"
    with open(tokens_file, 'r', encoding='utf-8') as f:
        return np.array(json.load(f)['tokens'], dtype=np.int32)


def write_spectrogram(output_dir: Path, wav_stem: str, data: np.ndarray) -> None:
    np.save(output_dir / f"{wav_stem}.npy", data)


def read_spectrogram(spectrogram_dir: Path, wav_stem: str) -> np.ndarray:
    return np.load(spectrogram_dir / f"{wav_stem}.npy")


def read_phoneme_list(phoneme_list_path: Path) -> tuple[list[str], int]:
    """Return (vocab_list, ctc_blank_idx). ctc_blank_idx = len(vocab_list)."""
    with open(phoneme_list_path, 'r', encoding='utf-8') as f:
        vocab_list = [line.strip() for line in f if line.strip()]
    return vocab_list, len(vocab_list)


def read_manifest(manifest_path: Path) -> pd.DataFrame:
    return pd.read_csv(manifest_path, encoding='utf-8')
