"""Generic typed collection of `Sample` objects, keyed by `name` and `content_hash`."""

from __future__ import annotations

import dataclasses
import json
from pathlib import Path
from typing import Any, Generic, Iterator, TypeVar

from pipeline.core.sample import AudioSample, Sample, SampleSpectrogram, SampleTokens, TextSample

S = TypeVar("S", bound=Sample)

_SAMPLE_TYPES_BY_NAME: dict[str, type[Sample]] = {
    cls.__name__: cls for cls in (TextSample, AudioSample, SampleSpectrogram, SampleTokens)
}


class Manifest(Generic[S]):
    """A named/content-hash-indexed collection of one concrete `Sample` subtype.

    Enforces name uniqueness within the manifest — two samples sharing a
    `name` is a construction-time error, since `name` is what backs on-disk
    filenames and cross-manifest joins (`parent_name`).
    """

    def __init__(self, samples: list[S] | None = None) -> None:
        self._by_name: dict[str, S] = {}
        self._by_content_hash: dict[str, S] = {}
        for sample in samples or []:
            self.add(sample)

    def add(self, sample: S) -> None:
        if sample.name in self._by_name:
            raise ValueError(f"Duplicate sample name: {sample.name!r}")
        self._by_name[sample.name] = sample
        self._by_content_hash[sample.content_hash] = sample

    def by_name(self, name: str) -> S:
        return self._by_name[name]

    def by_content_hash(self, content_hash: str) -> S:
        return self._by_content_hash[content_hash]

    def __len__(self) -> int:
        return len(self._by_name)

    def __iter__(self) -> Iterator[S]:
        return iter(self._by_name.values())


class ManifestStore:
    """Reads and writes `Manifest[S]` instances as JSON (schema v1)."""

    def write(self, manifest: Manifest[S], path: Path) -> None:
        samples = list(manifest)
        if not samples:
            raise ValueError("Cannot write an empty manifest")
        sample_types = {type(sample) for sample in samples}
        if len(sample_types) > 1:
            raise ValueError(
                f"Cannot write a manifest with mixed sample types: {sorted(t.__name__ for t in sample_types)}"
            )
        sample_type_name = type(samples[0]).__name__
        payload: dict[str, Any] = {
            "schema_version": 1,
            "sample_type": sample_type_name,
            "samples": [dataclasses.asdict(sample) for sample in samples],
        }
        path.write_text(json.dumps(payload))

    def read(self, path: Path) -> Manifest[Sample]:
        payload = json.loads(path.read_text())
        sample_type = _SAMPLE_TYPES_BY_NAME[payload["sample_type"]]
        return Manifest[Sample]([sample_type(**data) for data in payload["samples"]])
