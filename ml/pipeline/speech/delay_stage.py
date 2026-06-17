"""DelayAugmentor: prepend/append silence to WAV audio samples.

Both prefix_delay_s and suffix_delay_s are always stored in applied_values for hash
stability, even when not applied (stored as 0.0). The two should_vary checks are
independent — both, one, or neither delay may be applied on any given sample.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

import numpy as np

from pipeline.core.manifest import ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import MinMaxFilter, VariationGenerator
from pipeline.core.sample import AudioSample
from pipeline.io.audio_io import AudioReader, AudioWriter
from pipeline.stages import conventions


class DelayAugmentor(ModifierStage[AudioSample, AudioSample]):
    """Augmentation stage that adds leading and/or trailing silence to WAV samples.

    prefix_delay_s and suffix_delay_s are drawn independently based on their
    respective vary_probability values. A value of 0.0 is stored when the delay
    is not applied so that the content hash remains stable across configuration
    changes.

    input_dir is the directory containing the input WAV files referenced by
    AudioSample.path (bare filename). This is typically the output directory of
    the preceding stage (e.g. speech_01_generate_samples output).
    """

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        audio_reader: AudioReader,
        audio_writer: AudioWriter,
        input_dir: Path,
        prefix_vary_probability: float,
        prefix_min_s: float,
        prefix_max_s: float,
        suffix_vary_probability: float,
        suffix_min_s: float,
        suffix_max_s: float,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        self._audio_reader = audio_reader
        self._audio_writer = audio_writer
        self._input_dir = input_dir
        self._prefix_vary_probability = prefix_vary_probability
        self._prefix_filter = MinMaxFilter(prefix_min_s, prefix_max_s, precision=6)
        self._suffix_vary_probability = suffix_vary_probability
        self._suffix_filter = MinMaxFilter(suffix_min_s, suffix_max_s, precision=6)

    def _get_applied_values(
        self, sample: AudioSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        if generator.should_vary("prefix_delay_s", self._prefix_vary_probability):
            prefix_delay_s = generator.generate("prefix_delay_s", self._prefix_filter)
        else:
            prefix_delay_s = 0.0

        if generator.should_vary("suffix_delay_s", self._suffix_vary_probability):
            suffix_delay_s = generator.generate("suffix_delay_s", self._suffix_filter)
        else:
            suffix_delay_s = 0.0

        return {
            "prefix_delay_s": float(prefix_delay_s),
            "suffix_delay_s": float(suffix_delay_s),
        }

    def _derive_id(self, input_sample: AudioSample, applied_values: dict[str, Any]) -> str:
        prefix_delay_s: float = applied_values["prefix_delay_s"]
        suffix_delay_s: float = applied_values["suffix_delay_s"]
        pre_ms = int(prefix_delay_s * 1000)
        suf_ms = int(suffix_delay_s * 1000)
        return f"{input_sample.id}_pre{pre_ms}_suf{suf_ms}"

    async def _generate_output(
        self,
        input_sample: AudioSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> AudioSample:
        prefix_delay_s: float = applied_values["prefix_delay_s"]
        suffix_delay_s: float = applied_values["suffix_delay_s"]

        input_path = self._input_dir / input_sample.path
        audio_data, sample_rate = await self._audio_reader.read(input_path)

        n_prefix = int(prefix_delay_s * sample_rate)
        n_suffix = int(suffix_delay_s * sample_rate)

        parts: list[np.ndarray] = []
        if n_prefix > 0:
            parts.append(np.zeros(n_prefix, dtype=np.float32))
        parts.append(audio_data)
        if n_suffix > 0:
            parts.append(np.zeros(n_suffix, dtype=np.float32))

        output_audio = np.concatenate(parts) if len(parts) > 1 else audio_data

        self._output_dir.mkdir(parents=True, exist_ok=True)
        output_path = conventions.sample_file_path(self._output_dir, output_id, "wav")

        await self._audio_writer.write(output_path, output_audio, sample_rate)

        content_hash = self._compute_content_hash(
            parent_content_hash, output_seed, applied_values
        )

        return AudioSample(
            id=output_id,
            seed=output_seed,
            content_hash=content_hash,
            path=Path(f"{output_id}.wav"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.transcript,
            applied_values=applied_values,
        )
