import argparse
from pathlib import Path
import os
import asyncio
from tqdm import tqdm
import aiohttp

# Settings
download_urls = [
    "https://svn.code.sf.net/p/cmusphinx/code/trunk/cmudict/cmudict-0.7b",
    "https://svn.code.sf.net/p/cmusphinx/code/trunk/cmudict/cmudict-0.7b.phones"
]

# Parse command-line arguments
parser = argparse.ArgumentParser(description="Download phoneme dictionary files.")
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for output of downloaded files')
paths = parser.parse_args()

os.makedirs(paths.output_dir, exist_ok=True)

async def download_file(session, url, output_dir):
    filename = url.split("/")[-1]
    output_path = Path(output_dir) / filename
    async with session.get(url) as resp:
        resp.raise_for_status()
        with open(output_path, "wb") as f:
            async for chunk in resp.content.iter_chunked(1024):
                f.write(chunk)

async def main():
    async with aiohttp.ClientSession() as session:
        tasks = [download_file(session, url, paths.output_dir) for url in download_urls]
        for f in tqdm(asyncio.as_completed(tasks), total=len(tasks), desc="Downloading"):
            await f

if __name__ == "__main__":
    asyncio.run(main())

