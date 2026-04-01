import argparse
from collections import defaultdict
from pathlib import Path
import pandas as pd
import random
import os
import asyncio
import edge_tts
from tqdm import tqdm
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import MIN_SPEECH_RATE, MAX_SPEECH_RATE, SUBSAMPLE_RATE

# Read file and directory paths from command line arguments
parser = argparse.ArgumentParser(description="Generate speech sample variations.")
parser.add_argument('--input-file', type=Path, required=True, help='Path to the input CSV file')
parser.add_argument('--output-file', type=Path, required=True, help='Path to the output CSV file')
parser.add_argument('--samples-dir', type=Path, required=True, help='Directory for speech samples')

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


async def generate_variation_records(paths, phrases, labels, speech_to_detect, existing_records, voices: list):
    """Generate variation records, reusing existing records where the phrase already exists.

    Bug fix: the previous implementation used list.index() + list.pop() to match existing
    records by phrase.  When a phrase appeared multiple times (different voice/rate variations),
    only the first row was ever matched; subsequent rows were silently lost, and their .wav
    files were then deleted as "obsolete".

    Fix: build a dict[phrase -> list[record]] so every existing row for a phrase is preserved
    in FIFO order, and we pop the front entry for each occurrence of that phrase.
    """
    records = []
    if not voices:
        print("No voices available.")
        return records

    # Build a lookup: phrase_to_speak -> [record, record, ...] (preserves all rows)
    existing_by_phrase: dict[str, list[dict]] = defaultdict(list)
    for rec in existing_records:
        existing_by_phrase[rec['phrase_to_speak']].append(rec)

    for idx, (phrase, label, speech) in enumerate(tqdm(zip(phrases, labels, speech_to_detect), desc="Generating variations", total=len(phrases))):
        if idx % SUBSAMPLE_RATE != 0:
            continue
        if existing_by_phrase.get(phrase):
            records.append(existing_by_phrase[phrase].pop(0))
            continue
        voice = random.choice(voices)['ShortName']
        speech_rate = random.randint(MIN_SPEECH_RATE, MAX_SPEECH_RATE)
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
        print(f"Loaded {len(existing_records)} existing variation records from {paths.output_file}...")
    except FileNotFoundError:
        print(f"Did not find existing {paths.output_file}.")
        existing_records = []

    print("Fetching available voices...")
    voices = await get_voices()
    print("Generating variation records...")
    variation_records = await generate_variation_records(paths, phrases, labels, speech_to_detect, existing_records, voices)
    print(f"Saving variation records to {paths.output_file}...")
    variations_df = pd.DataFrame(variation_records)
    variations_df.to_csv(paths.output_file, index=False, encoding='utf-8')
    print("Deleting obsolete speech samples.")
    # Remove files from samples_dir that are not in the new variations_df
    if paths.samples_dir.exists():
        for file in paths.samples_dir.iterdir():
            if not variations_df["sample_file_name"].eq(file.name).any():
                print(f"Deleting obsolete sample file: {file.name}")
                file.unlink()


if __name__ == "__main__":
    asyncio.run(main())
