from __future__ import annotations

import argparse
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.io.audio_io import LibrosaAudioReader, SoundfileAudioWriter
from pipeline.speech.background_noise_stage import BackgroundNoiseAugmentor
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


class _DirectoryNoiseProvider:
    """NoiseProvider backed by a filesystem directory.

    Lists all WAV files in the given directory. DVC wiring points this at the
    appropriate data/ path; this class requires no knowledge of the DVC layout.

    Raises ValueError with the directory path if the directory contains no WAV
    files, so that a misconfigured --noise-dir produces an actionable error
    rather than the opaque ValueError("options must be non-empty") from
    VariationGenerator.choose().
    """

    def __init__(self, noise_dir: Path) -> None:
        self._noise_dir = noise_dir

    def list_files(self) -> list[Path]:
        files = list(self._noise_dir.glob("*.wav"))
        if not files:
            raise ValueError(f"No WAV files found in noise directory: {self._noise_dir}")
        return files


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
        audio_reader=LibrosaAudioReader(),
        audio_writer=SoundfileAudioWriter(),
        input_dir=args.input_manifest_dir,
        noise_dir=args.noise_dir,
        noise_provider=_DirectoryNoiseProvider(args.noise_dir),
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
