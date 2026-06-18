"""Unit tests for MicrophoneNoiseAugmentor."""

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
from pipeline.speech.mic_noise_stage import MicrophoneNoiseAugmentor
from pipeline.stages.params import AddMicNoiseParams


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_audio_sample(
    sample_id: str = "TV_ON_Jenny_r100",
    transcript: str = "TV_ON",
    sample_rate: int = 16000,
    duration_s: float = 0.1,
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

    def __init__(self, sample_rate: int = 16000, duration_s: float = 0.1) -> None:
        self.calls: list[Path] = []
        self._sample_rate = sample_rate
        self._duration_s = duration_s

    async def read(self, path: Path) -> AudioData:
        self.calls.append(path)
        n_samples = int(self._sample_rate * self._duration_s)
        samples = np.zeros(n_samples, dtype=np.float32)
        return AudioData(samples=samples, sample_rate=self._sample_rate)


class _RecordingAudioWriter:
    """Stub AudioWriter that records write() calls."""

    def __init__(self) -> None:
        self.calls: list[tuple[Path, AudioData]] = []

    async def write(self, path: Path, audio: AudioData) -> None:
        self.calls.append((path, audio))
        sf.write(str(path), audio.samples, audio.sample_rate, format="WAV", subtype="PCM_16")


def _make_params(
    vary_probability: float = 0.0,
    amplitude_min: float = 0.001,
    amplitude_max: float = 0.05,
) -> AddMicNoiseParams:
    return AddMicNoiseParams(
        vary_probability=vary_probability,
        amplitude_min=amplitude_min,
        amplitude_max=amplitude_max,
    )


def _make_stage(
    output_dir: Path,
    *,
    audio_reader: _RecordingAudioReader | None = None,
    audio_writer: _RecordingAudioWriter | None = None,
    input_dir: Path | None = None,
    params: AddMicNoiseParams | None = None,
    vary_probability: float = 0.0,
    amplitude_min: float = 0.001,
    amplitude_max: float = 0.05,
) -> tuple[MicrophoneNoiseAugmentor, _RecordingAudioReader, _RecordingAudioWriter]:
    if audio_reader is None:
        audio_reader = _RecordingAudioReader()
    if audio_writer is None:
        audio_writer = _RecordingAudioWriter()
    if input_dir is None:
        input_dir = output_dir
    if params is None:
        params = _make_params(
            vary_probability=vary_probability,
            amplitude_min=amplitude_min,
            amplitude_max=amplitude_max,
        )
    stage = MicrophoneNoiseAugmentor(
        output_dir=output_dir,
        manifest_store=ManifestStore(),
        audio_reader=audio_reader,
        audio_writer=audio_writer,
        input_dir=input_dir,
        params=params,
    )
    return stage, audio_reader, audio_writer


# ---------------------------------------------------------------------------
# TestAppliedValues
# ---------------------------------------------------------------------------


class TestAppliedValues:
    def test_applied_values_has_mic_noise_amplitude_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert "mic_noise_amplitude" in av

    def test_applied_values_has_exactly_one_key(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert set(av.keys()) == {"mic_noise_amplitude"}

    def test_amplitude_zero_when_not_applied(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0, amplitude_min=0.01, amplitude_max=0.05)
        sample = _make_audio_sample()
        for seed in range(10):
            av = stage._get_applied_values(sample, VariationGenerator(seed))
            assert av["mic_noise_amplitude"] == 0.0

    def test_amplitude_nonzero_when_applied(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(
            tmp_path, vary_probability=1.0, amplitude_min=0.01, amplitude_max=0.01
        )
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["mic_noise_amplitude"] > 0.0

    def test_amplitude_stored_as_float(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert isinstance(av["mic_noise_amplitude"], float)

    def test_amplitude_zero_stored_as_float_not_int(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path, vary_probability=0.0)
        sample = _make_audio_sample()
        av = stage._get_applied_values(sample, VariationGenerator(42))
        assert av["mic_noise_amplitude"] == 0.0
        assert isinstance(av["mic_noise_amplitude"], float)


# ---------------------------------------------------------------------------
# TestDeriveId
# ---------------------------------------------------------------------------


class TestDeriveId:
    def test_derive_id_format_with_noise_applied(self, tmp_path: Path) -> None:
        """Format: {input.id}_mic{int(amplitude*1000)}"""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {"mic_noise_amplitude": 0.025})
        assert result == "TV_ON_Jenny_r100_mic25"

    def test_derive_id_format_with_zero_amplitude(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="TV_ON_Jenny_r100")
        result = stage._derive_id(sample, {"mic_noise_amplitude": 0.0})
        assert result == "TV_ON_Jenny_r100_mic0"

    def test_derive_id_uses_input_id_as_prefix(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="VOLUME_UP_Aria_r110")
        result = stage._derive_id(sample, {"mic_noise_amplitude": 0.0})
        assert result.startswith("VOLUME_UP_Aria_r110_")

    def test_derive_id_amplitude_int_conversion(self, tmp_path: Path) -> None:
        """int(amplitude * 1000): 0.05 → 50, 0.001 → 1."""
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="S")
        result = stage._derive_id(sample, {"mic_noise_amplitude": 0.05})
        assert result == "S_mic50"

    def test_derive_id_always_has_mic_suffix(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(sample_id="X")
        result = stage._derive_id(sample, {"mic_noise_amplitude": 0.01})
        assert "_mic" in result


# ---------------------------------------------------------------------------
# TestGenerateOutput
# ---------------------------------------------------------------------------


class TestGenerateOutput:
    def test_generate_output_calls_audio_reader(self, tmp_path: Path) -> None:
        stage, reader, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(reader.calls) == 1

    def test_generate_output_calls_audio_writer(self, tmp_path: Path) -> None:
        stage, _, writer = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(writer.calls) == 1

    def test_output_audio_same_length_as_input(self, tmp_path: Path) -> None:
        """Mic noise does not change the length of the audio."""
        sample_rate = 16000
        duration_s = 0.1
        stage, _, writer = _make_stage(
            tmp_path,
            audio_reader=_RecordingAudioReader(sample_rate=sample_rate, duration_s=duration_s),
            vary_probability=1.0,
            amplitude_min=0.01,
            amplitude_max=0.01,
        )
        sample = _make_audio_sample(wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        _path, written_audio = writer.calls[0]
        assert len(written_audio.samples) == int(sample_rate * duration_s)

    def test_output_audio_unchanged_when_amplitude_is_zero(self, tmp_path: Path) -> None:
        """When noise not applied (amplitude=0), output samples equal input samples."""
        sample_rate = 16000
        duration_s = 0.1

        class _ConstantReader:
            async def read(self, path: Path) -> AudioData:
                n = int(sample_rate * duration_s)
                return AudioData(samples=np.full(n, 0.5, dtype=np.float32), sample_rate=sample_rate)

        writer = _RecordingAudioWriter()
        stage = MicrophoneNoiseAugmentor(
            output_dir=tmp_path,
            manifest_store=ManifestStore(),
            audio_reader=_ConstantReader(),
            audio_writer=writer,
            input_dir=tmp_path,
            params=_make_params(vary_probability=0.0),
        )
        sample = _make_audio_sample(wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        _path, written_audio = writer.calls[0]
        assert np.allclose(written_audio.samples, 0.5)

    def test_gaussian_noise_added_when_amplitude_nonzero(self, tmp_path: Path) -> None:
        """When amplitude > 0, output should differ from input (noise was added)."""
        sample_rate = 16000
        duration_s = 0.5  # Long enough for statistical significance
        amplitude = 0.1

        class _ZeroReader:
            async def read(self, path: Path) -> AudioData:
                n = int(sample_rate * duration_s)
                return AudioData(samples=np.zeros(n, dtype=np.float32), sample_rate=sample_rate)

        writer = _RecordingAudioWriter()
        stage = MicrophoneNoiseAugmentor(
            output_dir=tmp_path,
            manifest_store=ManifestStore(),
            audio_reader=_ZeroReader(),
            audio_writer=writer,
            input_dir=tmp_path,
            params=_make_params(vary_probability=1.0, amplitude_min=amplitude, amplitude_max=amplitude),
        )
        sample = _make_audio_sample(wav_dir=tmp_path, sample_rate=sample_rate, duration_s=duration_s)
        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        _path, written_audio = writer.calls[0]
        # Noise was added — not all zeros anymore
        assert not np.all(written_audio.samples == 0.0)

    def test_transcript_preserved_in_output_sample(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(transcript="CHANNEL_UP", wav_dir=tmp_path)
        result = asyncio.run(
            stage.transform(Manifest([sample]), tmp_path / "manifest.json")
        )
        assert result.samples[0].transcript == "CHANNEL_UP"


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------


class TestSkipPath:
    def test_skip_path_does_not_call_audio_writer_on_second_run(self, tmp_path: Path) -> None:
        stage, _, writer = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        initial_count = len(writer.calls)
        assert initial_count == 1

        asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert len(writer.calls) == initial_count

    def test_skip_path_preserves_output_sample_id(self, tmp_path: Path) -> None:
        stage, _, _ = _make_stage(tmp_path)
        sample = _make_audio_sample(wav_dir=tmp_path)

        result1 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        result2 = asyncio.run(stage.transform(Manifest([sample]), tmp_path / "manifest.json"))
        assert result2.samples[0].id == result1.samples[0].id
