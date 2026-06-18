from __future__ import annotations

import argparse
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.io.audio_io import LibrosaAudioReader, SoundfileAudioWriter
from pipeline.speech.mic_noise_stage import MicrophoneNoiseAugmentor
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Add Gaussian microphone noise to WAV audio samples"
    )
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    store = ManifestStore()
    input_manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

    args.output_dir.mkdir(parents=True, exist_ok=True)

    stage = MicrophoneNoiseAugmentor(
        output_dir=args.output_dir,
        manifest_store=store,
        audio_reader=LibrosaAudioReader(),
        audio_writer=SoundfileAudioWriter(),
        input_dir=args.input_manifest_dir,
        params=params.add_mic_noise,
    )

    asyncio.run(
        stage.transform(
            input_manifest,
            conventions.manifest_path(args.output_dir),
        )
    )


if __name__ == "__main__":
    main()
