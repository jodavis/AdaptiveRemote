import argparse
from pathlib import Path
import pandas as pd
import random
import os
import asyncio
import edge_tts
from tqdm import tqdm

# Settings
# Set the speech rate range (in percentage)
min_speech_rate = -50  # Minimum speech rate
max_speech_rate = 80   # Maximum speech rate

# Read file and directory paths from command line arguments
parser = argparse.ArgumentParser(description="Generate speech sample variations.")
parser.add_argument('--input-file', type=Path, required=True, help='Path to the input CSV file')
parser.add_argument('--output-file', type=Path, required=True, help='Path to the output CSV file')
parser.add_argument('--samples-dir', type=Path, required=True, help='Directory for speech samples')
paths = parser.parse_args()

os.makedirs(paths.output_file.parent, exist_ok=True)

phrases_df = pd.read_csv(paths.input_file, encoding='utf-8')
phrases = phrases_df['surface_form'].tolist()
labels = phrases_df['canonical_label'].tolist()
speech_to_detect = phrases_df['speech_to_detect'].tolist()

# Load the existing records if the file exists
try:
    existing_df = pd.read_csv(paths.output_file, encoding='utf-8')
    existing_records = existing_df.to_dict(orient='records')
    existing_phrases = existing_df['phrase_to_speak'].tolist()
    print(f"Loaded {len(existing_records)} existing variation records from {paths.output_file}...")
except FileNotFoundError:
    print(f"Did not find existing {paths.output_file}.")
    existing_records = []
    existing_phrases = []

# Functions
async def get_voices():
    voices = await edge_tts.list_voices()
    # Filter for female voices and exclude problematic ones
    female_voices = [
        v for v in voices
        if v['Gender'] == 'Female'
        and v['Locale'] == 'en-US'
        and ':' not in v['ShortName']
        and 'DragonHD' not in v['ShortName']
        and 'Turbo' not in v['ShortName']
    ]
    print(f"Sample female voices: {[v['ShortName'] for v in female_voices[:5]]}")
    print(f"Total female voices found: {len(female_voices)}")
    return female_voices

async def generate_variation_records(voices: list):
    records = []
    if not voices:
        print("No voices available.")
        return records
    for idx, (phrase, label, speech) in enumerate(tqdm(zip(phrases, labels, speech_to_detect), desc = "Generating variations", total=len(phrases))):
        try:
            existing_idx = existing_phrases.index(phrase)
            records.append(existing_records[existing_idx])
            existing_phrases.remove(existing_idx)
            continue
        except ValueError:
            pass  # Phrase not found in existing records, proceed to create new variations
        voice = random.choice(voices)['ShortName']
        speech_rate = random.randint(min_speech_rate, max_speech_rate)
        speech_rate_str = f"+{speech_rate}%" if speech_rate >= 0 else f"{speech_rate}%"
        records.append({
            'phrase_to_speak': phrase,
            'phrase_to_detect': speech,
            'voice': voice,
            'speech_rate': speech_rate_str,
            'sample_file_name': f"{label}_{idx}_{voice}_r{speech_rate + 100}.wav",
        })
    return records

async def main():
    print("Fetching available voices...")
    voices = await get_voices()
    print("Generating variation records...")
    variation_records = await generate_variation_records(voices)
    print(f"Saving variation records to {paths.output_file}...")
    variations_df = pd.DataFrame(variation_records)
    variations_df.to_csv(paths.output_file, index=False, encoding='utf-8')
    print("Deleting existing speech samples.")
    # Remove files from SAMPLE_OUTPUT_DIR that are not in the new variations_df
    if paths.samples_dir.exists():
        for file in paths.samples_dir.iterdir():
            if not variations_df["sample_file_name"].str.contains(file.name).any():
                print(f"Deleting obsolete sample file: {file.name}")
                file.unlink()

if __name__ == "__main__":
    asyncio.run(main())