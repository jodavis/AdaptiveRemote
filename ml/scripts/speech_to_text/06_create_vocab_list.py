import argparse
import csv
from pathlib import Path
import os

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Create vocabulary list from training data CSV.")
parser.add_argument('--input-file', type=Path, required=True, help='Path to input CSV file (training_data.csv)')
parser.add_argument('--output-file', type=Path, required=True, help='Path for output vocab list')
paths = parser.parse_args()

os.makedirs(paths.output_file.parent, exist_ok=True)
vocab_file = paths.output_file

# Load phrases from CSV and create vocab list
words = set()
with open(paths.input_file, newline='', encoding='utf-8') as csvfile:
    reader = csv.DictReader(csvfile)
    for row in reader:
        phrase = row.get('speech_to_detect', '').lower()
        for word in [w.strip() for w in phrase.replace(',', ' ').split()]:
            if word:
                words.add(word)

vocab_list = sorted(words)

with open(vocab_file, 'w', encoding='utf-8') as f:
    for word in vocab_list:
        f.write(word + '\n')

