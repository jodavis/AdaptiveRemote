"""Unit tests for the `Sample` dataclass hierarchy.

Per `ml/_spec_OopPipeline.md`'s Component Breakdown, the `Sample` family is Wrapper-tier
(plain dataclasses; visual inspection normally suffices), but `TextSample.__post_init__` is
genuine computed behavior -- not a pass-through -- so it gets direct test coverage even though
the surrounding class does not.
"""

from __future__ import annotations

import hashlib

from pipeline.core.sample import TextSample


class TestTextSample:
    def test_TextSample_PostInit_SetsContentHashToSha256OfContent(self) -> None:
        sample = TextSample(content="turn on the tv", label="TV_ON")

        expected_hash = hashlib.sha256("turn on the tv".encode("utf-8")).hexdigest()
        assert sample.content_hash == expected_hash

    def test_TextSample_PostInit_SetsNameToContentHash(self) -> None:
        sample = TextSample(content="turn on the tv", label="TV_ON")

        assert sample.name == sample.content_hash

    def test_TextSample_PostInit_DifferentContent_ProducesDifferentNameAndHash(self) -> None:
        first = TextSample(content="turn on the tv", label="TV_ON")
        second = TextSample(content="turn off the tv", label="TV_OFF")

        assert first.content_hash != second.content_hash
        assert first.name != second.name

    def test_TextSample_PostInit_IdenticalContent_ProducesSameNameAndHash(self) -> None:
        first = TextSample(content="turn on the tv", label="TV_ON")
        second = TextSample(content="turn on the tv", label="TV_ON_ALT_LABEL")

        assert first.content_hash == second.content_hash
        assert first.name == second.name
