"""Download the CMU Pronouncing Dictionary (cmudict-0.7b) into data/phoneme_dictionary/.

Usage:
    python download_phoneme_dictionary.py

Output: data/phoneme_dictionary/cmudict-0.7b
"""

from __future__ import annotations

import urllib.request
from pathlib import Path

_URL = "http://svn.code.sf.net/p/cmusphinx/code/trunk/cmudict/cmudict-0.7b"
_OUTPUT_DIR = Path(__file__).parent / "data" / "phoneme_dictionary"
_OUTPUT_FILE = _OUTPUT_DIR / "cmudict-0.7b"


def main() -> None:
    _OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Downloading CMU Pronouncing Dictionary to {_OUTPUT_FILE} ...")
    urllib.request.urlretrieve(_URL, _OUTPUT_FILE)
    print("Done.")


if __name__ == "__main__":
    main()
