import argparse
import os
from pathlib import Path
import random
import pandas as pd
import soundfile as sf
import numpy as np
from tqdm import tqdm

# Settings
# Parse command-line arguments
parser = argparse.ArgumentParser(description="Add random delays to audio samples.")
parser.add_argument('--input-file', type=Path, required=True, help='Input wav file list (CSV) with delay variations')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output wav files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

# Read the CSV file with delay variations
df = pd.read_csv(paths.input_file)

for _, row in tqdm(df.iterrows(), total=len(df), desc="Processing audio files", unit="file"):
    file_path = Path(row['input_file_path'])

    data, samplerate = sf.read(file_path)
    new_data = data
    prefix_delay = 0.0
    suffix_delay = 0.0

    # Add prefix silence
    if row['prefix_delay_seconds'] > 0.0:
        prefix_delay = row['prefix_delay_seconds']
        num_prefix_samples = int(prefix_delay * samplerate)
        silence_prefix = np.zeros((num_prefix_samples, data.shape[1]) if data.ndim > 1 else num_prefix_samples, dtype=data.dtype)
        new_data = np.concatenate([silence_prefix, new_data], axis=0)

    # Add suffix silence
    if row['suffix_delay_seconds'] > 0.0:
        suffix_delay = row['suffix_delay_seconds']
        num_suffix_samples = int(suffix_delay * samplerate)
        silence_suffix = np.zeros((num_suffix_samples, data.shape[1]) if data.ndim > 1 else num_suffix_samples, dtype=data.dtype)
        new_data = np.concatenate([new_data, silence_suffix], axis=0)

    out_path = paths.output_dir / row['new_file_name']

    sf.write(out_path, new_data, samplerate)

