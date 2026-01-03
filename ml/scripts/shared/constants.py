"""
Centralised pipeline dimension constants.  All scripts import from here
instead of hardcoding values independently.

Values are read from ml/params.yaml so they can be overridden via
``dvc exp run --set-param pipeline.n_mels=40`` without touching source code.
"""
from pathlib import Path

import yaml

_params_path = Path(__file__).parent.parent.parent / "params.yaml"
with open(_params_path, encoding='utf-8') as _f:
    _y = yaml.safe_load(_f)
    _p = _y["pipeline"]
    _d = _y["add_delays"]
    _gssv = _y["generate_speech_sample_variations"]
    _gs = _y["generate_speech_samples"]
    _abn = _y["add_background_noise"]
    _amn = _y["add_microphone_noise"]
    _csm = _y["create_set_manifests"]

INPUT_TOKEN_LENGTH: int = _p["input_token_length"]
N_MELS: int = _p["n_mels"]
TIME_STEPS: int = _p["time_steps"]
BATCH_SIZE: int = _p["batch_size"]
EPOCHS: int = _p["epochs"]
SUBSAMPLE_RATE: int = _p["subsample_rate"]

PREFIX_DELAY_FREQUENCY: int = _d["prefix_frequency"]
SUFFIX_DELAY_FREQUENCY: int = _d["suffix_frequency"]
MAX_DELAY_DURATION: float = _d["max_duration"]
MIN_DELAY_DURATION: float = _d["min_duration"]

MIN_SPEECH_RATE: int = _gssv["min_speech_rate"]
MAX_SPEECH_RATE: int = _gssv["max_speech_rate"]

MAX_RETRIES: int = _gs["max_retries"]
RETRY_DELAY_SEC: int = _gs["retry_delay_sec"]

BACKGROUND_NOISE_FREQUENCY: int = _abn["noise_frequency"]
BACKGROUND_NOISE_VOLUME_MIN: float = _abn["volume_min"]
BACKGROUND_NOISE_VOLUME_MAX: float = _abn["volume_max"]

MICROPHONE_NOISE_FREQUENCY: int = _amn["noise_frequency"]
MICROPHONE_NOISE_VOLUME_MIN: float = _amn["volume_min"]
MICROPHONE_NOISE_VOLUME_MAX: float = _amn["volume_max"]
MICROPHONE_NOISE_TYPE: str = _amn["noise_type"]

TRAINING_SET_PERCENTAGE: int = _csm["training_set_percentage"]
VALIDATION_SET_PERCENTAGE: int = _csm["validation_set_percentage"]
TEST_SET_PERCENTAGE: int = _csm["test_set_percentage"]