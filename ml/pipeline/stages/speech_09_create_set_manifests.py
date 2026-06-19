from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from pipeline.core.manifest import ManifestStore
from pipeline.speech.set_splitter import SetManifestSplitter
from pipeline.stages import conventions
from pipeline.stages.params import PipelineParams

_PROJECT_ROOT = Path(__file__).parents[2]


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Split augmented manifest into train/val/test sets"
    )
    parser.add_argument("--input-manifest-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    params = PipelineParams.load(conventions.params_path(_PROJECT_ROOT))

    store = ManifestStore()
    input_manifest = store.read(conventions.manifest_path(args.input_manifest_dir))

    args.output_dir.mkdir(parents=True, exist_ok=True)

    splitter = SetManifestSplitter(
        output_dir=args.output_dir,
        manifest_store=store,
        train_pct=params.create_set_manifests.train_pct,
        val_pct=params.create_set_manifests.val_pct,
        test_pct=params.create_set_manifests.test_pct,
    )

    splitter.split(input_manifest)


if __name__ == "__main__":
    main()
