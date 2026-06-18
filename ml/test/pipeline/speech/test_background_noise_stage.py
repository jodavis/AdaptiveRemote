"""Unit tests for BackgroundNoiseAugmentor."""

from __future__ import annotations

import asyncio
import hashlib
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

sys.path.insert(0, str(Path(__file__).parent.parent.parent.parent))

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample
from pipeline.io.audio_io import AudioData
from pipeline.speech.background_noise_stage import BackgroundNoiseAugmentor, NoiseProvider
from pipeline.stages.params import AddBackgroundNoiseParams


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str = "TV_ON_Jenny_r100",
    transcript: str = "TV_ON",
    sample_rate: int = 16000,
    duration_s: float = 0.5,
    wav_dir: Path | None = None,
) -> AudioSample:
    content = f"{sample_id}:audio"
    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
    path = Path(f"{sample_id}.wav")
    if wav_dir is not None:
        wav_path = wav_dir / path
        n_samples = int(sample_rate * duration_s)
        data = np.zeros(n_samples, dtype=np.float32)
        sf.write(str(wav_path), data, sample_rate, format="WAV", subtype="PCM_16")
    return AudioSample(
        id=sample_id,
        seed=0,
        content_hash=content_hash,
        path=path,
        parent_content_hash="parent_hash",
        transcript=transcript,
        applied_values={},
    )


class _RecordingAudioReader:
    """Stub AudioReader that records read() calls and returns silence."""

    def __init__(
        self,
        sample_rate: int = 16000,
        audio_duration_s: float = 0.5,
    ) -> None:
        self.calls: list[Path] = []
        self._sample_rate = sample_rate
        self._audio_duration_s = audio_duration_s

    async def read(self, path: Path) -> AudioData:
        self.calls.append(path)
        n_samples = int(self._sample_rate * self._audio_duration_s)
        samples = np.zeros(n_samples, dtype=np.float32)
        return AudioData(samples=samples, sample_rate=self._sample_rate)


class _RecordingAudioWriter:
    """Stub AudioWriter that records write() calls."""

    def __init__(self) -> None:
        self.calls: list[tuple[Path, AudioData]] = []

    async def write(self, path: Path, audio: AudioData) -> None:
        self.calls.append((path, audio))
        sf.write(str(path), audio.samples, audio.sample_rate, format="WAV", subtype="PCM_16")


class _FakeNoiseProvider:
    """Fake NoiseProvider that returns a fixed list of (name, AudioData) tuples."""

    def __init__(
        self,
        filenames: list[str],
        sample_rate: int = 16000,
        duration_s: float = 2.0,
    ) -> None:
        self._items = [
            (
                name,
                AudioData(
                    samples=(np.random.default_rng(i).random(int(sample_rate * duration_s)) * 2 - 1).astype(np.float32),
                    sample_rate=sample_rate,
                ),
            )
            for i, name in enumerate(filenames)
        ]

    def list_files(self) -> list[tuple[str, AudioData]]:
        return list(self._items)


def _make_params(
    vary_probability: float = 0.0,
    volume_min: float = 0.0,
    volume_max: float = 0.3,
) -> AddBackgroundNoiseParams:
    return AddBackgroundNoiseParams(
        vary_probability=vary_probability,
        volume_min=volume_min,
        volume_max=volume_max,
    )


def _make_stage(
    output_dir: Path,
    *,
    audio_reader: _RecordingAudioReader | None = None,
    audio_writer: _RecordingAudioWriter | None = None,
    input_dir: Path | None = None,
    noise_provider: _FakeNoiseProvider | None = None,
    params: AddBackgroundNoiseParams | None = None,
    vary_probability: float = 0.0,
    volume_min: float = 0.0,
    volume_max: float = 0.3,
    noise_filenames: list[str] | None = None,
    sample_rate: int = 16000,
) -> tuple[BackgroundNoiseAugmentor, _RecordingAudioReader, _RecordingAudioWriter]:
    if audio_reader is None:
        audio_reader = _RecordingAudioReader(sample_rate=sample_rate)
    if audio_writer is None:
        audio_writer = _RecordingAudioWriter()
    if input_dir is None:
        input_dir = output_dir
    if params is None:
        params = _make_params(
            vary_probability=vary_probability,
            volume_min=volume_min,
            volume_max=volume_max,
        )
    if noise_provider is None:
        if noise_filenames is None:
            noise_filenames = ["traffic.wav"]
        noise_provider = _FakeNoiseProvider(noise_filenames, sample_rate=sample_rate)
    stage = BackgroundNoiseAugmentor(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        audio_reader=audio_reader,
        audio_writer=audio_writer,
        input_dir=input_dir,
        noise_provider=noise_provider,
        params=params,
    )
    return stage, audio_reader, audio_writer


def _write_audio_wav(dir: Path, filename: str, sample_rate: int = 16000, duration_s: float = 0.5) -> Path:
    dir.mkdir(parents=True, exist_ok=True)
    path = dir / filename
    n_samples = int(sample_rate * duration_s)
    data = np.zeros(n_samples, dtype=np.float32)
    sf.write(str(path), data, sample_rate, format="WAV", subtype="PCM_16")
    return path


# ---------------------------------------------------------------------------
# TestAppliedValues
# ---------------------------------------------------------------------------


class TestAppliedValues:
    def test_applied_values_has_noise_file_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert "noise_file" in av

    def test_applied_values_has_noise_start_s_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert "noise_start_s" in av

    def test_applied_values_has_noise_volume_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert "noise_volume" in av

    def test_applied_values_has_exactly_three_keys(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert set(av.keys()) == {"noise_file", "noise_start_s", "noise_volume"}

    def test_noise_file_always_chosen_when_not_applied(self, tmp_path: Path) -> None:
        """noise_file is always selected even when should_vary returns False (vary_probability=0)."""
        stage, _, _ = _make_stage(
            tmp_path,
            vary_probability=0.0,
            noise_filenames=["rain.wav", "traffic.wav"],
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        # noise_file must be one of the provided filenames
        assert av["noise_file"] in ["rain.wav", "traffic.wav"]

    def test_noise_file_stored_even_when_volume_is_zero(self, tmp_path: Path) -> None:
        """noise_file is stored regardless of whether noise is applied."""
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["noise_file"] == "traffic.wav"
        assert av["noise_volume"] == 0.0

    def test_noise_start_s_zero_when_not_applied(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["noise_start_s"] == 0.0

    def test_noise_volume_zero_when_not_applied(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["noise_volume"] == 0.0

    def test_noise_volume_nonzero_when_applied(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(
            tmp_path,
            vary_probability=1.0,
            volume_min=0.2,
            volume_max=0.2,
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["noise_volume"] > 0.0

    def test_noise_file_chosen_from_sorted_list(self, tmp_path: Path) -> None:
        """choose() operates on sorted filenames for OS-independent determinism."""
        filenames = ["z_noise.wav", "a_noise.wav", "m_noise.wav"]
        stage, _, _ = _make_stage(tmp_path, noise_filenames=filenames)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(0))
        # Must be one of the provided filenames
        assert av["noise_file"] in filenames

    def test_noise_start_s_stored_as_float(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["noise_start_s"], float)

    def test_noise_volume_stored_as_float(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["noise_volume"], float)


# ---------------------------------------------------------------------------
# TestNoiseStartS
# ---------------------------------------------------------------------------


class TestNoiseStartS:
    def test_noise_start_s_is_always_zero(self, tmp_path: Path) -> None:
        """noise_start_s is always 0.0 — noise is mixed from the start of the file."""
        stage, _, _ = _make_stage(
            tmp_path,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["noise_start_s"] == 0.0


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_format_with_noise_applied(self, tmp_path: Path) -> None:
        """Format: {input.id}_{noise_filestem}_v{int(volume*100)}"""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {"noise_file": "traffic.wav", "noise_start_s": 0.5, "noise_volume": 0.25})
        assert result == "TV_ON_Jenny_r100_traffic_v25"

    def test_derive_id_format_with_zero_volume(self, tmp_path: Path) -> None:
        """Even when volume=0 (not applied), the filestem is still in the id."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {"noise_file": "rain.wav", "noise_start_s": 0.0, "noise_volume": 0.0})
        assert result == "TV_ON_Jenny_r100_rain_v0"

    def test_derive_id_uses_stem_not_full_filename(self, tmp_path: Path) -> None:
        """noise_filestem = Path(noise_file).stem — extension excluded."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="X")
        result = stage._derive_id(sample, {"noise_file": "background_noise.wav", "noise_start_s": 0.0, "noise_volume": 0.1})
        assert "background_noise" in result
        assert ".wav" not in result

    def test_derive_id_volume_int_conversion(self, tmp_path: Path) -> None:
        """int(volume * 100): 0.3 → 30, 0.25 → 25."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="S")
        result = stage._derive_id(sample, {"noise_file": "t.wav", "noise_start_s": 0.0, "noise_volume": 0.3})
        assert result == "S_t_v30"

    def test_derive_id_uses_input_id_as_prefix(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="VOLUME_UP_Aria_r110")
        result = stage._derive_id(sample, {"noise_file": "cafe.wav", "noise_start_s": 0.0, "noise_volume": 0.0})
        assert result.startswith("VOLUME_UP_Aria_r110_")


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_generate_output_does_not_call_reader_when_volume_is_zero(self, tmp_path: Path) -> None:
        """When noise not applied (volume=0), no audio reading occurs."""
        reader = _RecordingAudioReader()
        stage, _, _ = _make_stage(
            tmp_path,
            audio_reader=reader,
            vary_probability=0.0,
        )
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 0

    def test_generate_output_does_not_call_writer_when_volume_is_zero(self, tmp_path: Path) -> None:
        """When noise not applied (volume=0), no file is written."""
        stage, _, writer = _make_stage(
            tmp_path,
            vary_probability=0.0,
        )
        sample = _make_audio_sample()
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(writer.calls) == 0

    def test_generate_output_returns_input_when_volume_is_zero(self, tmp_path: Path) -> None:
        """When noise not applied, transform returns the original input sample."""
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample(sample_id="MY_SAMPLE")
        result = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result.samples[0].id == sample.id
        assert result.samples[0].path == sample.path

    def test_generate_output_calls_audio_reader_for_input_and_does_not_call_for_noise(self, tmp_path: Path) -> None:
        """When noise is applied (vary_probability=1.0), reader is called for input only; noise comes from provider."""
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav")
        reader = _RecordingAudioReader()
        stage, _, _ = _make_stage(
            tmp_path,
            audio_reader=reader,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
            noise_filenames=["traffic.wav"],
        )
        sample = _make_audio_sample(wav_dir=tmp_path)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        # Only 1 call: the input audio file. Noise audio comes from the provider directly.
        assert len(reader.calls) == 1

    def test_generate_output_calls_audio_writer_when_noise_applied(self, tmp_path: Path) -> None:
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav")
        stage, _, writer = _make_stage(
            tmp_path,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
            noise_filenames=["traffic.wav"],
        )
        sample = _make_audio_sample(wav_dir=tmp_path)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(writer.calls) == 1

    def test_output_audio_same_length_as_input(self, tmp_path: Path) -> None:
        """Background noise does not change the length of the audio."""
        sample_rate = 16000
        duration_s = 0.5
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav", sample_rate=sample_rate, duration_s=duration_s)

        reader = _RecordingAudioReader(sample_rate=sample_rate, audio_duration_s=duration_s)
        stage, _, writer = _make_stage(
            tmp_path,
            audio_reader=reader,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
            noise_filenames=["traffic.wav"],
            sample_rate=sample_rate,
        )
        sample = _make_audio_sample(wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        _path, written_audio = writer.calls[0]
        assert len(written_audio.samples) == int(sample_rate * duration_s)

    def test_transcript_preserved_in_output_sample_when_noise_applied(self, tmp_path: Path) -> None:
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav")
        stage, _, _ = _make_stage(
            tmp_path,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
            noise_filenames=["traffic.wav"],
        )
        sample = _make_audio_sample(transcript="VOLUME_UP", wav_dir=tmp_path)
        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )
        assert result.samples[0].transcript == "VOLUME_UP"

    def test_transcript_preserved_in_output_sample_when_not_applied(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample(transcript="CHANNEL_DOWN")
        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )
        assert result.samples[0].transcript == "CHANNEL_DOWN"

    def test_sample_rate_mismatch_raises(self, tmp_path: Path) -> None:
        """Raises ValueError when noise audio has a different sample rate than input audio."""
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav", sample_rate=16000)
        # Provide noise at a different sample rate
        noise_provider = _FakeNoiseProvider(["traffic.wav"], sample_rate=8000)
        reader = _RecordingAudioReader(sample_rate=16000)  # Input is 16000
        writer = _RecordingAudioWriter()
        stage = BackgroundNoiseAugmentor(
            output_dir=tmp_path,
            manifest_store=ManifestStore(),
            audio_reader=reader,
            audio_writer=writer,
            input_dir=tmp_path,
            noise_provider=noise_provider,
            params=_make_params(vary_probability=1.0, volume_min=0.1, volume_max=0.1),
        )
        sample = _make_audio_sample(wav_dir=tmp_path, sample_rate=16000)
        import pytest
        with pytest.raises(ValueError, match="sample.rate"):
            asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------


class TestSkipPath:
    def test_skip_path_does_not_call_audio_writer_on_second_run(self, tmp_path: Path) -> None:
        """When noise IS applied, second run skips writing (output unchanged)."""
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav")
        stage, _, writer = _make_stage(
            tmp_path,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
            noise_filenames=["traffic.wav"],
        )
        sample = _make_audio_sample(wav_dir=tmp_path)

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        initial_count = len(writer.calls)
        assert initial_count == 1

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(writer.calls) == initial_count

    def test_skip_path_preserves_output_sample_id(self, tmp_path: Path) -> None:
        """When noise IS applied, second run returns the same output id."""
        _write_audio_wav(tmp_path, "TV_ON_Jenny_r100.wav")
        stage, _, _ = _make_stage(
            tmp_path,
            vary_probability=1.0,
            volume_min=0.1,
            volume_max=0.1,
            noise_filenames=["traffic.wav"],
        )
        sample = _make_audio_sample(wav_dir=tmp_path)

        result1 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        result2 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result2.samples[0].id == result1.samples[0].id
