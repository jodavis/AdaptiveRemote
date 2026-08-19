"""Unit tests for `ModifierStage[T_in, T_out]` and `RandomizedModifierStage[T_in, T_out]`.

Per `ml/_spec_OopPipeline.md`'s "ModifierStage for all per-sample file transformations" and
"Previous output manifest as the seed store" decisions, `RandomizedModifierStage` is "the
highest-risk logic in the pipeline" -- this suite covers every case of both levels' skip/
regenerate algorithms (fresh run, fully up-to-date run, and -- for `RandomizedModifierStage`
specifically -- a partial-update run where some samples skip while others regenerate with their
stored seed), plus a GC regression test asserting garbage collection never touches a file outside
`self._output_dir`, even when a passthrough sample's `path` points at an ancestor stage's
directory.

Since no concrete `ModifierStage`/`RandomizedModifierStage` subclass exists in the codebase yet
(the first one, `TtsSampleGenerator`, lands in a later task), this suite defines its own minimal
concrete test doubles, `_PassthroughModifierStage`/`_PassthroughRandomizedModifierStage`, per
`ml/test/pipeline/core/test_randomization.py`'s established plain-subclass-double convention.
"""

from __future__ import annotations

import asyncio
import hashlib
import json
from pathlib import Path
from typing import Any

import pytest

from pipeline.core.manifest import Manifest, ManifestStore
from pipeline.core.modifier_stage import ModifierStage, RandomizedModifierStage
from pipeline.core.randomization import VariationGenerator
from pipeline.core.sample import AudioSample, SampleSpectrogram, SampleWithPath


class _PassthroughModifierStage(ModifierStage[SampleWithPath, SampleSpectrogram]):
    """Minimal concrete `ModifierStage` test double.

    Outputs `SampleSpectrogram` (a plain `SampleWithPath` subtype `ManifestStore` knows how to
    round-trip -- bare `SampleWithPath` is not itself a registered manifest sample type) --
    `SampleSpectrogram` is exactly what the real, deterministic `SpectrogramStage` produces via
    this same base class. `applied_values_by_name` lets a test simulate a stage-config change for
    one input sample without affecting others (`{}` for any name not present), exercising the
    "applied values changed" regenerate case independently of `parent_content_hash` matching.
    `next_output`, when set, makes `_generate_output` return that object directly instead of
    writing a placeholder file -- used to simulate a passthrough sample whose `path` points
    outside `self._output_dir`.
    """

    def __init__(self, output_dir: Path, manifest_store: ManifestStore) -> None:
        super().__init__(output_dir, manifest_store)
        self.applied_values_by_name: dict[str, dict[str, Any]] = {}
        self.generate_calls: list[SampleWithPath] = []
        self.next_output: SampleSpectrogram | None = None

    def _compute_applied_values(self, input_sample: SampleWithPath) -> dict[str, Any]:
        return dict(self.applied_values_by_name.get(input_sample.name, {}))

    async def _generate_output(
        self, input_sample: SampleWithPath, applied_values: dict[str, Any], content_hash: str
    ) -> SampleSpectrogram:
        self.generate_calls.append(input_sample)
        if self.next_output is not None:
            return self.next_output

        output = SampleSpectrogram(
            name=content_hash,
            content_hash=content_hash,
            path=f"{content_hash}.dat",
            parent_name=input_sample.name,
            parent_content_hash=input_sample.content_hash,
            applied_values=applied_values,
        )
        (self._output_dir / output.path).write_text("data")
        return output


class _PassthroughRandomizedModifierStage(RandomizedModifierStage[AudioSample, AudioSample]):
    """Minimal concrete `RandomizedModifierStage` test double, mirroring
    `_PassthroughModifierStage` but for the seeded three-case algorithm. `generate_calls` records
    `(input_sample, seed)` pairs so tests can assert both whether regeneration happened and which
    seed (fresh vs. stored) it happened with.
    """

    def __init__(self, output_dir: Path, manifest_store: ManifestStore) -> None:
        super().__init__(output_dir, manifest_store)
        self.applied_values_by_name: dict[str, dict[str, Any]] = {}
        self.generate_calls: list[tuple[AudioSample, int]] = []

    def _compute_randomized_applied_values(
        self, input_sample: AudioSample, variation_generator: VariationGenerator
    ) -> dict[str, Any]:
        return dict(self.applied_values_by_name.get(input_sample.name, {}))

    async def _generate_randomized_output(
        self,
        input_sample: AudioSample,
        applied_values: dict[str, Any],
        content_hash: str,
        seed: int,
    ) -> AudioSample:
        self.generate_calls.append((input_sample, seed))
        output = AudioSample(
            name=content_hash,
            content_hash=content_hash,
            path=f"{content_hash}.wav",
            parent_name=input_sample.name,
            parent_content_hash=input_sample.content_hash,
            applied_values=applied_values,
            transcript=input_sample.transcript,
            label=input_sample.label,
            seed=seed,
        )
        (self._output_dir / output.path).write_text("audio")
        return output


def _build_input_sample(name: str = "parent", content_hash: str = "parent-hash") -> SampleWithPath:
    return SampleWithPath(
        name=name,
        content_hash=content_hash,
        path=f"{name}.dat",
        parent_name="root",
        parent_content_hash="root-hash",
        applied_values={},
    )


def _build_audio_input_sample(
    name: str = "parent", content_hash: str = "parent-hash"
) -> AudioSample:
    return AudioSample(
        name=name,
        content_hash=content_hash,
        path=f"{name}.wav",
        parent_name="root",
        parent_content_hash="root-hash",
        applied_values={},
        transcript="turn on the tv",
        label="TV_ON",
        seed=1,
    )


class TestModifierStage:
    def test_ModifierStage_Init_DirectInstantiation_RaisesTypeError(self, tmp_path: Path) -> None:
        with pytest.raises(TypeError):
            ModifierStage(tmp_path, ManifestStore())  # type: ignore[abstract]

    def test_ModifierStage_Transform_FreshRun_NoPreviousManifest_GeneratesEveryOutput(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())
        input_sample = _build_input_sample()
        input_manifest: Manifest[SampleWithPath] = Manifest([input_sample])

        result = asyncio.run(stage.transform(input_manifest))

        assert stage.generate_calls == [input_sample]
        assert len(result) == 1

    def test_ModifierStage_Transform_FreshRun_WritesOutputManifestFile(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())
        input_manifest: Manifest[SampleWithPath] = Manifest([_build_input_sample()])

        asyncio.run(stage.transform(input_manifest))

        assert (tmp_path / "manifest.json").exists()

    def test_ModifierStage_Transform_UpToDateRun_SkipsGenerationAndReusesPreviousOutput(
        self, tmp_path: Path
    ) -> None:
        store = ManifestStore()
        stage = _PassthroughModifierStage(tmp_path, store)
        input_sample = _build_input_sample()
        first_output = asyncio.run(stage.transform(Manifest([input_sample])))
        stage.generate_calls.clear()

        second_output = asyncio.run(stage.transform(Manifest([input_sample])))

        assert stage.generate_calls == []
        assert second_output.samples == first_output.samples

    def test_ModifierStage_Transform_AppliedValuesChanged_Regenerates(
        self, tmp_path: Path
    ) -> None:
        store = ManifestStore()
        stage = _PassthroughModifierStage(tmp_path, store)
        input_sample = _build_input_sample()
        asyncio.run(stage.transform(Manifest([input_sample])))
        stage.generate_calls.clear()
        stage.applied_values_by_name[input_sample.name] = {"n_mels": 100}

        asyncio.run(stage.transform(Manifest([input_sample])))

        assert stage.generate_calls == [input_sample]

    def test_ModifierStage_Transform_NewSampleNotInPreviousManifest_Regenerates(
        self, tmp_path: Path
    ) -> None:
        store = ManifestStore()
        stage = _PassthroughModifierStage(tmp_path, store)
        first_sample = _build_input_sample(name="first", content_hash="first-hash")
        asyncio.run(stage.transform(Manifest([first_sample])))
        stage.generate_calls.clear()
        second_sample = _build_input_sample(name="second", content_hash="second-hash")

        asyncio.run(stage.transform(Manifest([second_sample])))

        assert stage.generate_calls == [second_sample]

    def test_ModifierStage_Transform_EmptyInputManifest_PropagatesManifestStoreValueError(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())
        empty_manifest: Manifest[SampleWithPath] = Manifest([])

        with pytest.raises(ValueError):
            asyncio.run(stage.transform(empty_manifest))

    def test_ModifierStage_Transform_GC_DeletesOrphanedFileInOutputDir(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())
        orphan_path = tmp_path / "orphan.dat"
        orphan_path.write_text("stale")

        asyncio.run(stage.transform(Manifest([_build_input_sample()])))

        assert not orphan_path.exists()

    def test_ModifierStage_Transform_GC_NeverDeletesFileOutsideOutputDir(
        self, tmp_path: Path
    ) -> None:
        output_dir = tmp_path / "output"
        output_dir.mkdir()
        ancestor_dir = tmp_path / "ancestor"
        ancestor_dir.mkdir()
        ancestor_file = ancestor_dir / "TV_ON_r1.dat"
        ancestor_file.write_text("keep")
        orphan_file = output_dir / "orphan.dat"
        orphan_file.write_text("stale")
        store = ManifestStore()
        stage = _PassthroughModifierStage(output_dir, store)
        input_sample = _build_input_sample()
        passthrough_output = SampleSpectrogram(
            name="passthrough",
            content_hash="passthrough-hash",
            path=str(ancestor_file.relative_to(tmp_path)),
            parent_name=input_sample.name,
            parent_content_hash=input_sample.content_hash,
            applied_values={},
        )
        stage.next_output = passthrough_output

        result = asyncio.run(stage.transform(Manifest([input_sample])))

        assert ancestor_file.exists()
        assert not orphan_file.exists()
        assert result.by_name("passthrough") == passthrough_output

    def test_ModifierStage_GarbageCollect_NeverDeletesManifestJson(self, tmp_path: Path) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())
        manifest_path = tmp_path / "manifest.json"
        manifest_path.write_text("{}")

        stage._garbage_collect(Manifest([]))

        assert manifest_path.exists()

    def test_ModifierStage_ComputeContentHash_MatchesSpecFormula(self, tmp_path: Path) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())
        applied_values = {"b": 2, "a": 1.5}

        result = stage._compute_content_hash("parent-hash", applied_values)

        canonical = json.dumps(
            applied_values, sort_keys=True, separators=(",", ":"), ensure_ascii=True
        )
        expected = hashlib.sha256(f"parent-hash:{canonical}".encode("utf-8")).hexdigest()
        assert result == expected

    def test_ModifierStage_ComputeContentHash_KeyOrderDoesNotAffectHash(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())

        first = stage._compute_content_hash("parent-hash", {"a": 1, "b": 2})
        second = stage._compute_content_hash("parent-hash", {"b": 2, "a": 1})

        assert first == second

    def test_ModifierStage_ComputeContentHash_DifferentAppliedValues_ProducesDifferentHash(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughModifierStage(tmp_path, ManifestStore())

        first = stage._compute_content_hash("parent-hash", {"a": 1})
        second = stage._compute_content_hash("parent-hash", {"a": 2})

        assert first != second


class TestRandomizedModifierStage:
    def test_RandomizedModifierStage_Init_DirectInstantiation_RaisesTypeError(
        self, tmp_path: Path
    ) -> None:
        with pytest.raises(TypeError):
            RandomizedModifierStage(tmp_path, ManifestStore())  # type: ignore[abstract]

    def test_RandomizedModifierStage_Transform_NewSample_DrawsFreshSeedAndGenerates(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        monkeypatch.setattr(stage, "_draw_seed", lambda: 4242)
        input_sample = _build_audio_input_sample()

        asyncio.run(stage.transform(Manifest([input_sample])))

        assert stage.generate_calls == [(input_sample, 4242)]

    def test_RandomizedModifierStage_Transform_FreshRun_WritesOutputManifestFile(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        monkeypatch.setattr(stage, "_draw_seed", lambda: 4242)

        asyncio.run(stage.transform(Manifest([_build_audio_input_sample()])))

        assert (tmp_path / "manifest.json").exists()

    def test_RandomizedModifierStage_Transform_UpToDateRun_SkipsGenerationAndReusesStoredSeed(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        seeds = iter([1111, 2222])
        monkeypatch.setattr(stage, "_draw_seed", lambda: next(seeds))
        input_sample = _build_audio_input_sample()
        first_output = asyncio.run(stage.transform(Manifest([input_sample])))
        stage.generate_calls.clear()

        second_output = asyncio.run(stage.transform(Manifest([input_sample])))

        assert stage.generate_calls == []
        assert second_output.samples[0].seed == 1111
        assert second_output.samples == first_output.samples

    def test_RandomizedModifierStage_Transform_ConstraintsChanged_RegeneratesWithStoredSeed(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        seeds = iter([1111, 2222])
        monkeypatch.setattr(stage, "_draw_seed", lambda: next(seeds))
        input_sample = _build_audio_input_sample()
        asyncio.run(stage.transform(Manifest([input_sample])))
        stage.generate_calls.clear()
        stage.applied_values_by_name[input_sample.name] = {"delay": 0.5}

        asyncio.run(stage.transform(Manifest([input_sample])))

        assert stage.generate_calls == [(input_sample, 1111)]

    def test_RandomizedModifierStage_Transform_NoPreviousOutput_DrawsFreshSeedNotStoredSeed(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        seeds = iter([1111, 2222])
        monkeypatch.setattr(stage, "_draw_seed", lambda: next(seeds))
        first_sample = _build_audio_input_sample(name="first", content_hash="first-hash")
        asyncio.run(stage.transform(Manifest([first_sample])))
        stage.generate_calls.clear()
        second_sample = _build_audio_input_sample(name="second", content_hash="second-hash")

        asyncio.run(stage.transform(Manifest([second_sample])))

        assert stage.generate_calls == [(second_sample, 2222)]

    def test_RandomizedModifierStage_Transform_PartialUpdate_SkipsUnchangedRegeneratesChanged(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        monkeypatch.setattr(stage, "_draw_seed", lambda: 9999)
        unchanged_sample = _build_audio_input_sample(
            name="unchanged", content_hash="unchanged-hash"
        )
        changed_sample = _build_audio_input_sample(name="changed", content_hash="changed-hash")
        asyncio.run(stage.transform(Manifest([unchanged_sample, changed_sample])))
        stage.generate_calls.clear()
        stage.applied_values_by_name["changed"] = {"delay": 0.75}

        asyncio.run(stage.transform(Manifest([unchanged_sample, changed_sample])))

        regenerated_samples = [sample for sample, _ in stage.generate_calls]
        assert regenerated_samples == [changed_sample]

    def test_RandomizedModifierStage_DrawSeed_ReturnsNonNegative64BitInteger(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())

        result = stage._draw_seed()

        assert isinstance(result, int)
        assert 0 <= result < 2**64

    def test_RandomizedModifierStage_ComputeAppliedValues_NotOverridden_RaisesNotImplementedError(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())

        with pytest.raises(NotImplementedError):
            stage._compute_applied_values(_build_audio_input_sample())

    def test_RandomizedModifierStage_GenerateOutput_NotOverridden_RaisesNotImplementedError(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())

        async def _call() -> None:
            await stage._generate_output(_build_audio_input_sample(), {}, "hash")

        with pytest.raises(NotImplementedError):
            asyncio.run(_call())

    def test_RandomizedModifierStage_ComputeSeededContentHash_MatchesSpecFormula(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        applied_values = {"delay": 0.5}

        result = stage._compute_seeded_content_hash("parent-hash", applied_values, seed=42)

        canonical = json.dumps(
            applied_values, sort_keys=True, separators=(",", ":"), ensure_ascii=True
        )
        expected = hashlib.sha256(f"parent-hash:42:{canonical}".encode("utf-8")).hexdigest()
        assert result == expected

    def test_RandomizedModifierStage_ComputeSeededContentHash_DifferentSeed_ProducesDifferentHash(
        self, tmp_path: Path
    ) -> None:
        stage = _PassthroughRandomizedModifierStage(tmp_path, ManifestStore())
        applied_values = {"delay": 0.5}

        first = stage._compute_seeded_content_hash("parent-hash", applied_values, seed=1)
        second = stage._compute_seeded_content_hash("parent-hash", applied_values, seed=2)

        assert first != second
