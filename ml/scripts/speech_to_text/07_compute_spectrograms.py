import argparse
from pathlib import Path
import os
import numpy as np
import librosa
import pandas as pd
import soundfile as sf
from tqdm import tqdm
import re

# Settings
time_steps = 360  # number of time steps in spectrogram output
input_token_length = 20 # max length of input token sequences

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Compute log-mel spectrograms for audio files.")
parser.add_argument('--train-manifest', type=Path, required=True, help='Path to train_manifest.csv')
parser.add_argument('--eval-manifest', type=Path, required=True, help='Path to eval_manifest.csv')
parser.add_argument('--test-manifest', type=Path, required=True, help='Path to test_manifest.csv')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output spectrogram npy files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

def compute_melspectrogram(time_steps, wav_path):
    y, sr = sf.read(str(wav_path))
    # If stereo, convert to mono (average channels)
    if y.ndim > 1:
        y = np.mean(y, axis=1)
    S = librosa.feature.melspectrogram(y=y, sr=sr, n_mels=80)
    log_S = librosa.power_to_db(S, ref=np.max)
    if log_S.shape[1] < time_steps:
        pad_width = time_steps - log_S.shape[1]
        log_S = np.pad(log_S, ((0,0),(0,pad_width)), mode='constant')
    else:
        print(f'Warning: Truncating spectrogram for {wav_path}, has {log_S.shape[1]}>{time_steps} time steps.')
        log_S = log_S[:, :time_steps]
    return log_S

def load_rows_from_manifest(manifest_path):
    loaded_rows = pd.read_csv(manifest_path, encoding='utf-8')
    print(f"Loaded {len(loaded_rows)} samples from {manifest_path}.")
    return [(row['filepath'], row['speech_to_detect']) for _, row in loaded_rows.iterrows()]

manifest_rows = load_rows_from_manifest(paths.train_manifest) + \
                load_rows_from_manifest(paths.eval_manifest) + \
                load_rows_from_manifest(paths.test_manifest)

for wav_path, transcription in tqdm(manifest_rows, desc="Computing spectrograms from manifests", total=len(manifest_rows)):
    wav_path = Path(wav_path)
    try:
        log_S = compute_melspectrogram(time_steps, wav_path)

        out_path = paths.output_dir / (wav_path.stem + '.npy')
        np.save(out_path, log_S)
    except Exception as e:
        print(f'Error processing {wav_path}: {e}')





