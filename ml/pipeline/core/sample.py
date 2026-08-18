"""Sample dataclass hierarchy for the ML pipeline's Manifest data model.

Every sample type flowing through the pipeline (intent phrases, synthesized audio, spectrogram
features, phoneme tokens) is represented as a small, self-describing dataclass. `content_hash`
is the real identity field -- used for skip-unchanged comparisons throughout the pipeline (see
`ml/_spec_OopPipeline.md`, "Content hash determines sample identity"). `name` only serves
manifest-uniqueness, on-disk filenames, and cross-manifest joins via `parent_name` -- see
"TextSample.name is content-addressable, not a random UUID" in the spec.
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass, field
from typing import Any


@dataclass
class Sample:
    """Base identity fields shared by every sample type in the pipeline."""

    name: str
    content_hash: str


@dataclass
class SampleWithPath(Sample):
    """A sample backed by a physical file, written by a `ModifierStage` (or subclass).

    `path` is root-relative to the pipeline's shared `data_root` directory (e.g.
    `speech_02_add_delays/TV_ON_Jenny_r77.wav`), not a bare filename -- see "Directories as
    stage I/O boundaries" in the spec. `parent_name`/`parent_content_hash`/`applied_values`
    record the upstream sample this one was derived from and the values applied to derive it;
    every `ModifierStage`/`RandomizedModifierStage` output uses these fields for skip-unchanged
    matching and content-hash computation (implemented by ADR-341, not this task).
    """

    path: str
    parent_name: str
    parent_content_hash: str
    applied_values: dict[str, Any]


@dataclass
class TextSample(Sample):
    """The root sample type: an intent phrase's full surface-form text.

    `name` and `content_hash` are excluded from `__init__` and computed in `__post_init__`
    instead, making `TextSample` identity content-addressable rather than randomly assigned --
    see "TextSample.name is content-addressable, not a random UUID" in the spec. Has no
    `parent_name`/`parent_content_hash`/`applied_values`/`seed`: it is the root of the
    pipeline, produced from the input phrase CSV rather than derived from an upstream sample.
    """

    content: str
    label: str
    name: str = field(init=False, default="")
    content_hash: str = field(init=False, default="")

    def __post_init__(self) -> None:
        self.content_hash = hashlib.sha256(self.content.encode("utf-8")).hexdigest()
        self.name = self.content_hash


@dataclass
class AudioSample(SampleWithPath):
    """A synthesized or augmented audio sample, backed by a `.wav` file.

    `transcript` is set to the full spoken surface form (`TextSample.content`) by
    `TtsSampleGenerator`, not the canonical `label` -- see "AudioSample.transcript is the full
    spoken surface form" in the spec (implemented by ADR-346, not this task). `seed` is only
    populated by `RandomizedModifierStage` subclasses (`TtsSampleGenerator`, `DelayAugmentor`,
    `BackgroundNoiseAugmentor`, `MicrophoneNoiseAugmentor`) -- every `AudioSample` currently
    flows through one of those, so the field is required here rather than optional.
    """

    transcript: str
    label: str
    seed: int


@dataclass
class SampleSpectrogram(SampleWithPath):
    """A log-mel spectrogram feature, backed by a per-sample file.

    Produced by the deterministic `SpectrogramStage` (a base `ModifierStage`, not a
    `RandomizedModifierStage`) -- carries no `seed` field at all, per "ModifierStage for all
    per-sample file transformations" in the spec.
    """


@dataclass
class SampleTokens(SampleWithPath):
    """A phoneme-token sequence, backed by a per-sample file.

    Produced by the deterministic `TokenStage` (a base `ModifierStage`, not a
    `RandomizedModifierStage`) -- carries no `seed` field at all, per "ModifierStage for all
    per-sample file transformations" in the spec.
    """
