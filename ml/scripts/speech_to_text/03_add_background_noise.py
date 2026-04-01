import argparse
import os
from pathlib import Path
import random
import numpy as np
import soundfile as sf
from tqdm import tqdm
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import BACKGROUND_NOISE_FREQUENCY, BACKGROUND_NOISE_VOLUME_MIN, BACKGROUND_NOISE_VOLUME_MAX

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Add background noise to audio samples.")
parser.add_argument('--input-dir', type=Path, required=True, help='Directory containing input wav files')
parser.add_argument('--noise-dir', type=Path, required=True, help='Directory containing noise wav files')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output wav files')


def get_random_noise(noise_files, length, sr):
    noise_file = random.choice(noise_files)
    noise, noise_sr = sf.read(noise_file)
    if len(noise.shape) > 1:
        noise = noise[:,0]  # Use first channel if stereo
    if noise_sr != sr:
        # Resample noise to match target sample rate
        num_samples = int(len(noise) * sr / noise_sr)
        indices = np.linspace(0, len(noise) - 1, num_samples).astype(int)
        noise = noise[indices]
    if len(noise) < length:
        # Loop noise if too short
        repeats = int(np.ceil(length / len(noise)))
        noise = np.tile(noise, repeats)
    max_start = max(0, len(noise) - length)
    start = random.randint(0, max_start)
    return noise[start:start+length]


def add_noise_to_audio(audio, noise, volume):
    return audio + noise * volume


def main(paths):
    os.makedirs(paths.output_dir, exist_ok=True)
    noise_files = list(paths.noise_dir.glob("*.wav"))
    if not noise_files:
        print(f"No noise samples found in {paths.noise_dir}")
        return
    input_files = list(paths.input_dir.glob("*.wav"))
    for input_file in tqdm(input_files, desc="Processing audio files", unit="file", total=len(input_files)):
        stem = input_file.stem
        audio, sr = sf.read(input_file)
        if len(audio.shape) > 1:
            audio = audio[:,0]  # Use first channel if stereo
        add_noise = (random.randint(1, BACKGROUND_NOISE_FREQUENCY) == 1)
        if add_noise:
            noise = get_random_noise(noise_files, len(audio), sr)
            volume = random.uniform(BACKGROUND_NOISE_VOLUME_MIN, BACKGROUND_NOISE_VOLUME_MAX)
            audio_noisy = add_noise_to_audio(audio, noise, volume)
            # Clip to [-1,1] to avoid overflow
            audio_noisy = np.clip(audio_noisy, -1.0, 1.0)
            out_audio = audio_noisy
            # Modify filename to include _bg{volume} without leading '0.'
            volume_str = f"{int(volume * 1000):03d}"
            out_filename = f"{stem}_bg{volume_str}.wav"
        else:
            out_audio = audio
            out_filename = input_file.name
        out_path = paths.output_dir / out_filename
        sf.write(out_path, out_audio, sr)


if __name__ == "__main__":
    main(parser.parse_args())
