from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Generic, Sequence, TypeVar

from pipeline.core.sample import (
    AudioSample,
    Sample,
    SampleSpectrogram,
    SampleTokens,
    TextSample,
)

S = TypeVar("S", bound=Sample)


class Manifest(Generic[S]):
    """Typed, immutable collection of samples with O(1) lookup by id or content_hash."""

    def __init__(self, samples: Sequence[S]) -> None:
        self._samples: tuple[S, ...] = tuple(samples)
        # Keep first occurrence for content_hash — duplicate hashes are possible
        # (deterministic stage, same parent + seed=0 + empty applied_values).
        self._by_content_hash: dict[str, S] = {}
        for s in self._samples:
            self._by_content_hash.setdefault(s.content_hash, s)
        self._by_id: dict[str, S] = {s.id: s for s in self._samples}
        if len(self._by_id) != len(self._samples):
            counts: dict[str, int] = {}
            for s in self._samples:
                counts[s.id] = counts.get(s.id, 0) + 1
            dupes = [sid for sid, n in counts.items() if n > 1]
            raise ValueError(f"Manifest contains duplicate sample ids: {dupes}")

    @property
    def samples(self) -> tuple[S, ...]:
        return self._samples

    def by_content_hash(self, h: str) -> S | None:
        return self._by_content_hash.get(h)

    def by_id(self, id: str) -> S | None:
        return self._by_id.get(id)


_SAMPLE_TYPE_KEY: dict[type, str] = {
    TextSample: "text",
    AudioSample: "audio",
    SampleSpectrogram: "spectrogram",
    SampleTokens: "tokens",
}

_SAMPLE_TYPE_CLASS: dict[str, type] = {v: k for k, v in _SAMPLE_TYPE_KEY.items()}


class ManifestStore:
    """Reads and writes Manifest JSON files (schema version 1).

    JSON format:
      {"version": 1, "sample_type": "<type>", "samples": [...]}

    Path fields in JSON are bare filenames (no directory component).
    Callers prepend output_dir when resolving full paths.
    Numeric applied_values are stored as raw int/float — never as strings.
    """

    def read(self, path: Path) -> Manifest[Any]:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        if data.get("version") != 1:
            raise ValueError(f"Unsupported manifest version: {data.get('version')!r}")
        sample_type = data.get("sample_type")
        if sample_type is None:
            raise ValueError("Missing 'sample_type' in manifest")
        cls = _SAMPLE_TYPE_CLASS.get(sample_type)
        if cls is None:
            raise ValueError(f"Unknown sample_type: {sample_type!r}")
        samples = [_deserialise(cls, entry) for entry in data["samples"]]
        return Manifest(samples)

    def write(self, manifest: Manifest[Any], path: Path) -> None:
        samples = manifest.samples
        if not samples:
            raise ValueError("Cannot write empty manifest")
        first_type = type(samples[0])
        if any(type(s) is not first_type for s in samples):
            raise ValueError(
                f"Manifest contains mixed sample types: "
                f"{set(type(s).__name__ for s in samples)}"
            )
        sample_type = _SAMPLE_TYPE_KEY[first_type]
        payload: dict[str, Any] = {
            "version": 1,
            "sample_type": sample_type,
            "samples": [_serialise(s) for s in samples],
        }
        with open(path, "w", encoding="utf-8") as f:
            json.dump(payload, f)


# ---------------------------------------------------------------------------
# Serialisation helpers
# ---------------------------------------------------------------------------

def _serialise(s: Sample) -> dict[str, Any]:
    if isinstance(s, TextSample):
        return {
            "id": s.id,
            "seed": s.seed,
            "content_hash": s.content_hash,
            "content": s.content,
            "label": s.label,
        }
    if isinstance(s, AudioSample):
        return {
            "id": s.id,
            "seed": s.seed,
            "content_hash": s.content_hash,
            "path": s.path.name,
            "parent_content_hash": s.parent_content_hash,
            "transcript": s.transcript,
            "applied_values": s.applied_values,
        }
    if isinstance(s, SampleSpectrogram):
        return {
            "id": s.id,
            "seed": s.seed,
            "content_hash": s.content_hash,
            "path": s.path.name,
            "parent_content_hash": s.parent_content_hash,
            "transcript": s.transcript,
            "parent_id": s.parent_id,
        }
    if isinstance(s, SampleTokens):
        return {
            "id": s.id,
            "seed": s.seed,
            "content_hash": s.content_hash,
            "path": s.path.name,
            "parent_content_hash": s.parent_content_hash,
            "transcript": s.transcript,
            "parent_id": s.parent_id,
        }
    raise ValueError(f"Unrecognised sample type: {type(s)}")


def _deserialise(cls: type, e: dict[str, Any]) -> Sample:
    if cls is TextSample:
        return TextSample(
            seed=e["seed"],
            content_hash=e["content_hash"],
            content=e["content"],
            label=e["label"],
        )
    if cls is AudioSample:
        return AudioSample(
            id=e["id"],
            seed=e["seed"],
            content_hash=e["content_hash"],
            path=Path(e["path"]),
            parent_content_hash=e["parent_content_hash"],
            transcript=e["transcript"],
            applied_values=e["applied_values"],
        )
    if cls is SampleSpectrogram:
        return SampleSpectrogram(
            id=e["id"],
            seed=e["seed"],
            content_hash=e["content_hash"],
            path=Path(e["path"]),
            parent_content_hash=e["parent_content_hash"],
            transcript=e["transcript"],
            parent_id=e["parent_id"],
        )
    if cls is SampleTokens:
        return SampleTokens(
            id=e["id"],
            seed=e["seed"],
            content_hash=e["content_hash"],
            path=Path(e["path"]),
            parent_content_hash=e["parent_content_hash"],
            transcript=e["transcript"],
            parent_id=e["parent_id"],
        )
    raise ValueError(f"Unrecognised class: {cls}")
