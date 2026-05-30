from __future__ import annotations

from abc import ABC
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass
class Sample(ABC):
    """Base class for all pipeline sample types.

    id: stable identifier.
      TextSample: equals content_hash — content-addressable; no file, not user-visible.
      SampleWithPath subtypes: derived by each stage's _derive_id(); used as filename stem.
    seed: 0 for TextSample and deterministic stages; os.urandom(8) for stochastic stages.
    content_hash: sha256 hex digest.
      TextSample: sha256(content.encode('utf-8')).
      SampleWithPath subtypes: sha256(parent_content_hash + ":" + str(seed) + ":" + canonical(applied_values)).
    """

    id: str
    seed: int
    content_hash: str


@dataclass
class SampleWithPath(Sample, ABC):
    """Base for all ModifierStage output types that produce a file.

    path: relative filename (bare name only, no directory component).
    parent_content_hash: content_hash of the upstream sample; skip-unchanged lookup key.
    """

    path: Path
    parent_content_hash: str


@dataclass
class TextSample(Sample):
    """Bootstrapped phrase variant; no output file.

    id is derived from content_hash (content-addressable; callers do not pass it).
    seed=0 for all TextSamples.
    content_hash = sha256(content.encode('utf-8')).
    """

    content: str
    label: str
    id: str = field(init=False)

    def __post_init__(self) -> None:
        self.id = self.content_hash


@dataclass
class AudioSample(SampleWithPath):
    """WAV file produced by TtsSampleGenerator or an augmentation stage."""

    transcript: str
    applied_values: dict[str, Any]


@dataclass
class SampleSpectrogram(SampleWithPath):
    """NPY file produced by SpectrogramStage (seed=0, deterministic)."""

    transcript: str
    parent_id: str


@dataclass
class SampleTokens(SampleWithPath):
    """JSON file produced by TokenStage (seed=0, deterministic)."""

    transcript: str
    parent_id: str
