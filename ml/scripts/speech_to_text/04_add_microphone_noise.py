import argparse
import os
from pathlib import Path
import random
import numpy as np
import soundfile as sf
from tqdm import tqdm

# Settings
# Noise frequency
microphone_noise_frequency = 2  # Add noise every N samples

# Noise volume
microphone_noise_volume_min = 0.01  # Minimum noise volume
microphone_noise_volume_max = 0.05   # Maximum noise volume
microphone_noise_type = 'white'     # Type of noise (future extension)

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Add microphone noise to audio samples.")
parser.add_argument('--input-dir', type=Path, required=True, help='Directory containing input wav files')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output wav files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

# Process each audio file in the input directory
input_files = list(paths.input_dir.glob('*.wav'))
for file_path in tqdm(input_files, desc="Processing audio files", unit="file", total=len(input_files)):
    stem = file_path.stem
    # Decide randomly whether to add noise
    add_noise = random.randint(1, microphone_noise_frequency) == 1
    data, samplerate = sf.read(file_path)
    noise_volume = 0.0
    if add_noise:
        # Random noise volume
        noise_volume = random.uniform(microphone_noise_volume_min, microphone_noise_volume_max)
        # Generate white noise
        noise = np.random.normal(0, 1, data.shape) * noise_volume
        data_noisy = data + noise
        # Clip to valid range
        data_noisy = np.clip(data_noisy, -1.0, 1.0)
        # Modify filename to indicate noise
        noise_str = f"_mic{int(noise_volume * 1000):03d}"
        out_name = file_path.stem + noise_str + file_path.suffix
        out_path = paths.output_dir / out_name
        sf.write(out_path, data_noisy, samplerate)
    else:
        # Save original file without noise
        out_path = paths.output_dir / file_path.name
        sf.write(out_path, data, samplerate)

