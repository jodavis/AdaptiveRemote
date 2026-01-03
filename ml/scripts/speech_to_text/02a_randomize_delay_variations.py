import argparse
from pathlib import Path
import pandas as pd
import random
import os
import asyncio
import edge_tts
from tqdm import tqdm

# Settings
# Delay frequency
prefix_delay_frequency = 5  # Add a delay every N samples
suffix_delay_frequency = 5  # Add a delay every N samples

# Delay duration
max_delay_duration = 1.5  # Maximum delay duration in seconds
min_delay_duration = 0.5  # Minimum delay duration in seconds

# Read file and directory paths from command line arguments
parser = argparse.ArgumentParser(description="Generate speech sample variations.")
parser.add_argument('--input-dir', type=Path, required=True, help='Path to the input directory containing speech samples')
parser.add_argument('--output-file', type=Path, required=True, help='Path to the output CSV file containing random delay values')
paths = parser.parse_args()

os.makedirs(paths.output_file.parent, exist_ok=True)

records = []
for file_path in paths.input_dir.glob('*.wav'):
    stem = file_path.stem

    if random.randint(1, prefix_delay_frequency) == 1:
        prefix_delay = random.uniform(min_delay_duration, max_delay_duration)
        stem = f"{stem}_pre{int(prefix_delay * 1000):04d}"
    else:
        prefix_delay = 0.0

    if random.randint(1, suffix_delay_frequency) == 1:
        suffix_delay = random.uniform(min_delay_duration, max_delay_duration)
        stem = f"{stem}_suf{int(suffix_delay * 1000):04d}"
    else:
        suffix_delay = 0.0

    records.append({
        'input_file_path': str(file_path),
        'prefix_delay_seconds': prefix_delay,
        'suffix_delay_seconds': suffix_delay,
        'new_file_name': f"{stem}{file_path.suffix}"
    })

# Save to CSV
df = pd.DataFrame.from_records(records)
df.to_csv(paths.output_file, index=False, encoding='utf-8')
print(f"Delay variations saved to {paths.output_file}")