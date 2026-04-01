"""Tests for is_valid_audio_file() in 01_generate_speech_samples.py."""
import importlib.util
import sys
import types
from pathlib import Path

import pytest

# ---------------------------------------------------------------------------
# Module loading
# ---------------------------------------------------------------------------

SCRIPT_PATH = (
    Path(__file__).parent.parent.parent
    / "scripts/speech_to_text/01_generate_speech_samples.py"
)


def _load_module():
    # Stub edge_tts so no network import is needed
    import types as _types
    stub = _types.ModuleType("edge_tts")
    stub.Communicate = object
    sys.modules.setdefault("edge_tts", stub)
    spec = importlib.util.spec_from_file_location("script_01", SCRIPT_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_mod = _load_module()
is_valid_audio_file = _mod.is_valid_audio_file


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

def test_is_valid_audio_file_rejects_missing_file(tmp_path):
    assert not is_valid_audio_file(tmp_path / "nonexistent.wav")


def test_is_valid_audio_file_rejects_zero_byte_file(tmp_path):
    f = tmp_path / "empty.wav"
    f.write_bytes(b"")
    assert not is_valid_audio_file(f)


def test_is_valid_audio_file_accepts_nonzero_file(tmp_path):
    f = tmp_path / "valid.wav"
    f.write_bytes(b"\x00\x01\x02")
    assert is_valid_audio_file(f)
