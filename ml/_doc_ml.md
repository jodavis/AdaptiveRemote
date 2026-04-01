# ML Pipeline Design — AdaptiveRemote (current implementation)

## Purpose and Scope

This document describes the ML pipeline in the `ml` folder. It includes DVC stages, scripts, inputs and outputs, how to run it locally for development, and a short set of next steps. The pipeline is implemented for local Windows development and uses DVC to manage data and artifacts.

## Repository layout (relevant paths)

- `ml/dvc.yaml` — pipeline orchestration and stage definitions.
- `ml/scripts/intent_prediction/` — scripts to generate intent phrase variations (`01_generate_phrases.py` and `01_input_phrases.csv`).
- `ml/scripts/speech_to_text/` — speech sample generation, augmentation, featurization, training, and evaluation scripts (`01*`–`09*`).
- `ml/data/` — DVC-tracked raw, intermediate, and output artifacts (manifests, spectrograms, models). This data is managed by DVC, not committed to the Git repo.

## Implemented DVC stages and scripts

The pipeline for both speech-to-text and intent prediction is defined with explicit stages in [`ml/dvc.yaml`](./dvc.yaml). See that file for stage names, inputs, and outputs. 

Each stage in `dvc.yaml` declares the exact command, dependencies, and outputs used by the pipeline. The scripts follow a consistent CLI convention (required `--input`/`--manifest`/`--output` arguments) and each stage saves outputs into its own folder in the the `ml/data` tree.

## Implementation details (summary)

- Intent generation: `01_generate_phrases.py` reads `01_input_phrases.csv`, synthesizes surface-form variations (adding pleasantries, hesitations, spelling variants, and repeats), and writes `training_data.csv` used as input to both intent prediction and speech sample generation.
- Speech sample generation and augmentation: scripts `01*`–`04*` in speech_to_text generate TTS or synthetic samples, randomize delays, add background and microphone noise, and write clean/noisy audio files into DVC-backed directories.
- Manifests and vocab: `05_create_set_manifests.py` creates train/val CSV manifests referencing audio filepaths and expected transcripts; `02_compute_vocab.py` builds `phoneme_list.txt` from the transcripts; `06_compute_token_lists.py` uses the manifests and `phoneme_list.txt` to produce per-utterance token JSON files.
- Featurization: `07_compute_spectrograms.py` reads manifests, computes fixed-size log-Mel spectrograms (`*.npy`), and stores them in the spectrogram output directory.
- Training: `08_train_model.py` loads the training manifest, spectrogram `.npy` files, and token JSON files, constructs a Keras model (Conv2D → BiLSTM → Dense), trains with a CTC-style loss loop, and saves `speech_to_text_model.keras`.
- Evaluation: `09_evaluate_model.py` loads the saved model, runs greedy CTC decoding on eval spectrograms, computes WER using `jiwer`, and writes an `evaluation_predictions.txt` report.

## Dependencies and environment

- See `ml/scripts/requirements.txt` for required Python packages.
- The code is written to run on Windows for development (CPU TensorFlow). Model training and larger-scale runs can be moved to Linux GPU hosts with minimal changes.

## How to run (developer quickstart)

1. Ensure DVC is installed and configured for your environment.
2. From the repository root:

```powershell
cd ml
pip install -r scripts/requirements.txt
dvc pull
dvc repro
```

`dvc repro` will execute the defined stages in the correct order and populate `ml/data` with outputs. Inspect the `dvc.yaml` file for per-stage commands and I/O when you need to run or debug a particular step.

## Observations and current limitations

- The training loop in `08_train_model.py` implements a simple custom training loop with CTC loss; model hyperparameters (epochs, batch_size, time_steps) are hardcoded and could be parameterized.
- The dataset generation is entirely file-based; large intermediate artifacts (audio, spectrograms, models) are stored under `ml/data` and should be pushed to the DVC remote to share across machines.
- There is a simple greedy CTC decoder in `09_evaluate_model.py`; for production accuracy reporting, a beam search decoder could be added.
- Scripts assume certain manifest and file conventions (manifest columns include `filepath` and `speech_to_detect`). Changes to manifest format will require updating `07*`, `08*`, and `09*` scripts.

## Next steps (practical, minimal changes)

- Parameterize training and featurization hyperparameters via CLI args or a YAML config to avoid editing source for experiments.
- Pin dependency versions in `ml/scripts/requirements.txt` (or add `requirements.lock`) for reproducibility.
- Add a small-sample smoke dataset and a CI job that runs `dvc pull` + `dvc repro` on that sample to detect regressions.
- Add a minimal `model_registry.json` (template) that records model metadata (train commit, metrics, DVC path) when a training run completes.
- Add a simple cleanup helper to remove local intermediate files not referenced by DVC.

