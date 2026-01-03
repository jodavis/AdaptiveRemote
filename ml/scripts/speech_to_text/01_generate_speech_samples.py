import argparse
from pathlib import Path
import pandas as pd
import os
import asyncio
import edge_tts
from tqdm import tqdm

# Settings
# Set subsampling rate (e.g., 1 = all, 2 = every other, 3 = every third)
subsample_rate = 1  # Change this value as needed

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Generate speech samples from variations CSV.")
parser.add_argument('--input-file', type=Path, required=True, help='Path to the input CSV file (variations)')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output speech samples')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

# Load the phrases from CSV
phrases_df = pd.read_csv(paths.input_file, encoding='utf-8')

subsampled_indices = list(range(0, len(phrases_df), subsample_rate))

# Functions
async def generate_samples():
    for count, idx in enumerate(tqdm(subsampled_indices, desc="Generating speech samples")):
        output_path = paths.output_dir / phrases_df.iloc[idx]['sample_file_name']
        if output_path.exists():
            continue  # Skip if samples already exist for this phrase index
        phrase = phrases_df.iloc[idx]['phrase_to_speak']
        voice = phrases_df.iloc[idx]['voice']
        speech_rate_str = phrases_df.iloc[idx]['speech_rate']
        try:
            communicate = edge_tts.Communicate(
                text=phrase,
                voice=voice,
                rate=speech_rate_str,
            )
            await communicate.save(str(output_path))
        except Exception as e:
            print(f"Error generating sample for index {idx} with voice {voice}: {e}")
            continue
    return count

async def main():
    print("Starting sample generation...")
    total_generated = await generate_samples()
    print(f"Sample generation completed. Total samples generated: {total_generated + 1}")

if __name__ == "__main__":
    asyncio.run(main())