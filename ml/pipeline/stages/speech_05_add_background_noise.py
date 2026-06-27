from __future__ import annotations

import argparse
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.io.audio_io import AudioData, LibrosaAudioReader, SoundfileAudioWriter
from pipeline.speech.background_noise_stage import BackgroundNoiseAugmentor
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


class _DirectoryNoiseProvider:
    """NoiseProvider backed by a filesystem directory.

    Loads all WAV files in the given directory at construction time, resampling
    them to ``sample_rate`` so that all noise audio matches the pipeline sample
    rate. This avoids per-sample I/O at augmentation time and ensures noise
    files are at the correct sample rate before mixing.

    Raises ValueError with the directory path if the directory contains no WAV
    files, so that a misconfigured --noise-dir produces an actionable error.
    """

    def __init__(self, noise_dir: Path, sample_rate: int) -> None:
        self._noise_dir = noise_dir
        self._items: list[tuple[str, AudioData]] = self._load(noise_dir, sample_rate)

    @staticmethod
    def _load(noise_dir: Path, sample_rate: int) -> list[tuple[str, AudioData]]:
        import librosa

        wav_paths = sorted(noise_dir.glob("*.wav"))
        if not wav_paths:
            raise ValueError(f"No WAV files found in noise directory: {noise_dir}")

        items: list[tuple[str, AudioData]] = []
        for path in wav_paths:
            # librosa.load with sr=sample_rate resamples on load if needed.
            samples, sr = librosa.load(str(path), sr=sample_rate, mono=True, dtype="float32")
            items.append((path.name, AudioData(samples=samples, sample_rate=int(sr))))
        return items

    def list_files(self) -> list[tuple[str, AudioData]]:
        return list(self._items)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Add environmental background noise to WAV audio samples"
    )
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--noise-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    store = ManifestStore()
    input_manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

    args.output_dir.mkdir(parents=True, exist_ok=True)

    stage = BackgroundNoiseAugmentor(
        output_dir=args.output_dir,
        manifest_store=store,
        audio_reader=LibrosaAudioReader(params.sample_rate),
        audio_writer=SoundfileAudioWriter(),
        input_dir=args.input_manifest_dir,
        noise_provider=_DirectoryNoiseProvider(args.noise_dir, params.sample_rate),
        params=params.add_background_noise,
    )

    asyncio.run(
        stage.transform(
            input_manifest,
            conventions.manifest_path(args.output_dir),
        )
    )


if __name__ == "__main__":
    main()
