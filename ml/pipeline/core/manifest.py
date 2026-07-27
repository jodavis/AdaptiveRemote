"""Generic typed collection of `Sample` objects, keyed by `name` and `content_hash`."""

from __future__ import annotations

from typing import Generic, Iterator, TypeVar

from pipeline.core.sample import Sample

S = TypeVar("S", bound=Sample)


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
