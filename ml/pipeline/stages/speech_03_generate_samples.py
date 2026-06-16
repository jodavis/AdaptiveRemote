"""DVC entry-point: generate TTS speech samples and write Manifest[AudioSample].

Usage:
    python speech_03_generate_samples.py
        --input-manifest-dir DIR
        --output-dir DIR

Fetches the en-US female voice list from edge_tts, then runs TtsSampleGenerator
to synthesize speech for every TextSample in the input manifest.
"""

from __future__ import annotations

import argparse
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.speech.tts_stage import EdgeTtsProvider, TtsSampleGenerator
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


def _fetch_voices() -> list[str]:
    import edge_tts

    all_voices = asyncio.run(edge_tts.list_voices())
    voices = sorted([
        v["ShortName"]
        for v in all_voices
        if v["Gender"] == "Female"
        and v["Locale"] == "en-US"
        and ":" not in v["ShortName"]
        and "DragonHD" not in v["ShortName"]
        and "Turbo" not in v["ShortName"]
    ])
    return voices


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate TTS speech samples")
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))
    voices = _fetch_voices()

    store = ManifestStore()
    input_manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

    args.output_dir.mkdir(parents=True, exist_ok=True)

    stage = TtsSampleGenerator(
        output_dir=args.output_dir,
        manifest_store=store,
        tts_provider=EdgeTtsProvider(),
        voices=voices,
        speech_rate_min=params.generate_samples.speech_rate_min,
        speech_rate_max=params.generate_samples.speech_rate_max,
    )

    asyncio.run(
        stage.transform(
            input_manifest,
            conventions.manifest_path(args.output_dir),
        )
    )


if __name__ == "__main__":
    main()
