"""Round-trip tests for shared.io_utils using real file I/O via tmp_path."""
import numpy as np
import pandas as pd
import pytest

from shared.io_utils import (
    write_token_list,
    read_token_list,
    write_spectrogram,
    read_spectrogram,
    read_phoneme_list,
    read_manifest,
)


def test_write_read_token_list_round_trip(tmp_path):
    phonemes = ['AH', 'B', 'K']
    tokens = [0, 1, 2, 3, 3, 3]
    write_token_list(tmp_path, "test_wav", phonemes, tokens)
    result = read_token_list(tmp_path, "test_wav")
    np.testing.assert_array_equal(result, tokens)


def test_read_token_list_returns_int32(tmp_path):
    write_token_list(tmp_path, "stem", ['AH'], [0, 1])
    result = read_token_list(tmp_path, "stem")
    assert result.dtype == np.int32


def test_write_read_spectrogram_round_trip(tmp_path):
    data = np.random.rand(80, 360).astype(np.float32)
    write_spectrogram(tmp_path, "spec_wav", data)
    result = read_spectrogram(tmp_path, "spec_wav")
    assert result.shape == (80, 360)
    np.testing.assert_array_almost_equal(result, data)


def test_read_phoneme_list_returns_list_and_blank_idx(tmp_path):
    pfile = tmp_path / "phoneme_list.txt"
    pfile.write_text("AH\nB\nK\n", encoding='utf-8')
    vocab, blank_idx = read_phoneme_list(pfile)
    assert vocab == ['AH', 'B', 'K']
    assert blank_idx == 3


def test_read_phoneme_list_strips_whitespace(tmp_path):
    pfile = tmp_path / "phoneme_list.txt"
    pfile.write_text("AH\n\nB\n   \nK\n", encoding='utf-8')
    vocab, _ = read_phoneme_list(pfile)
    assert '' not in vocab
    assert vocab == ['AH', 'B', 'K']


def test_read_manifest_returns_dataframe_with_correct_columns(tmp_path):
    csv_file = tmp_path / "manifest.csv"
    csv_file.write_text("filepath,speech_to_detect\nfoo.wav,hello\nbar.wav,world\n", encoding='utf-8')
    df = read_manifest(csv_file)
    assert isinstance(df, pd.DataFrame)
    assert list(df.columns) == ['filepath', 'speech_to_detect']
    assert len(df) == 2
