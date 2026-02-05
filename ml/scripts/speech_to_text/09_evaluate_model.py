import argparse
from pathlib import Path
import os
import json
import numpy as np
import pandas as pd
from tqdm import tqdm
from jiwer import wer

print("Initializing TensorFlow...")
import tensorflow as tf

# Settings
input_token_length = 20 # max length of input token sequences
n_mels = 80 # number of mel frequency bins
time_steps = 360 # number of time steps in spectrogram input
batch_size = 32 # evaluation batch size

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Evaluate speech-to-text model.")
parser.add_argument('--manifest', type=Path, required=True, help='Path to val_manifest.csv')
parser.add_argument('--model', type=Path, required=True, help='Path to model file (speech_to_text_model.keras)')
parser.add_argument('--vocab', type=Path, required=True, help='Path to vocab_list.txt')
parser.add_argument('--spectrogram-dir', type=Path, required=True, help='Directory with spectrogram npy files')
parser.add_argument('--token-list-dir', type=Path, required=True, help='Directory with token list JSON files')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for evaluation results (predictions and metrics)')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

# Read the sample file names from manifest
eval_set = pd.read_csv(paths.manifest, encoding='utf-8')
print(f"Loaded {len(eval_set)} evaluation samples from manifest.")

# Prepare input/output pairs for evaluation
x_eval = []
y_eval = []
for _, row in tqdm(eval_set.iterrows(), total=len(eval_set), desc="Loading evaluation data"):
    wav_path = row['filepath']
    # Get the corresponding spectrogram/tokens NPY file path
    wav_filename = Path(wav_path).stem
    spectrogram_file = paths.spectrogram_dir / f"{wav_filename}.npy"
    tokens_file = paths.token_list_dir / f"{wav_filename}_tokens.json"
    # Load the spectrogram and tokens
    x_eval.append(np.load(spectrogram_file))
    with open(tokens_file, 'r', encoding='utf-8') as f:
        y_eval.append(np.array(json.load(f)['tokens'], dtype=np.int32))

# Load the trained model
print("Loading speech-to-text model...")
model = tf.keras.models.load_model(paths.model)
print(f"Loaded {paths.model}")

# Load the vocabulary list from vocab file
with open(paths.vocab, 'r', encoding='utf-8') as vocabfile:
    vocab_list = [line.strip() for line in vocabfile if line.strip()]
    ctc_blank_idx = len(vocab_list)  # CTC blank token is conventionally the last index
    print(
        f"Vocabulary size: {len(vocab_list)}, "
        f"Number of classes (with CTC blank): {len(vocab_list) + 1}, "
        f"CTC blank index: {ctc_blank_idx}"
    )

def ctc_greedy_decode(pred, blank=ctc_blank_idx):
    pred_ids = np.argmax(pred, axis=-1)
    decoded = []
    for seq in pred_ids:
        prev = blank
        out = []
        for idx in seq:
            if idx != prev and idx != blank:
                out.append(idx)
            prev = idx
        decoded.append(out)
    return decoded

eval_dataset = tf.data.Dataset.from_tensor_slices((x_eval, y_eval))\
    .batch(batch_size).prefetch(tf.data.AUTOTUNE)

# Evaluate on eval set
all_preds = []
for batch, _ in eval_dataset:
    pred = model.predict(batch)
    all_preds.extend(ctc_greedy_decode(pred))

# Convert indices to words
def indices_to_words(indices):
    return [vocab_list[i] if i < len(vocab_list) else '[UNK]' for i in indices]

# Compute WER
def trim_at_blank(seq: np.ndarray, blank_idx):
    idxs = np.where(seq >= len(vocab_list))[0]
    if len(idxs) > 0:
        return seq[:idxs[0]]
    else:
        return seq

refs = [' '.join(indices_to_words(trim_at_blank(seq, ctc_blank_idx))) for seq in y_eval]
hyps = [' '.join(indices_to_words(seq)) for seq in all_preds]
wer_score = wer(refs, hyps)
print(f'WER: {wer_score:.3f}')

# Show a few example predictions
for i in range(5):
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
