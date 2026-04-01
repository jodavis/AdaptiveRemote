import argparse
from pathlib import Path
import os
import numpy as np
from tqdm import tqdm
from jiwer import wer
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import BATCH_SIZE
from shared.ctc_utils import ctc_greedy_decode, indices_to_words, trim_at_blank
from shared.io_utils import read_spectrogram, read_token_list, read_phoneme_list, read_manifest

print("Initializing TensorFlow...")
import tensorflow as tf

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Evaluate speech-to-text model.")
parser.add_argument('--manifest', type=Path, required=True, help='Path to val_manifest.csv')
parser.add_argument('--model', type=Path, required=True, help='Path to model file (speech_to_text_model.keras)')
parser.add_argument('--vocab', type=Path, required=True, help='Path to vocab_list.txt')
parser.add_argument('--spectrogram-dir', type=Path, required=True, help='Directory with spectrogram npy files')
parser.add_argument('--token-list-dir', type=Path, required=True, help='Directory with token list JSON files')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for evaluation results (predictions and metrics)')

if __name__ == "__main__":
    paths = parser.parse_args()

    os.makedirs(paths.output_dir, exist_ok=True)

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
    print(
        f"Vocabulary size: {len(vocab_list)}, "
        f"Number of classes (with CTC blank): {len(vocab_list) + 1}, "
        f"CTC blank index: {ctc_blank_idx}"
    )

    eval_dataset = tf.data.Dataset.from_tensor_slices((x_eval, y_eval))\
        .batch(BATCH_SIZE).prefetch(tf.data.AUTOTUNE)

    # Evaluate on eval set
    all_preds = []
    for batch, _ in eval_dataset:
        pred = model.predict(batch)
        all_preds.extend(ctc_greedy_decode(pred, blank=ctc_blank_idx))

    # Compute WER
    refs = [' '.join(indices_to_words(trim_at_blank(seq, vocab_list), vocab_list)) for seq in y_eval]
    hyps = [' '.join(indices_to_words(seq, vocab_list)) for seq in all_preds]
    wer_score = wer(refs, hyps)
    print(f'WER: {wer_score:.3f}')

    # Show a few example predictions
    for i in range(min(5, refs.__len__())):
        print('REF:', refs[i])
        print('HYP:', hyps[i])
        print()

    # Save all predictions to a file in output dir
    output_predictions_file = paths.output_dir / "evaluation_predictions.txt"
    with open(output_predictions_file, 'w', encoding='utf-8') as f:
        f.write(f'WER: {wer_score:.3f}\n\n')
        for ref, hyp in zip(refs, hyps):
            f.write(f'REF: {ref}\n')
            f.write(f'HYP: {hyp}\n\n')
    print(f"Saved evaluation predictions to {output_predictions_file}")
