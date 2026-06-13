"""Download the CMU Pronouncing Dictionary (cmudict-0.7b).

Usage:
    python download_phoneme_dictionary.py --output-dir DIR

Output: <output-dir>/cmudict-0.7b
"""

from __future__ import annotations

import argparse
import urllib.request
from pathlib import Path

_URL = "http://svn.code.sf.net/p/cmusphinx/code/trunk/cmudict/cmudict-0.7b"
_FILENAME = "cmudict-0.7b"


def main() -> None:
    parser = argparse.ArgumentParser(description="Download CMU Pronouncing Dictionary")
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    output_dir: Path = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    output_file = output_dir / _FILENAME
    print(f"Downloading CMU Pronouncing Dictionary to {output_file} ...")
    urllib.request.urlretrieve(_URL, output_file)
    print("Done.")


if __name__ == "__main__":
    main()
