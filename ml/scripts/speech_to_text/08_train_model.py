import argparse
from pathlib import Path
import os
import numpy as np
from tqdm import tqdm
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))  # adds ml/scripts/ to sys.path

from shared.constants import INPUT_TOKEN_LENGTH, N_MELS, TIME_STEPS, EPOCHS, BATCH_SIZE
from shared.io_utils import read_spectrogram, read_token_list, read_phoneme_list, read_manifest

print("Initializing TensorFlow...")
import tensorflow as tf
from tensorflow.keras import layers, Model, Input

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Train speech-to-text model.")
parser.add_argument('--manifest', type=Path, required=True, help='Path to train_manifest.csv')
parser.add_argument('--vocab', type=Path, required=True, help='Path to vocab_list.txt')
parser.add_argument('--spectrogram-dir', type=Path, required=True, help='Directory with spectrogram npy files')
parser.add_argument('--token-list-dir', type=Path, required=True, help='Directory with token list JSON files')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output model')

if __name__ == "__main__":
    paths = parser.parse_args()

    os.makedirs(paths.output_dir, exist_ok=True)
    output_model_file = paths.output_dir / "speech_to_text_model.keras"

    # Read the sample file names from manifest
    training_set = read_manifest(paths.manifest)
    print(f"Loaded {len(training_set)} training samples from manifest.")

    # Build the model
    vocab_list, ctc_blank_idx = read_phoneme_list(paths.vocab)
    num_classes = len(vocab_list) + 1  # +1 for CTC blank token
    print(f"Vocabulary size: {len(vocab_list)}, Number of classes (with CTC blank): {num_classes}, CTC blank index: {ctc_blank_idx}")

    input_layer = Input(shape=(N_MELS, TIME_STEPS), name='input')
    x = layers.Reshape((N_MELS, TIME_STEPS, 1))(input_layer)
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
        wav_filename = Path(row['filepath']).stem
        x_train.append(read_spectrogram(paths.spectrogram_dir, wav_filename))
        y_train.append(read_token_list(paths.token_list_dir, wav_filename))

    print(f"Prepared {len(x_train)} input-output pairs for training.")

    train_dataset = tf.data.Dataset.from_tensor_slices((x_train, y_train))\
        .batch(BATCH_SIZE).prefetch(tf.data.AUTOTUNE)

    history = []
    for epoch in range(EPOCHS):
        epoch_loss = []
        for batch in tqdm(train_dataset, desc=f"Epoch {epoch+1}/{EPOCHS}"):
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
        print(f'Epoch {epoch+1}/{EPOCHS} - Loss: {np.mean(epoch_loss):.4f}')
        history.append(np.mean(epoch_loss))

    # Save the trained model in Keras format
    model.save(output_model_file)
    print(f"Model saved to {output_model_file}")
