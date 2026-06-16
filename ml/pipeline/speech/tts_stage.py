from __future__ import annotations

import asyncio
import io
from pathlib import Path
from typing import Any, Protocol

import soundfile as sf

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import MinMaxFilter, VariationGenerator
from pipeline.core.sample import AudioSample, TextSample
from pipeline.stages import conventions


class TtsProvider(Protocol):
    async def synthesize(
        self, text: str, voice: str, rate: str, output_path: Path
    ) -> None: ...


class TtsSampleGenerator(ModifierStage[TextSample, AudioSample]):
    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        tts_provider: TtsProvider,
        voices: list[str],
        speech_rate_min: int,
        speech_rate_max: int,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._tts_provider = tts_provider
        self._voices = voices
        self._speech_rate_filter = MinMaxFilter(
            float(speech_rate_min), float(speech_rate_max)
        )

    def _get_applied_values(
        self, sample: TextSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        voice = generator.choose("voice", self._voices)
        speech_rate = generator.generate_int("speech_rate", self._speech_rate_filter)
        return {"voice": voice, "speech_rate": speech_rate}

    def _derive_id(self, input_sample: TextSample, applied_values: dict[str, Any]) -> str:
        voice: str = applied_values["voice"]
        speech_rate: int = applied_values["speech_rate"]
        voice_short = voice.split("-")[-1].replace("Neural", "")
        return f"{input_sample.label}_{voice_short}_r{speech_rate + 100}"

    async def _generate_output(
        self,
        input_sample: TextSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> AudioSample:
        voice: str = applied_values["voice"]
        speech_rate: int = applied_values["speech_rate"]
        rate_str = f"{speech_rate:+d}%"

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "wav")

        await self._tts_provider.synthesize(
            text=input_sample.content,
            voice=voice,
            rate=rate_str,
            output_path=output_path,
        )

        content_hash = self._compute_content_hash(
            parent_content_hash, output_seed, applied_values
        )

        return AudioSample(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.wav"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.label,
            applied_values=applied_values,
        )


class EdgeTtsProvider:

    _MAX_RETRIES: int = 3
    _INITIAL_DELAY_S: float = 1.0
    _BACKOFF_FACTOR: float = 2.0

    async def synthesize(
        self, text: str, voice: str, rate: str, output_path: Path
    ) -> None:
        import edge_tts  # deferred so unit tests don't require a live network

        delay = self._INITIAL_DELAY_S
        last_exc: Exception | None = None

        for attempt in range(self._MAX_RETRIES):
            try:
                communicate = edge_tts.Communicate(text=text, voice=voice, rate=rate)
                mp3_buf = io.BytesIO()
                async for chunk in communicate.stream():
                    if chunk["type"] == "audio":
                        mp3_buf.write(chunk["data"])

                mp3_buf.seek(0)
                data, sample_rate = sf.read(mp3_buf)
                if data.ndim > 1:
                    data = data.mean(axis=1)
                sf.write(
                    str(output_path),
                    data,
                    sample_rate,
                    format="WAV",
                    subtype="PCM_16",
                )
                return

            except Exception as exc:
                last_exc = exc
                if attempt < self._MAX_RETRIES - 1:
                    await asyncio.sleep(delay)
                    delay *= self._BACKOFF_FACTOR

        raise RuntimeError(
            f"TTS synthesis failed after {self._MAX_RETRIES} attempts"
        ) from last_exc
