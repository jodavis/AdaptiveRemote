import string
import argparse
from pathlib import Path
import os
from tqdm import tqdm
import pandas as pd
import json
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.phoneme_utils import load_phoneme_dict, build_trie

# Settings

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Download phoneme dictionary files.")
parser.add_argument('--phoneme-dictionary-dir', type=Path, required=True, help='Directory containing phoneme dictionary files')
parser.add_argument('--training-data-file', type=Path, required=True, help='Path to training data CSV file')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output of vocabulary files')

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


if __name__ == "__main__":
    paths = parser.parse_args()

    os.makedirs(paths.output_dir, exist_ok=True)

    # 1. Load phoneme dictionary
    phoneme_dict = load_phoneme_dict(paths.phoneme_dictionary_dir)

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
