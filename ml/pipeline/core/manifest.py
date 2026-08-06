"""`Manifest[S]`/`ManifestStore`: a typed, in-memory sample collection plus its JSON
round-trip persistence.

Replaces the pre-refactor dual-CSV format (a variation CSV plus set-manifest CSVs) with a
single generic collection indexed for the two lookups every pipeline stage needs -- by `name`
(manifest-uniqueness, on-disk filenames, cross-manifest joins) and by `content_hash` (the real
identity field, used for skip-unchanged comparisons). See "Unified end-to-end Manifest format"
in `ml/_spec_OopPipeline.md`.
"""

from __future__ import annotations

import json
from dataclasses import asdict, fields
from pathlib import Path
from typing import Any, Generic, Iterable, Iterator, TypeVar

from pipeline.core.sample import AudioSample, Sample, SampleSpectrogram, SampleTokens, TextSample

S = TypeVar("S", bound=Sample)

SCHEMA_VERSION = 1

# Every concrete sample type a Manifest can carry, per the spec's "carries TextSample,
# AudioSample, SampleSpectrogram, or SampleTokens objects" -- keyed by class name so a
# written manifest file self-describes which type to reconstruct on read, without the
# caller needing to pass one in.
_SAMPLE_TYPES: dict[str, type[Sample]] = {
    "TextSample": TextSample,
    "AudioSample": AudioSample,
    "SampleSpectrogram": SampleSpectrogram,
    "SampleTokens": SampleTokens,
}


class Manifest(Generic[S]):
    """An in-memory collection of `Sample` (or subtype) objects.

    Construction eagerly indexes every sample by `name` and `content_hash`. `Manifest` itself
    has no knowledge of JSON serialisation or DVC split files -- that's `ManifestStore`'s job.
    """

    def __init__(self, samples: Iterable[S]) -> None:
        self._samples: list[S] = list(samples)
        self._by_name: dict[str, S] = {}
        self._by_content_hash: dict[str, S] = {}
        for sample in self._samples:
            if sample.name in self._by_name:
                raise ValueError(f"Duplicate sample name: {sample.name!r}")
            self._by_name[sample.name] = sample
            self._by_content_hash[sample.content_hash] = sample

    @property
    def samples(self) -> list[S]:
        return list(self._samples)

    def by_name(self, name: str) -> S:
        return self._by_name[name]

    def by_content_hash(self, content_hash: str) -> S:
        return self._by_content_hash[content_hash]

    def __iter__(self) -> Iterator[S]:
        return iter(self._samples)

    def __len__(self) -> int:
        return len(self._samples)


class ManifestStore:
    """JSON round-trip persistence for `Manifest[S]`, schema version 1.

    `write` raises `ValueError` on an empty manifest (no sample to derive a `sample_type`
    from) or on a manifest mixing more than one concrete sample type -- both load-bearing
    correctness checks, not incidental validation, per the spec. `read` reconstructs the
    concrete dataclass recorded in the file's own `sample_type` field, so callers don't need
    to pass an expected type.
    """

    def write(self, manifest: Manifest[S], path: Path) -> None:
        samples = manifest.samples
        if not samples:
            raise ValueError("Cannot write an empty manifest")

        sample_type_name = type(samples[0]).__name__
        for sample in samples:
            if type(sample).__name__ != sample_type_name:
                raise ValueError(
                    "Manifest contains mixed sample types: "
                    f"{sample_type_name!r} and {type(sample).__name__!r}"
                )

        document = {
            "schema_version": SCHEMA_VERSION,
            "sample_type": sample_type_name,
            "samples": [asdict(sample) for sample in samples],
        }
        path.write_text(json.dumps(document, indent=2), encoding="utf-8")

    def read(self, path: Path) -> Manifest[Any]:
        document = json.loads(path.read_text(encoding="utf-8"))
        sample_type = _SAMPLE_TYPES[document["sample_type"]]
        init_field_names = {f.name for f in fields(sample_type) if f.init}
        samples = [
            sample_type(**{k: v for k, v in raw.items() if k in init_field_names})
            for raw in document["samples"]
        ]
        return Manifest(samples)
