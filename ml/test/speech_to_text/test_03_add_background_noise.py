"""Tests for add_noise_to_audio() and get_random_noise() in 03_add_background_noise.py.

add_noise_to_audio is a pure function (no mocking needed).
get_random_noise mocks soundfile.read and random.choice.
"""
import importlib.util
import sys
import types
from pathlib import Path
from unittest.mock import patch, MagicMock

import numpy as np
import pytest

# ---------------------------------------------------------------------------
# Module loading
# ---------------------------------------------------------------------------

SCRIPT_PATH = (
    Path(__file__).parent.parent.parent
    / "scripts/speech_to_text/03_add_background_noise.py"
)


def _load_module():
    spec = importlib.util.spec_from_file_location("script_03", SCRIPT_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_mod = _load_module()
add_noise_to_audio = _mod.add_noise_to_audio
get_random_noise = _mod.get_random_noise


# ---------------------------------------------------------------------------
# add_noise_to_audio — pure function tests
# ---------------------------------------------------------------------------

def test_linear_mix():
    audio = np.array([0.5], dtype=np.float64)
    noise = np.array([0.1], dtype=np.float64)
    result = add_noise_to_audio(audio, noise, volume=0.5)
    np.testing.assert_allclose(result, [0.55])


def test_zero_volume_returns_original():
    audio = np.array([0.3, 0.7], dtype=np.float64)
    noise = np.array([1.0, 1.0], dtype=np.float64)
    result = add_noise_to_audio(audio, noise, volume=0.0)
    np.testing.assert_array_equal(result, audio)


# ---------------------------------------------------------------------------
# get_random_noise — mocked I/O tests
# ---------------------------------------------------------------------------

def _patch_sf_read(noise_array, noise_sr):
    return patch("soundfile.read", return_value=(noise_array, noise_sr))


def test_short_noise_tiled_to_target_length():
    target_length = 100
    short_noise = np.ones(30, dtype=np.float32)
    target_sr = 16000

    with _patch_sf_read(short_noise, target_sr), \
         patch("random.choice", return_value=Path("noise.wav")), \
         patch("random.randint", return_value=0):
        result = get_random_noise([Path("noise.wav")], target_length, target_sr)

    assert len(result) == target_length


def test_long_noise_trimmed_to_target_length():
    target_length = 50
    long_noise = np.ones(200, dtype=np.float32)
    target_sr = 16000

    with _patch_sf_read(long_noise, target_sr), \
         patch("random.choice", return_value=Path("noise.wav")), \
         patch("random.randint", return_value=0):
        result = get_random_noise([Path("noise.wav")], target_length, target_sr)

    assert len(result) == target_length


def test_mismatched_sample_rate_resampled_to_target_length():
    target_length = 160
    # Noise at 8000 Hz; target 16000 Hz → should be upsampled to 2x length
    noise_sr = 8000
    target_sr = 16000
    # 80 samples at 8000 Hz == 160 samples at 16000 Hz
    noise_data = np.ones(80, dtype=np.float32)

    with _patch_sf_read(noise_data, noise_sr), \
         patch("random.choice", return_value=Path("noise.wav")), \
         patch("random.randint", return_value=0):
        result = get_random_noise([Path("noise.wav")], target_length, target_sr)

    assert len(result) == target_length


def test_stereo_noise_uses_first_channel():
    target_length = 50
    # Stereo noise: shape (100, 2); channel 0 = 1.0, channel 1 = 2.0
    stereo_noise = np.stack([np.ones(100), np.full(100, 2.0)], axis=1).astype(np.float32)
    target_sr = 16000

    with _patch_sf_read(stereo_noise, target_sr), \
         patch("random.choice", return_value=Path("noise.wav")), \
         patch("random.randint", return_value=0):
        result = get_random_noise([Path("noise.wav")], target_length, target_sr)

    # Should have used channel 0 (all 1.0)
    assert len(result) == target_length
    np.testing.assert_array_equal(result, np.ones(target_length))
