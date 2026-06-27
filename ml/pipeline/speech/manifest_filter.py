"""Manifest lookup utilities shared between training, evaluation, and testing stages.

lookup_sample_triplets() accepts a split manifest and the full spectrogram/token manifests,
and returns matched (AudioSample, SampleSpectrogram, SampleTokens) triples for each sample
whose features are present in both manifests.
"""

from __future__ import annotations

import logging

from pipeline.core.manifest import Manifest
from pipeline.core.sample import AudioSample, SampleSpectrogram, SampleTokens

_logger = logging.getLogger(__name__)


def lookup_sample_triplets(
    split_manifest: Manifest[AudioSample],
    spectrogram_manifest: Manifest[SampleSpectrogram],
    token_manifest: Manifest[SampleTokens],
) -> list[tuple[AudioSample, SampleSpectrogram, SampleTokens]]:
    """Return matched (AudioSample, SampleSpectrogram, SampleTokens) triples.

    Matches SampleSpectrogram.parent_id and SampleTokens.parent_id to AudioSample.id.
    Samples missing a spectrogram or token entry are skipped with a WARNING log.

    Args:
        split_manifest: the target split (train, val, or test) as an AudioSample manifest.
        spectrogram_manifest: full spectrogram manifest (all splits).
        token_manifest: full token manifest (all splits).

    Returns:
        List of (audio, spectrogram, token) triples, one per matched sample in split_manifest.
    """
    split_by_id: dict[str, AudioSample] = {s.id: s for s in split_manifest.samples}
    spec_by_parent: dict[str, SampleSpectrogram] = {
        s.parent_id: s
        for s in spectrogram_manifest.samples
        if s.parent_id in split_by_id
    }
    tok_by_parent: dict[str, SampleTokens] = {
        s.parent_id: s
        for s in token_manifest.samples
        if s.parent_id in split_by_id
    }

    result: list[tuple[AudioSample, SampleSpectrogram, SampleTokens]] = []
    for audio_id, audio_sample in split_by_id.items():
        if audio_id not in spec_by_parent:
            _logger.warning("No spectrogram for parent_id %s — skipping", audio_id)
            continue
        if audio_id not in tok_by_parent:
            _logger.warning("No token sample for parent_id %s — skipping", audio_id)
            continue
        result.append((audio_sample, spec_by_parent[audio_id], tok_by_parent[audio_id]))

    return result
