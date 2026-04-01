
import argparse
import os
from pathlib import Path
import random
import csv
import re
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import TRAINING_SET_PERCENTAGE, VALIDATION_SET_PERCENTAGE, TEST_SET_PERCENTAGE

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Create set manifests for training, validation, and test sets.")
parser.add_argument('--input-manifest', type=Path, required=True, help='Path to input manifest CSV (training_data.csv)')
parser.add_argument('--clean-dir', type=Path, required=True, help='Directory with clean speech samples')
parser.add_argument('--noisy-dir', type=Path, required=True, help='Directory with noisy speech samples')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output manifest files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

# Read surface_form from input manifest
surface_forms = []
with open(paths.input_manifest, newline='', encoding='utf-8') as csvfile:
    reader = csv.DictReader(csvfile)
    for row in reader:
        surface_forms.append(row['speech_to_detect'])

# Collect all files from clean and noisy dirs
all_files = []
for input_dir in [paths.clean_dir, paths.noisy_dir]:
    for root, _, files in os.walk(input_dir):
        for file in files:
            file_path = Path(root) / file
            all_files.append(str(file_path.resolve()))

# Shuffle the list (seeded for reproducibility)
random.seed(42)
random.shuffle(all_files)

total_files = len(all_files)
train_count = int(total_files * TRAINING_SET_PERCENTAGE / 100)
val_count = int(total_files * VALIDATION_SET_PERCENTAGE / 100)
test_count = total_files - train_count - val_count

train_files = all_files[:train_count]
val_files = all_files[train_count:train_count+val_count]
test_files = all_files[train_count+val_count:]


# Helper to extract number between underscores
def extract_number_from_filename(filename):
    match = re.search(r'_(\d+)_', filename)
    if match:
        return int(match.group(1))
    return None

def write_manifest(file_list, manifest_path):
    with open(manifest_path, 'w', newline='', encoding='utf-8') as csvfile:
        writer = csv.writer(csvfile)
        writer.writerow(['filepath', 'speech_to_detect'])
        for f in file_list:
            num = extract_number_from_filename(os.path.basename(f))
            surface_form = surface_forms[num] if num is not None and num < len(surface_forms) else ''
            writer.writerow([f, surface_form])

write_manifest(train_files, paths.output_dir / 'train_manifest.csv')
write_manifest(val_files, paths.output_dir / 'val_manifest.csv')
write_manifest(test_files, paths.output_dir / 'test_manifest.csv')
