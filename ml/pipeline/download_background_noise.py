"""Download background noise WAV files for audio augmentation.

Source: Internet Archive (archive.org) — no authentication required.
To add samples: browse https://archive.org/search?query=tv+background&and[]=mediatype%3A%22audio%22,
then add entries to _NOISE_FILES using the direct download URL pattern:
  https://archive.org/download/<item-id>/<filename>

Downloads are converted to WAV automatically so that the noise directory
always contains WAV files as expected by _DirectoryNoiseProvider. The
original sample rate is preserved; _DirectoryNoiseProvider resamples to
the pipeline sample rate at load time.

Usage:
    python download_background_noise.py --output-dir DIR

Output: one WAV file per noise sample in <output-dir>/.
"""

from __future__ import annotations

import argparse
import tempfile
import urllib.error
import urllib.request
from pathlib import Path

import librosa
import soundfile as sf

# Map output filename (must be .wav) → direct download URL.
# All URLs should be Internet Archive direct-download links of the form:
#   https://archive.org/download/<item-id>/<filename>
# Source files may be MP3 or WAV; they are converted to WAV on download.
_NOISE_FILES: dict[str, str] = {
    "news-program.wav": "https://archive.org/download/wtop-washington-dc-052226-2146-news-cbs-part-1/WTOP%20Washington%20DC%20052226%202146%20news%20-%20CBS%2C%20%20%20part%202.MP3",
    "daytona.wav": "https://dn710200.ca.archive.org/0/items/jamendo-348384/01-1719468-SilverBoxStudio-Daytona%20Synthwave.mp3",
    "king-ralph.wav": "https://dn721604.ca.archive.org/0/items/nbc-tv-king-ralph-9-11-92/NBC%20TV%20King%20Ralph%209-11-92.mp3",
    "super-mario.wav": "https://dn721902.ca.archive.org/0/items/minions-910-movie-clip-kevin-saves-the-day-2015-hd/Minions%20%28910%29%20Movie%20CLIP%20-%20Kevin%20Saves%20the%20Day%20%282015%29%20HD.mp3",
    "simpsons.wav": "https://dn721602.ca.archive.org/0/items/simpsonssleepaudio/The%20Simpsons%20Audio/s03%20-%20e05%20Homer%20Defined.mp3",
}

_USER_AGENT = "AdaptiveRemote-noise-downloader/1.0 (https://github.com/jodavis/AdaptiveRemote)"


def _fetch(url: str, dest: Path) -> None:
    req = urllib.request.Request(url, headers={"User-Agent": _USER_AGENT})
    with urllib.request.urlopen(req) as response, open(dest, "wb") as f:
        f.write(response.read())


def _download_as_wav(url: str, output_path: Path) -> None:
    suffix = Path(url.split("?")[0]).suffix or ".tmp"
    with tempfile.NamedTemporaryFile(suffix=suffix, delete=True) as tmp:
        tmp_path = Path(tmp.name)
        _fetch(url, tmp_path)
        # sr=None preserves original sample rate; _DirectoryNoiseProvider resamples later.
        samples, sr = librosa.load(str(tmp_path), sr=None, mono=True, dtype="float32")
    sf.write(str(output_path), samples, sr, subtype="PCM_16")


def main() -> None:
    parser = argparse.ArgumentParser(description="Download background noise WAV files")
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    if not _NOISE_FILES:
        print("No files configured. Add entries to _NOISE_FILES in this script.")
        return

    output_dir: Path = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)

    for filename, url in _NOISE_FILES.items():
        if not filename.endswith(".wav"):
            print(f"  SKIP {filename}: output filename must end in .wav")
            continue

        output_path = output_dir / filename
        if output_path.exists():
            print(f"Already downloaded: {output_path}")
            continue

        print(f"Downloading {filename} ...")
        try:
            _download_as_wav(url, output_path)
        except urllib.error.HTTPError as e:
            print(f"  ERROR {e.code}: {url}")
            continue
        print(f"Saved to {output_path}")


if __name__ == "__main__":
    main()
