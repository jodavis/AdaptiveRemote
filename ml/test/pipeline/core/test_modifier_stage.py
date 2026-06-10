from __future__ import annotations

import asyncio
import hashlib
import json
from pathlib import Path
from typing import Any, ClassVar

import pytest

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.modifier_stage import ModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, TextSample


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _content_hash(parent_content_hash: str, seed: int, applied_values: dict[str, Any]) -> str:
    return ModifierStage._compute_content_hash(parent_content_hash, seed, applied_values)


def _text_sample(content: str = "turn on the tv", label: str = "TV_ON") -> TextSample:
    h = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return TextSample(seed=0, content_hash=h, content=content, label=label)


def _audio_sample(
    *,
    id: str = "TV_ON_fake",
    seed: int = 42,
    parent_content_hash: str,
    applied_values: dict[str, Any] | None = None,
) -> AudioSample:
    av = applied_values or {}
    ch = _content_hash(parent_content_hash, seed, av)
    return AudioSample(
        id=id,
        seed=seed,
        content_hash=ch,
        path=Path(f"{id}.wav"),
        parent_content_hash=parent_content_hash,
        transcript="TV_ON",
        applied_values=av,
    )


def _write_prev_manifest(path: Path, samples: list[AudioSample]) -> None:
    ManifestStore().write(Manifest(samples), path)


class _FakeStage(ModifierStage[TextSample, AudioSample]):
    """Minimal concrete subclass for testing ModifierStage logic."""

    _is_deterministic: ClassVar[bool] = False

    def __init__(
        self,
        output_dir: Path,
        manifest_store: ManifestStore,
        *,
        av_by_content_hash: dict[str, dict[str, Any]] | None = None,
    ) -> None:
        super().__init__(output_dir, manifest_store)
        # per-sample applied_values; keyed on input_sample.content_hash
        self._av_map: dict[str, dict[str, Any]] = av_by_content_hash or {}
        self.generate_output_calls: list[tuple[str, int, dict[str, Any]]] = []
        self.derive_id_calls: list[tuple[str, dict[str, Any]]] = []

    def _get_applied_values(
        self, sample: TextSample, generator: VariationGenerator
    ) -> dict[str, Any]:
        return dict(self._av_map.get(sample.content_hash, {}))

    async def _generate_output(
        self,
        input_sample: TextSample,
        output_id: str,
        output_seed: int,
        applied_values: dict[str, Any],
        parent_content_hash: str,
    ) -> AudioSample:
        self.generate_output_calls.append((output_id, output_seed, applied_values))
        (self._output_dir / f"{output_id}.wav").write_bytes(b"audio")
        return AudioSample(
            id=output_id,
            seed=output_seed,
            content_hash=_content_hash(parent_content_hash, output_seed, applied_values),
            path=Path(f"{output_id}.wav"),
            parent_content_hash=parent_content_hash,
            transcript=input_sample.label,
            applied_values=applied_values,
        )

    def _derive_id(
        self, input_sample: TextSample, applied_values: dict[str, Any]
    ) -> str:
        self.derive_id_calls.append((input_sample.content_hash, applied_values))
        return f"{input_sample.id}_fake"


class _DeterministicFakeStage(_FakeStage):
    _is_deterministic: ClassVar[bool] = True


# ---------------------------------------------------------------------------
# TestComputeContentHash
# ---------------------------------------------------------------------------

class TestComputeContentHash:
    def test_known_value(self) -> None:
        # Verify exact sha256 formula: sha256(parent + ":" + str(seed) + ":" + canonical(av))
        parent = "abc"
        seed = 0
        av: dict[str, Any] = {}
        canonical = json.dumps(av, sort_keys=True, separators=(",", ":"), ensure_ascii=True)
        raw = f"{parent}:{seed}:{canonical}"
        expected = hashlib.sha256(raw.encode("utf-8")).hexdigest()

        result = ModifierStage._compute_content_hash(parent, seed, av)

        assert result == expected

    def test_sort_keys_ensures_stability(self) -> None:
        h1 = ModifierStage._compute_content_hash("p", 1, {"b": 2, "a": 1})
        h2 = ModifierStage._compute_content_hash("p", 1, {"a": 1, "b": 2})
        assert h1 == h2

    def test_different_seeds_give_different_hashes(self) -> None:
        h1 = ModifierStage._compute_content_hash("p", 1, {})
        h2 = ModifierStage._compute_content_hash("p", 2, {})
        assert h1 != h2

    def test_different_parents_give_different_hashes(self) -> None:
        h1 = ModifierStage._compute_content_hash("pA", 0, {})
        h2 = ModifierStage._compute_content_hash("pB", 0, {})
        assert h1 != h2

    def test_different_applied_values_give_different_hashes(self) -> None:
        h1 = ModifierStage._compute_content_hash("p", 0, {"x": 1})
        h2 = ModifierStage._compute_content_hash("p", 0, {"x": 2})
        assert h1 != h2


# ---------------------------------------------------------------------------
# TestSkipPath
# ---------------------------------------------------------------------------

class TestSkipPath:
    def test_unchanged_sample_is_kept_verbatim(self, tmp_path: Path) -> None:
        inp = _text_sample()
        prev = _audio_sample(seed=77, parent_content_hash=inp.content_hash, applied_values={})
        _write_prev_manifest(tmp_path / "manifest.json", [prev])

        stage = _FakeStage(tmp_path, ManifestStore())
        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert len(result.samples) == 1
        out = result.samples[0]
        assert out.id == prev.id
        assert out.content_hash == prev.content_hash
        assert out.seed == prev.seed

    def test_generate_output_not_called_on_skip(self, tmp_path: Path) -> None:
        inp = _text_sample()
        prev = _audio_sample(seed=77, parent_content_hash=inp.content_hash, applied_values={})
        _write_prev_manifest(tmp_path / "manifest.json", [prev])

        stage = _FakeStage(tmp_path, ManifestStore())
        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert stage.generate_output_calls == []

    def test_existing_output_file_not_deleted_on_skip(self, tmp_path: Path) -> None:
        inp = _text_sample()
        prev = _audio_sample(seed=77, parent_content_hash=inp.content_hash, applied_values={})
        _write_prev_manifest(tmp_path / "manifest.json", [prev])
        existing_file = tmp_path / prev.path.name
        existing_file.write_bytes(b"audio")

        stage = _FakeStage(tmp_path, ManifestStore())
        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert existing_file.exists()

    def test_manifest_written_with_preserved_sample(self, tmp_path: Path) -> None:
        inp = _text_sample()
        prev = _audio_sample(seed=77, parent_content_hash=inp.content_hash, applied_values={})
        _write_prev_manifest(tmp_path / "manifest.json", [prev])

        stage = _FakeStage(tmp_path, ManifestStore())
        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        written = ManifestStore().read(tmp_path / "manifest.json")
        assert len(written.samples) == 1
        assert written.samples[0].id == prev.id


# ---------------------------------------------------------------------------
# TestRegenPath
# ---------------------------------------------------------------------------

class TestRegenPath:
    def _setup_regen(
        self, tmp_path: Path, old_av: dict[str, Any], new_av: dict[str, Any]
    ) -> tuple[TextSample, AudioSample, _FakeStage]:
        """Creates prev manifest with old_av; stage returns new_av for the sample."""
        inp = _text_sample()
        stored_seed = 99
        # content_hash was computed with old_av, so it won't match new_av → regen
        prev = AudioSample(
            id="TV_ON_old",
            seed=stored_seed,
            content_hash=_content_hash(inp.content_hash, stored_seed, old_av),
            path=Path("TV_ON_old.wav"),
            parent_content_hash=inp.content_hash,
            transcript="TV_ON",
            applied_values=old_av,
        )
        _write_prev_manifest(tmp_path / "manifest.json", [prev])
        stage = _FakeStage(
            tmp_path,
            ManifestStore(),
            av_by_content_hash={inp.content_hash: new_av},
        )
        return inp, prev, stage

    def test_regen_produces_new_id_via_derive_id(self, tmp_path: Path) -> None:
        inp, prev, stage = self._setup_regen(tmp_path, {"x": 1}, {"x": 2})
        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert len(result.samples) == 1
        assert result.samples[0].id != prev.id
        assert len(stage.derive_id_calls) == 1

    def test_regen_preserves_stored_seed(self, tmp_path: Path) -> None:
        inp, prev, stage = self._setup_regen(tmp_path, {"x": 1}, {"x": 2})
        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert result.samples[0].seed == prev.seed

    def test_regen_updates_content_hash(self, tmp_path: Path) -> None:
        inp, prev, stage = self._setup_regen(tmp_path, {"x": 1}, {"x": 2})
        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert result.samples[0].content_hash != prev.content_hash
        expected = _content_hash(inp.content_hash, prev.seed, {"x": 2})
        assert result.samples[0].content_hash == expected

    def test_regen_old_file_gc_deleted(self, tmp_path: Path) -> None:
        inp, prev, stage = self._setup_regen(tmp_path, {"x": 1}, {"x": 2})
        old_file = tmp_path / prev.path.name
        old_file.write_bytes(b"old audio")

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert not old_file.exists()

    def test_regen_generate_output_called_with_stored_seed(self, tmp_path: Path) -> None:
        inp, prev, stage = self._setup_regen(tmp_path, {"x": 1}, {"x": 2})
        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert len(stage.generate_output_calls) == 1
        _id, seed, av = stage.generate_output_calls[0]
        assert seed == prev.seed
        assert av == {"x": 2}


# ---------------------------------------------------------------------------
# TestNewSamplePath
# ---------------------------------------------------------------------------

class TestNewSamplePath:
    def test_new_sample_calls_derive_id(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert len(stage.derive_id_calls) == 1

    def test_new_sample_calls_generate_output(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert len(stage.generate_output_calls) == 1

    def test_new_sample_gets_nonzero_seed(self, tmp_path: Path) -> None:
        # Stochastic stage → seed from os.urandom; not 0 (astronomically unlikely to be 0)
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        # os.urandom(8) producing 0 is 1 in 2^64; treat as impossible for test purposes
        assert result.samples[0].seed != 0

    def test_no_previous_manifest_means_all_samples_are_new(self, tmp_path: Path) -> None:
        samples = [_text_sample(content=f"phrase {i}") for i in range(3)]
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest(samples), tmp_path / "manifest.json"))

        assert len(stage.generate_output_calls) == 3

    def test_new_sample_id_derived_not_uuid(self, tmp_path: Path) -> None:
        # _derive_id is called (not uuid4); our stub returns "{id}_fake"
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert result.samples[0].id.endswith("_fake")

    def test_manifest_written_after_new_sample(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        written = ManifestStore().read(tmp_path / "manifest.json")
        assert len(written.samples) == 1


# ---------------------------------------------------------------------------
# TestDeterministicStage
# ---------------------------------------------------------------------------

class TestDeterministicStage:
    def test_new_sample_gets_seed_zero(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _DeterministicFakeStage(tmp_path, ManifestStore())

        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert result.samples[0].seed == 0

    def test_generate_output_called_with_seed_zero(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _DeterministicFakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        _id, seed, _av = stage.generate_output_calls[0]
        assert seed == 0

    def test_multiple_new_samples_all_get_seed_zero(self, tmp_path: Path) -> None:
        samples = [_text_sample(content=f"phrase {i}") for i in range(3)]
        stage = _DeterministicFakeStage(tmp_path, ManifestStore())

        result = asyncio.run(stage.transform(Manifest(samples), tmp_path / "manifest.json"))

        assert all(s.seed == 0 for s in result.samples)


# ---------------------------------------------------------------------------
# TestGarbageCollection
# ---------------------------------------------------------------------------

class TestGarbageCollection:
    def test_gc_removes_orphaned_file(self, tmp_path: Path) -> None:
        orphan = tmp_path / "orphan.wav"
        orphan.write_bytes(b"stale")
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert not orphan.exists()

    def test_gc_removes_multiple_orphaned_files(self, tmp_path: Path) -> None:
        orphans = [tmp_path / f"orphan{i}.wav" for i in range(3)]
        for f in orphans:
            f.write_bytes(b"stale")
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert all(not f.exists() for f in orphans)

    def test_gc_does_not_delete_manifest_json(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert (tmp_path / "manifest.json").exists()

    def test_gc_does_not_delete_current_output_file(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        result = asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        output_path = tmp_path / result.samples[0].path.name
        assert output_path.exists()

    def test_gc_does_not_delete_skipped_file(self, tmp_path: Path) -> None:
        inp = _text_sample()
        prev = _audio_sample(seed=77, parent_content_hash=inp.content_hash, applied_values={})
        _write_prev_manifest(tmp_path / "manifest.json", [prev])
        existing_file = tmp_path / prev.path.name
        existing_file.write_bytes(b"audio")
        orphan = tmp_path / "orphan.wav"
        orphan.write_bytes(b"stale")

        stage = _FakeStage(tmp_path, ManifestStore())
        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))

        assert existing_file.exists()
        assert not orphan.exists()

    def test_gc_empty_output_dir_does_not_raise(self, tmp_path: Path) -> None:
        inp = _text_sample()
        stage = _FakeStage(tmp_path, ManifestStore())

        # Should not raise even when output_dir is empty (no files to GC)
        asyncio.run(stage.transform(Manifest([inp]), tmp_path / "manifest.json"))


# ---------------------------------------------------------------------------
# TestSplitBehavior
# ---------------------------------------------------------------------------

class TestSplitBehavior:
    """Two samples; same 'constraint change' causes a skip for one but regen for the other.

    Demonstrates that skip/regen is determined per-sample by whether the recomputed
    content_hash matches the stored content_hash — not by the seed alone.
    """

    def test_split_behavior_skip_and_regen_in_one_transform(self, tmp_path: Path) -> None:
        inp_a = _text_sample(content="phrase a", label="A")
        inp_b = _text_sample(content="phrase b", label="B")

        seed_a = 10
        seed_b = 20
        # Both samples previously had {"x": 1}
        old_av = {"x": 1}
        prev_a = AudioSample(
            id="A_fake",
            seed=seed_a,
            content_hash=_content_hash(inp_a.content_hash, seed_a, old_av),
            path=Path("A_fake.wav"),
            parent_content_hash=inp_a.content_hash,
            transcript="A",
            applied_values=old_av,
        )
        prev_b = AudioSample(
            id="B_fake",
            seed=seed_b,
            content_hash=_content_hash(inp_b.content_hash, seed_b, old_av),
            path=Path("B_fake.wav"),
            parent_content_hash=inp_b.content_hash,
            transcript="B",
            applied_values=old_av,
        )
        _write_prev_manifest(tmp_path / "manifest.json", [prev_a, prev_b])

        # After constraint change: sample A still returns {"x": 1} (no change → skip)
        # but sample B now returns {"x": 2} (changed → regen)
        stage = _FakeStage(
            tmp_path,
            ManifestStore(),
            av_by_content_hash={
                inp_a.content_hash: {"x": 1},
                inp_b.content_hash: {"x": 2},
            },
        )
        result = asyncio.run(
            stage.transform(Manifest([inp_a, inp_b]), tmp_path / "manifest.json")
        )

        assert len(result.samples) == 2
        out_a = next(s for s in result.samples if s.parent_content_hash == inp_a.content_hash)
        out_b = next(s for s in result.samples if s.parent_content_hash == inp_b.content_hash)

        # Sample A: skipped — same id, same content_hash, no regen call
        assert out_a.id == prev_a.id
        assert out_a.content_hash == prev_a.content_hash

        # Sample B: regenerated — new id, same seed, different content_hash
        assert out_b.id != prev_b.id
        assert out_b.seed == prev_b.seed
        assert out_b.content_hash != prev_b.content_hash

        assert len(stage.generate_output_calls) == 1
