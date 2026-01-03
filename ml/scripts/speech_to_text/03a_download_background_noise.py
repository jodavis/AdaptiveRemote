import argparse
import os
from pathlib import Path
import requests

# Settings
# Noise samples
noise_samples = {
    "creative-background-short-ver.wav": "https://cdn.freesound.org/sounds/721/721949-a0b57121-2a03-4dac-97c0-ee15fc5db207?filename=721949__audiocoffee__creative-background-short-ver.wav",
    "trailer.wav": "https://cdn.freesound.org/sounds/785/785516-53995c18-2299-49bc-b042-357c8cb919fd?filename=785516__litesaturation__trailer.wav",
    "tv-chatter.wav": "https://cdn.freesound.org/sounds/765/765157-8a98bb7d-6d3d-4869-af6c-4ba18aaddf27?filename=765157__mieckevanhoek__tv-chatter.wav",
    "tv-news-loop.wav": "https://cdn.freesound.org/sounds/468/468539-e433c8eb-7f21-467d-9910-a37f4738c868?filename=468539__sergequadrado__tv-news-loop.wav",
    "tv-recording-of-a-handball-match-3.wav": "https://cdn.freesound.org/sounds/786/786263-6ef16c1d-183a-4143-beca-6b9528e9cdb5?filename=786263__king_anna__tv-recording-of-a-handball-match-3.wav",
}

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Download background noise samples.")
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for downloaded noise wav files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

# Download noise samples
for filename, url in noise_samples.items():
    output_path = paths.output_dir / filename
    if output_path.exists():
        print(f"File {output_path} already exists. Skipping download.")
        continue
    print(f"Downloading {filename} from {url}...")
    response = requests.get(url, timeout=30, stream=True)
    response.raise_for_status()
    with open(output_path, "wb") as f:
        for chunk in response.iter_content(chunk_size=8192):
            f.write(chunk)
    print(f"Saved to {output_path}")

