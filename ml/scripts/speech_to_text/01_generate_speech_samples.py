import argparse
from pathlib import Path
import pandas as pd
import os
import asyncio
import edge_tts
from tqdm import tqdm

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import MAX_RETRIES, RETRY_DELAY_SEC

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Generate speech samples from variations CSV.")
parser.add_argument('--input-file', type=Path, required=True, help='Path to the input CSV file (variations)')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output speech samples')


def is_valid_audio_file(path: Path) -> bool:
    """Return True if the file exists and has non-zero size."""
    return path.exists() and path.stat().st_size > 0


async def generate_sample(idx, phrase, voice, speech_rate_str, output_path):
    for attempt in range(MAX_RETRIES):
        try:
            communicate = edge_tts.Communicate(
                text=phrase,
                voice=voice,
                rate=speech_rate_str,
            )
            await communicate.save(str(output_path))
            if is_valid_audio_file(output_path):
                return True
            # File missing or empty — delete corrupt file before retry
            if output_path.exists():
                output_path.unlink()
        except Exception as e:
            print(f"Attempt {attempt + 1}/{MAX_RETRIES} failed for index {idx}: {e}")
        if attempt < MAX_RETRIES - 1:
            await asyncio.sleep(RETRY_DELAY_SEC * (2 ** attempt))
    print(f"Failed after {MAX_RETRIES} attempts for index {idx}, skipping.")
    return False


async def generate_samples(paths, phrases_df):
    count = 0
    for idx in tqdm(range(0, len(phrases_df)), desc="Generating speech samples"):
        output_path = paths.output_dir / phrases_df.iloc[idx]['sample_file_name']
        if is_valid_audio_file(output_path):
            continue  # Skip if a valid file already exists
        phrase = phrases_df.iloc[idx]['phrase_to_speak']
        voice = phrases_df.iloc[idx]['voice']
        speech_rate_str = phrases_df.iloc[idx]['speech_rate']
        if await generate_sample(idx, phrase, voice, speech_rate_str, output_path):
            count += 1
    return count


async def main():
    paths = parser.parse_args()
    os.makedirs(paths.output_dir, exist_ok=True)

    # Load the phrases from CSV
    phrases_df = pd.read_csv(paths.input_file, encoding='utf-8')

    print("Starting sample generation...")
    total_generated = await generate_samples(paths, phrases_df)
    print(f"Sample generation completed. Total samples generated: {total_generated}")


if __name__ == "__main__":
    asyncio.run(main())
