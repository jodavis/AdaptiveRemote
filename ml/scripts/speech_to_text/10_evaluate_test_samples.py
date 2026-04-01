import argparse
from pathlib import Path
import os
import numpy as np
from tqdm import tqdm
from zipfile import ZipFile
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import BATCH_SIZE
from shared.ctc_utils import ctc_greedy_decode, tokens_to_text
from shared.io_utils import read_spectrogram, read_token_list, read_phoneme_list, read_manifest

print("Initializing TensorFlow...")
import tensorflow as tf

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Evaluate test samples and create ZIP of successfully recognized files.")
parser.add_argument('--manifest', type=Path, required=True, help='Path to test_manifest.csv')
parser.add_argument('--model', type=Path, required=True, help='Path to model file (speech_to_text_model.keras)')
parser.add_argument('--vocab', type=Path, required=True, help='Path to vocab_list.txt')
parser.add_argument('--spectrogram-dir', type=Path, required=True, help='Directory with spectrogram npy files')
parser.add_argument('--token-list-dir', type=Path, required=True, help='Directory with token list JSON files')
parser.add_argument('--output-zip', type=Path, required=True, help='Path for output zip file')

if __name__ == "__main__":
    paths = parser.parse_args()

    os.makedirs(paths.output_zip.parent, exist_ok=True)

    # Read the sample file names from manifest
    eval_set = read_manifest(paths.manifest)
    print(f"Loaded {len(eval_set)} evaluation samples from manifest.")

    # Prepare input/output pairs for evaluation
    x_eval = []
    y_eval = []
    for _, row in tqdm(eval_set.iterrows(), total=len(eval_set), desc="Loading evaluation data"):
        wav_filename = Path(row['filepath']).stem
        x_eval.append(read_spectrogram(paths.spectrogram_dir, wav_filename))
        y_eval.append(read_token_list(paths.token_list_dir, wav_filename))

    # Load the trained model
    print("Loading speech-to-text model...")
    model = tf.keras.models.load_model(paths.model)
    print(f"Loaded {paths.model}")

    # Load the vocabulary list from vocab file
    vocab_list, ctc_blank_idx = read_phoneme_list(paths.vocab)
    print(f"Vocabulary size: {len(vocab_list)}, Number of classes (with CTC blank): {len(vocab_list) + 1}")

    eval_dataset = tf.data.Dataset.from_tensor_slices((x_eval, y_eval))\
        .batch(BATCH_SIZE).prefetch(tf.data.AUTOTUNE)

    # Evaluate on eval set
    all_preds = []
    for batch, _ in eval_dataset:
        pred = model.predict(batch)
        all_preds.extend(ctc_greedy_decode(pred, blank=ctc_blank_idx))

    # Collect files where predicted text matches reference
    success_files = []
    for i, (pred_tokens, true_tokens) in enumerate(zip(all_preds, y_eval)):
        pred_text = tokens_to_text(pred_tokens, vocab_list)
        true_text = tokens_to_text(true_tokens, vocab_list)
        if pred_text == true_text:
            wav_path = eval_set.iloc[i]['filepath']
            success_files.append(wav_path)

    # Add successfully matched files to ZIP
    with ZipFile(paths.output_zip, 'w') as zipf:
        for file_path in success_files:
            zipf.write(file_path, arcname=Path(file_path).name)

    print(f"Successfully matched and added {len(success_files)} files to {paths.output_zip}")
