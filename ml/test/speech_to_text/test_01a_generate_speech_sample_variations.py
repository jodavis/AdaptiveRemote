"""
Tests for the deduplication logic in 01a_generate_speech_sample_variations.py.

The module is loaded via importlib because its filename starts with a digit.
All tests exercise generate_variation_records() directly — no async TTS calls.
"""
import asyncio
import importlib.util
import sys
import types
from pathlib import Path

import pandas as pd
import pytest

# ---------------------------------------------------------------------------
# Module loading helpers
# ---------------------------------------------------------------------------

SCRIPT_PATH = (
    Path(__file__).parent.parent.parent
    / "scripts/speech_to_text/01a_generate_speech_sample_variations.py"
)


def _load_module():
    """Import the script as a module without executing its __main__ block."""
    spec = importlib.util.spec_from_file_location("script_01a", SCRIPT_PATH)
    mod = importlib.util.module_from_spec(spec)
    # Stub out edge_tts so the import doesn't require network access
    edge_tts_stub = types.ModuleType("edge_tts")
    edge_tts_stub.list_voices = lambda: []
    edge_tts_stub.Communicate = object
    sys.modules.setdefault("edge_tts", edge_tts_stub)
    spec.loader.exec_module(mod)
    return mod


_mod = _load_module()
generate_variation_records = _mod.generate_variation_records


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_paths(tmp_path):
    """Return a minimal namespace that looks like the parsed argparse object."""
    class _Paths:
        output_file = tmp_path / "out.csv"
        samples_dir = tmp_path / "samples"
    return _Paths()


def _run(coro):
    return asyncio.run(coro)


VOICES = [{'ShortName': 'en-US-TestVoice', 'Gender': 'Female', 'Locale': 'en-US'}]

_EXISTING_RECORD = {
    'phrase_to_speak': 'go back',
    'phrase_to_detect': 'go back',
    'voice': 'en-US-ExistingVoice',
    'speech_rate': '+10%',
    'sample_file_name': 'Back_0_en-US-ExistingVoice_r110.wav',
}


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

def test_existing_variation_is_preserved(tmp_path):
    paths = _make_paths(tmp_path)
    phrases = ['go back']
    labels = ['Back']
    speech = ['go back']
    existing = [_EXISTING_RECORD.copy()]

    records = _run(generate_variation_records(paths, phrases, labels, speech, existing, VOICES))

    assert len(records) == 1
    assert records[0]['voice'] == 'en-US-ExistingVoice'
    assert records[0]['speech_rate'] == '+10%'


def test_new_phrase_not_in_existing_gets_new_record(tmp_path):
    paths = _make_paths(tmp_path)
    phrases = ['volume up']
    labels = ['VolumeUp']
    speech = ['volume up']

    records = _run(generate_variation_records(paths, phrases, labels, speech, [], VOICES))

    assert len(records) == 1
    assert records[0]['phrase_to_speak'] == 'volume up'
    assert records[0]['voice'] == 'en-US-TestVoice'


def test_removed_phrase_not_in_output(tmp_path):
    """A phrase that was in the existing CSV but is no longer in the input should be absent."""
    paths = _make_paths(tmp_path)
    existing = [_EXISTING_RECORD.copy()]  # 'go back' existed before
    phrases = ['volume up']              # new input has different phrase
    labels = ['VolumeUp']
    speech = ['volume up']

    records = _run(generate_variation_records(paths, phrases, labels, speech, existing, VOICES))

    assert all(r['phrase_to_speak'] != 'go back' for r in records)


def test_subsample_rate_one_includes_all_phrases(tmp_path, monkeypatch):
    """SUBSAMPLE_RATE=1 includes every phrase."""
    monkeypatch.setattr(_mod, 'SUBSAMPLE_RATE', 1)
    paths = _make_paths(tmp_path)
    phrases = ['go back', 'volume up', 'mute']
    labels = ['Back', 'VolumeUp', 'Mute']
    speech = phrases[:]

    records = _run(generate_variation_records(paths, phrases, labels, speech, [], VOICES))

    assert len(records) == 3
    assert [r['phrase_to_speak'] for r in records] == phrases


def test_subsample_rate_skips_non_multiples(tmp_path, monkeypatch):
    """Only phrases at indices divisible by SUBSAMPLE_RATE are included."""
    monkeypatch.setattr(_mod, 'SUBSAMPLE_RATE', 2)
    paths = _make_paths(tmp_path)
    phrases = ['go back', 'volume up', 'mute']   # idx 0, 1, 2
    labels = ['Back', 'VolumeUp', 'Mute']
    speech = phrases[:]

    records = _run(generate_variation_records(paths, phrases, labels, speech, [], VOICES))

    # idx 0 ('go back') and idx 2 ('mute') are multiples of 2; idx 1 ('volume up') is not
    assert len(records) == 2
    assert records[0]['phrase_to_speak'] == 'go back'
    assert records[1]['phrase_to_speak'] == 'mute'


def test_subsample_rate_idx_in_filename_is_phrases_list_index(tmp_path, monkeypatch):
    """sample_file_name uses the phrase's index in the full phrases list, not the subsampled position."""
    monkeypatch.setattr(_mod, 'SUBSAMPLE_RATE', 2)
    paths = _make_paths(tmp_path)
    phrases = ['go back', 'volume up', 'mute']
    labels = ['Back', 'VolumeUp', 'Mute']
    speech = phrases[:]

    records = _run(generate_variation_records(paths, phrases, labels, speech, [], VOICES))

    # 'mute' is at idx 2 in the full list — its filename should contain '_2_', not '_1_'
    mute_record = next(r for r in records if r['phrase_to_speak'] == 'mute')
    assert '_2_' in mute_record['sample_file_name']


def test_subsample_rate_existing_preserved_at_included_index(tmp_path, monkeypatch):
    """An existing record at an included index is preserved; one at a skipped index is dropped."""
    monkeypatch.setattr(_mod, 'SUBSAMPLE_RATE', 2)
    paths = _make_paths(tmp_path)
    existing_go_back = _EXISTING_RECORD.copy()   # phrase 'go back' — will be at idx 0 (included)
    existing_volume_up = {
        **_EXISTING_RECORD,
        'phrase_to_speak': 'volume up',
        'phrase_to_detect': 'volume up',
        'sample_file_name': 'VolumeUp_1_en-US-ExistingVoice_r110.wav',
    }                                            # phrase 'volume up' — will be at idx 1 (skipped)
    phrases = ['go back', 'volume up']
    labels = ['Back', 'VolumeUp']
    speech = phrases[:]

    records = _run(generate_variation_records(paths, phrases, labels, speech, [existing_go_back, existing_volume_up], VOICES))

    assert len(records) == 1
    assert records[0]['phrase_to_speak'] == 'go back'
    assert records[0]['voice'] == 'en-US-ExistingVoice'   # preserved from previous run


def test_sample_file_not_deleted_when_record_preserved(tmp_path):
    """Preserved record's sample_file_name must appear in output → not a deletion candidate."""
    paths = _make_paths(tmp_path)
    existing = [_EXISTING_RECORD.copy()]
    phrases = ['go back']
    labels = ['Back']
    speech = ['go back']

    records = _run(generate_variation_records(paths, phrases, labels, speech, existing, VOICES))

    output_filenames = {r['sample_file_name'] for r in records}
    assert _EXISTING_RECORD['sample_file_name'] in output_filenames
