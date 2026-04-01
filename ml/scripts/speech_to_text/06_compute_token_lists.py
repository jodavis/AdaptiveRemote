import argparse
import json
from pathlib import Path
import os
import pandas as pd
from tqdm import tqdm
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import INPUT_TOKEN_LENGTH
from shared.io_utils import write_token_list, read_phoneme_list, read_manifest

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Compute token lists for audio files.")

parser.add_argument('--train-manifest', type=Path, required=True, help='Path to train_manifest.csv')
parser.add_argument('--eval-manifest', type=Path, required=True, help='Path to eval_manifest.csv')
parser.add_argument('--test-manifest', type=Path, required=True, help='Path to test_manifest.csv')
parser.add_argument('--phoneme-list', type=Path, required=True, help='Path to phoneme_list.txt')
parser.add_argument('--words-to-phonemes', type=Path, required=True, help='Path to words_to_phonemes.json')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output token JSON files')


def compute_tokens(phoneme_list, words_to_phonemes, transcription):
	# Convert transcription to uppercase and split into words
	words = transcription.upper().replace(',', ' ').split()
	phonemes = []
	for word in words:
		if word in words_to_phonemes:
			phonemes.extend(words_to_phonemes[word])
		# If word not in mapping, skip it (could log warning if desired)
	# Convert phonemes to indices
	tokens = [phoneme_list.index(ph) for ph in phonemes if ph in phoneme_list]
	tokens = tokens + [len(phoneme_list)] * (INPUT_TOKEN_LENGTH - len(tokens)) if len(tokens) < INPUT_TOKEN_LENGTH else tokens[:INPUT_TOKEN_LENGTH]
	return phonemes, tokens


def load_rows_from_manifest(manifest_path):
	df = read_manifest(manifest_path)
	print(f"Loaded {len(df)} samples from {manifest_path}.")
	return [(row['filepath'], row['speech_to_detect']) for _, row in df.iterrows()]


if __name__ == "__main__":
	paths = parser.parse_args()

	os.makedirs(paths.output_dir, exist_ok=True)

	# Read the phoneme list from file
	phoneme_list, pad_value = read_phoneme_list(paths.phoneme_list)
	print(f"Loaded phoneme list with {len(phoneme_list)} entries.")

	# Read the words-to-phonemes mapping from JSON
	with open(paths.words_to_phonemes, 'r', encoding='utf-8') as w2p_file:
		words_to_phonemes = json.load(w2p_file)

	manifest_rows = (
		load_rows_from_manifest(paths.train_manifest)
		+ load_rows_from_manifest(paths.eval_manifest)
		+ load_rows_from_manifest(paths.test_manifest)
	)

	for wav_path, transcription in tqdm(manifest_rows, desc="Computing token lists from manifests", total=len(manifest_rows)):
		wav_path = Path(wav_path)
		try:
			phonemes, tokens = compute_tokens(phoneme_list, words_to_phonemes, transcription)
			write_token_list(paths.output_dir, wav_path.stem, phonemes, tokens)
		except Exception as e:
			print(f'Error processing {wav_path}: {e}')
