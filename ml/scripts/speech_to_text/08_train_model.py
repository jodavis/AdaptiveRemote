import argparse
from pathlib import Path
import os
import numpy as np
import pandas as pd
from tensorflow.keras import layers, Model, Input
from tqdm import tqdm

print("Initializing TensorFlow...")
import tensorflow as tf

# Settings
input_token_length = 20 # max length of input token sequences
n_mels = 80 # number of mel frequency bins
time_steps = 360 # number of time steps in spectrogram input
epochs = 10 # number of training epochs
batch_size = 32 # training batch size

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Train speech-to-text model.")
parser.add_argument('--manifest', type=Path, required=True, help='Path to train_manifest.csv')
parser.add_argument('--vocab', type=Path, required=True, help='Path to vocab_list.txt')
parser.add_argument('--spectrogram-dir', type=Path, required=True, help='Directory with spectrogram npy files')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output model')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)
output_model_file = paths.output_dir / "speech_to_text_model.keras"

# Read the sample file names from manifest
training_set = pd.read_csv(paths.manifest, encoding='utf-8')
print(f"Loaded {len(training_set)} training samples from manifest.")

# Build the model
with open(paths.vocab, 'r', encoding='utf-8') as vocabfile:
    vocab_list = [line.strip() for line in vocabfile if line.strip()]
    num_classes = len(vocab_list) + 1  # +1 for CTC blank token
    ctc_blank_idx = len(vocab_list)  # CTC blank token is at the last index
    print(f"Vocabulary size: {len(vocab_list)}, Number of classes (with CTC blank): {num_classes}, CTC blank index: {ctc_blank_idx}")

input_layer = Input(shape=(n_mels, time_steps), name='input')
x = layers.Reshape((n_mels, time_steps, 1))(input_layer)
x = layers.Conv2D(32, (3, 3), activation='relu', padding='same')(x)
x = layers.BatchNormalization()(x)
x = layers.MaxPooling2D(pool_size=(2, 2))(x)
x = layers.Conv2D(64, (3, 3), activation='relu', padding='same')(x)
x = layers.BatchNormalization()(x)
x = layers.MaxPooling2D(pool_size=(2, 2))(x)
new_time_steps = x.shape[1]
new_features = x.shape[2] * x.shape[3]
x = layers.Reshape((new_time_steps, new_features))(x)
x = layers.Bidirectional(layers.LSTM(128, return_sequences=True))(x)
output_layer = layers.Dense(num_classes, activation='softmax', name='output')(x)
model = Model(inputs=input_layer, outputs=output_layer)
model.summary()

# Compile model with dummy loss (real loss in Lambda layer)
model.compile(optimizer='adam', loss='categorical_crossentropy')  # placeholder
print("Model compiled.")

x_train = []
y_train = []

# Prepare input/output pairs for training
for _, row in tqdm(training_set.iterrows(), total=len(training_set), desc="Loading training data"):
    wav_path = row['filepath']
    # Get the corresponding spectrogram/tokens NPY file path
    wav_filename = Path(wav_path).stem
    spectrogram_file = paths.spectrogram_dir / f"{wav_filename}.npy"
    tokens_file = paths.spectrogram_dir / f"{wav_filename}_tokens.npy"
    # Load the numpy array from the npy file
    x_train.append(np.load(spectrogram_file))
    y_train.append(np.load(tokens_file))

print(f"Prepared {len(x_train)} input-output pairs for training.")

train_dataset = tf.data.Dataset.from_tensor_slices((x_train, y_train))\
    .batch(batch_size).prefetch(tf.data.AUTOTUNE)

history = []
for epoch in range(epochs):
    epoch_loss = []
    for batch in tqdm(train_dataset, desc=f"Epoch {epoch+1}/{epochs}"):
        x_batch, y_batch = batch
        with tf.GradientTape() as tape:
            y_pred = model(x_batch, training=True)
            # Compute prediction lengths (time steps of y_pred)
            pred_len = tf.fill([tf.shape(y_pred)[0], 1], tf.shape(y_pred)[1])
            # Compute true label lengths by counting non-padding tokens (assumes 0 is padding)
            lbl_len = tf.math.count_nonzero(y_batch, axis=1, dtype=tf.int32)
            lbl_len_reshaped = tf.expand_dims(lbl_len, axis=1)
            loss = tf.keras.backend.ctc_batch_cost(y_batch, y_pred, pred_len, lbl_len_reshaped)
        grads = tape.gradient(loss, model.trainable_variables)
        model.optimizer.apply_gradients(zip(grads, model.trainable_variables))
        epoch_loss.append(tf.reduce_mean(loss).numpy())
    print(f'Epoch {epoch+1}/{epochs} - Loss: {np.mean(epoch_loss):.4f}')
    history.append(np.mean(epoch_loss))

# Save the trained model in Keras format
model.save(output_model_file)
print(f"Model saved to {output_model_file}")