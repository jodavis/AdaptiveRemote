import argparse
from pathlib import Path
import os
import numpy as np
import pandas as pd
from tqdm import tqdm
from zipfile import ZipFile

print("Initializing TensorFlow...")
import tensorflow as tf

# Settings
input_token_length = 20 # max length of input token sequences
n_mels = 80 # number of mel frequency bins
time_steps = 360 # number of time steps in spectrogram input
batch_size = 32 # evaluation batch size

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Evaluate test samples and create ZIP of successfully recognized files.")
parser.add_argument('--manifest', type=Path, required=True, help='Path to test_manifest.csv')
parser.add_argument('--model', type=Path, required=True, help='Path to model file (speech_to_text_model.keras)')
parser.add_argument('--vocab', type=Path, required=True, help='Path to vocab_list.txt')
parser.add_argument('--spectrogram-dir', type=Path, required=True, help='Directory with spectrogram npy files')
parser.add_argument('--output-zip', type=Path, required=True, help='Path for output zip file')
paths = parser.parse_args()

os.makedirs(paths.output_zip.parent, exist_ok=True)


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
    tokens_file = paths.spectrogram_dir / f"{wav_filename}_tokens.npy"
    # Load the numpy array from the npy file
    x_eval.append(np.load(spectrogram_file))
    y_eval.append(np.load(tokens_file))

# Load the trained model
print("Loading speech-to-text model...")
model = tf.keras.models.load_model(paths.model)
print(f"Loaded {paths.model}")

# Load the vocabulary list from vocab file
with open(paths.vocab, 'r', encoding='utf-8') as vocabfile:
    vocab_list = [line.strip() for line in vocabfile if line.strip()]
    ctc_blank_idx = len(vocab_list)  # CTC blank token is conventionally at the last index
    print(f"Vocabulary size: {len(vocab_list)}, Number of classes (with CTC blank): {len(vocab_list) + 1}")

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

# Convert predicted token indices to text
success_files = []
def tokens_to_text(tokens):
    return ''.join([vocab_list[idx] for idx in tokens if idx < len(vocab_list)])

for i, (pred_tokens, true_tokens) in enumerate(zip(all_preds, y_eval)):
    pred_text = tokens_to_text(pred_tokens)
    true_text = tokens_to_text(true_tokens)
    if pred_text == true_text:
        wav_path = eval_set.iloc[i]['filepath']
        success_files.append(wav_path)

# Add successfully matched files to ZIP
with ZipFile(paths.output_zip, 'w') as zipf:
    for file_path in success_files:
        zipf.write(file_path, arcname=Path(file_path).name)

print(f"Successfully matched and added {len(success_files)} files to {paths.output_zip}")

