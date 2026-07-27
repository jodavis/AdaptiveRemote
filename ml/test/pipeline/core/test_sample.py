from __future__ import annotations

from pipeline.core.sample import TextSample


def test_text_sample_post_init_content_hash_set_name_to_content_hash() -> None:
    # Arrange
    content_hash = "abc123"

    # Act
    sample = TextSample(content_hash=content_hash, content="Turn on the TV")

    # Assert
    assert sample.name == content_hash
