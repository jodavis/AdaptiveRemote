"""Tests for compute_melspectrogram() in 07_compute_spectrograms.py.

soundfile.read and librosa.feature.melspectrogram are patched so no real
audio files are needed.
"""
import importlib.util
import sys
import types
from pathlib import Path
from unittest.mock import patch, MagicMock

import numpy as np
import pytest

from shared import constants

# ---------------------------------------------------------------------------
# Module loading
# ---------------------------------------------------------------------------

SCRIPT_PATH = (
    Path(__file__).parent.parent.parent
    / "scripts/speech_to_text/07_compute_spectrograms.py"
)


def _load_module():
    spec = importlib.util.spec_from_file_location("script_07", SCRIPT_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_mod = _load_module()
compute_melspectrogram = _mod.compute_melspectrogram

N_MELS = constants.N_MELS
TIME_STEPS = constants.TIME_STEPS


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _patch_audio(mono_audio, sr=16000):
    """Return a context manager that patches sf.read to return the given audio."""
    return patch("soundfile.read", return_value=(mono_audio, sr))


def _patch_mel(mel_output):
    """Return a context manager patching librosa.feature.melspectrogram."""
    return patch("librosa.feature.melspectrogram", return_value=mel_output)


def _patch_power_to_db(output):
    return patch("librosa.power_to_db", return_value=output)


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

def test_pads_short_spectrogram_to_time_steps():
    short_cols = TIME_STEPS - 50
    mel_out = np.ones((N_MELS, short_cols), dtype=np.float32)
    audio = np.zeros(1000, dtype=np.float32)
    with _patch_audio(audio), _patch_mel(mel_out), _patch_power_to_db(mel_out):
        result = compute_melspectrogram(Path("fake.wav"))
    assert result.shape == (N_MELS, TIME_STEPS)
    # Padded columns should be zero
    assert np.all(result[:, short_cols:] == 0.0)


def test_truncates_long_spectrogram_to_time_steps():
    long_cols = TIME_STEPS + 50
    mel_out = np.ones((N_MELS, long_cols), dtype=np.float32)
    audio = np.zeros(1000, dtype=np.float32)
    with _patch_audio(audio), _patch_mel(mel_out), _patch_power_to_db(mel_out):
        result = compute_melspectrogram(Path("fake.wav"))
    assert result.shape == (N_MELS, TIME_STEPS)


def test_stereo_converted_to_mono():
    # Stereo audio: shape (samples, 2); mean should produce mono
    stereo = np.ones((1000, 2), dtype=np.float32)
    stereo[:, 1] = 3.0  # channel 1 = 3.0, channel 0 = 1.0 → mean = 2.0
    mel_out = np.ones((N_MELS, TIME_STEPS), dtype=np.float32)
    captured = {}

    def fake_mel(y, sr, n_mels):
        captured['y'] = y
        return mel_out

    with _patch_audio(stereo), \
         patch("librosa.feature.melspectrogram", side_effect=fake_mel), \
         _patch_power_to_db(mel_out):
        compute_melspectrogram(Path("fake.wav"))

    # Mono conversion: average of channels
    np.testing.assert_array_almost_equal(captured['y'], np.full(1000, 2.0))


def test_exact_length_unchanged():
    mel_out = np.ones((N_MELS, TIME_STEPS), dtype=np.float32)
    audio = np.zeros(1000, dtype=np.float32)
    with _patch_audio(audio), _patch_mel(mel_out), _patch_power_to_db(mel_out):
        result = compute_melspectrogram(Path("fake.wav"))
    assert result.shape == (N_MELS, TIME_STEPS)
