import string
import argparse
from pathlib import Path
import os
from tqdm import tqdm
import pandas as pd
import json
from collections import defaultdict

# Settings

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Download phoneme dictionary files.")
parser.add_argument('--phoneme-dictionary-dir', type=Path, required=True, help='Directory containing phoneme dictionary files')
parser.add_argument('--training-data-file', type=Path, required=True, help='Path to training data CSV file')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output of vocabulary files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

word_translation_table = str.maketrans({
    **{
        '1': 'ONE',
        '2': 'TWO',
        '3': 'THREE',
        '4': 'FOR',
        '5': 'FIVE',
        '6': 'SIX',
        '7': 'SEVEN',
        '8': 'EIGHT',
        '9': 'NINE',
    },
    **{p: ' ' for p in string.punctuation}
})

# --- Helper functions ---
def load_phoneme_dict(phoneme_dict_dir):
    """Load all phoneme dictionary files in the directory into a single dict."""
    phoneme_dict = {}
    dict_path = phoneme_dict_dir / "cmudict-0.7b"
    with open(dict_path) as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or line.startswith(";;;"): continue
            parts = line.split()
            word = parts[0]
            # Strip trailing numerals from each phoneme
            phonemes = [ ''.join([c for c in p if c.isalpha()]) for p in parts[1:] ]
            if word not in phoneme_dict:
                phoneme_dict[word] = phonemes
    phoneme_dict['NETFLIX'] = ['N', 'EH', 'T', 'F', 'L', 'IH', 'K', 'S']  # Add custom entry for Netflix
    return phoneme_dict

def extract_words_from_csv(training_data_file):
    """Extract all distinct words from the 'surface_form' column of the training data CSV file, stripping punctuation."""
    words = set()
    df = pd.read_csv(training_data_file, encoding="utf-8")
    if 'surface_form' not in df.columns:
        raise ValueError("'surface_form' column not found in training data file.")
    # Create translation table: punctuation -> space
    for cell in df['surface_form'].dropna():
        # Replace punctuation with spaces
        cell_clean = str(cell).translate(word_translation_table)
        for word in cell_clean.split():
            if word:
                words.add(word.upper())
    return sorted(words)

def build_trie(word_to_phonemes):
    """Build a trie mapping phoneme sequences to words."""
    root = {}
    for word, phonemes in word_to_phonemes.items():
        node = root
        for phoneme in phonemes:
            node = node.setdefault(phoneme, {})
        node.setdefault("$", []).append(word)
    return root

# --- Main logic ---

# 1. Load phoneme dictionary
dict_path = paths.phoneme_dictionary_dir
phoneme_dict = load_phoneme_dict(dict_path)

# 2. Extract words from training data
words = extract_words_from_csv(paths.training_data_file)

# 3. Map words to phoneme sequences
word_to_phonemes = {}
missing_words = []
for word in words:
    if word in phoneme_dict:
        word_to_phonemes[word] = phoneme_dict[word]
    else:
        missing_words.append(word)

if missing_words:
    print(f"Warning: {len(missing_words)} words not found in phoneme dictionary. They will be skipped.")
    for entry in missing_words[:10]:  # Print first 10 missing words
        print(f"  - {entry}")

# 4. Collect all phonemes used
phoneme_set = set()
for phonemes in word_to_phonemes.values():
    phoneme_set.update(phonemes)
phoneme_list = sorted(phoneme_set)

# 5. Output JSON file: words and their phoneme sequences
words_json_path = Path(paths.output_dir) / "words_to_phonemes.json"
with open(words_json_path, "w", encoding="utf-8") as f:
    json.dump({w: word_to_phonemes[w] for w in sorted(word_to_phonemes)}, f, indent=2, ensure_ascii=False)

# 6. Output phoneme list file
phoneme_list_path = Path(paths.output_dir) / "phoneme_list.txt"
with open(phoneme_list_path, "w", encoding="utf-8") as f:
    for phoneme in phoneme_list:
        f.write(phoneme + "\n")

# 7. Output trie JSON file
trie = build_trie(word_to_phonemes)
trie_json_path = Path(paths.output_dir) / "phoneme_trie.json"
with open(trie_json_path, "w", encoding="utf-8") as f:
    json.dump(trie, f, indent=2, ensure_ascii=False)

