# ML: Build & Test

This document describes how to set up, build (install dependencies), and run tests for the Python code under the `ml/` directory.

**Prerequisites:**
- **Python:** Use a modern Python 3.10+ interpreter (3.10–3.11 recommended).
- **Platform notes:** Some packages (TensorFlow, audio libraries) may require platform-specific wheels or system libraries on Windows.

**Quick setup (recommended, Windows / PowerShell)**

1. Create and activate a virtual environment:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

2. Install required Python packages:

```powershell
pip install --upgrade pip
pip install -r scripts/requirements.txt
```

3. Run tests (see details below):

```powershell
pytest
```

If you prefer Command Prompt (cmd.exe):

```cmd
python -m venv .venv
.\.venv\Scripts\activate.bat
pip install -r scripts/requirements.txt
pytest
```

Conda alternative:

```powershell
conda create -n adr-ml python=3.10 -y
conda activate adr-ml
pip install -r scripts/requirements.txt
pytest
```

Test configuration
- Pytest config for the `ml/` package lives in `ml/pyproject.toml`. By default the test runner excludes tests marked with the `e2e` marker.
- Default invocation: `pytest` (run from the `ml/` directory). The `pythonpath` is set so imports work when running inside `ml/`.

Running only unit tests or a single file
- Run a single test file:

```powershell
pytest test/path/to/test_file.py -q
```
- Run a single test by node id (class::method):

```powershell
pytest test/path/to/test_file.py::TestClass::test_method -q
```

End-to-end tests (e2e)
- E2E tests are marked with `e2e` and are excluded by default (they may invoke `dvc repro` and require datasets and DVC remotes).
- To run e2e tests you must have DVC installed and the required data available. A typical invocation that overrides the default exclusion:

```powershell
pytest --override-ini "addopts=-m e2e"
```

Notes & troubleshooting
- TensorFlow: installing `tensorflow` on Windows may require matching the Python version and choosing CPU vs GPU builds. If installation fails, consult TensorFlow installation docs for the correct wheel.
- Audio libraries: `soundfile`, `librosa`, and `pydub` can require platform libraries (libsndfile, ffmpeg). On Windows, install `ffmpeg` and ensure it is on `PATH`.
- If tests fail due to missing data for e2e flows, either fetch DVC-tracked data or skip e2e tests (default behavior).

CI / Reproducible runs
- In CI, run the same commands from the `ml/` directory. Ensure the CI image provides the correct Python version and any system libraries needed by the audio stack.

If you want, I can:
- add a small `README.md` in `ml/` with these instructions formatted differently, or
- run `pytest` here and paste failures if any occur (I will need to create/activate an environment first).
