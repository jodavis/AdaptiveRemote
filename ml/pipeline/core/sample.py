"""Typed sample hierarchy carried through every pipeline manifest.

Every concrete `Sample` subtype is a plain dataclass (Wrapper-tier — see
`ml/_spec_OopPipeline.md`'s Component Breakdown). The one piece of real
behavior in this module is `TextSample.__post_init__`, which makes a
`TextSample`'s `name` content-addressable rather than a random identifier.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any


@dataclass
class Sample:
    """Base sample: identity/lineage fields shared by every concrete sample type.

    `name` serves manifest-uniqueness, on-disk filenames, and cross-manifest
    joins (`parent_name`); it is not the identity field. `content_hash` (and
    `parent_content_hash` matching against it) is what actually determines
    sample identity for skip/regenerate decisions made by later pipeline
    stages.
    """

    name: str = ""
    content_hash: str = ""
    applied_values: dict[str, Any] = field(default_factory=dict)
    parent_name: str | None = None
    parent_content_hash: str | None = None


@dataclass
class SampleWithPath(Sample):
    """A sample backed by a file, addressed relative to the pipeline's shared data root."""

    path: str = ""


@dataclass
class TextSample(Sample):
    """A generated phrase variant. No parent — `content_hash` is derived from `content` alone."""

    content: str = ""
    label: str = ""

    def __post_init__(self) -> None:
        # content_hash = sha256(content) (computed by the caller); name is set
        # equal to it so two TextSamples with identical surface-form text
        # always share the same name, keeping PhraseVariator output stable
        # across reruns instead of assigning a fresh random identifier.
        self.name = self.content_hash


@dataclass
class AudioSample(SampleWithPath):
    """Synthesized/augmented speech audio. Only samples produced by a
    `RandomizedModifierStage` subclass carry a `seed`.
    """

    transcript: str = ""
    label: str = ""
    seed: int = 0


@dataclass
class SampleSpectrogram(SampleWithPath):
    """A log-mel spectrogram derived deterministically from an `AudioSample`. No `seed` field."""


@dataclass
class SampleTokens(SampleWithPath):
    """A phoneme-token sequence derived deterministically from an `AudioSample`. No `seed` field."""
