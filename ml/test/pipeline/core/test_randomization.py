"""Unit tests for `PassFilter`/`MinMaxFilter`/`NormalFilter` and `VariationGenerator`.

Per `ml/_spec_OopPipeline.md`'s Component Breakdown, both the pass-filter family and
`VariationGenerator` are Testable -- `VariationGenerator` in particular is called out as "the
highest-risk logic in the pipeline," so this suite covers the quantization-grid math ("Precision-
quantized rejection sampling"), every seed/name-hash-derived formula ("Seed-based randomisation
with pass filters"), and the independence property those formulas are designed to guarantee:
each variable's derived value must not depend on what other variables were requested, or in what
order.
"""

from __future__ import annotations

import hashlib

import pytest

from pipeline.core.randomization import MinMaxFilter, NormalFilter, PassFilter, VariationGenerator


class _NeverAcceptingFilter(PassFilter):
    """Test double: shares `PassFilter`'s quantization grid but rejects every candidate, so
    `VariationGenerator.generate()` is forced through all 1000 attempts deterministically."""

    def density(self, value: float) -> float:
        return 0.0


class _RejectsFirstQuantizeCallFilter(PassFilter):
    """Test double: `quantize()` returns `None` (grid-miss) on its first call only, then
    delegates to `PassFilter.quantize()` normally -- deterministically exercises
    `VariationGenerator.generate()`'s retry-after-grid-miss branch instead of relying on a real
    grid-miss happening to occur within the real hash sequence."""

    def __init__(self, min_val: float, max_val: float, precision: int = 0) -> None:
        super().__init__(min_val, max_val, precision)
        self._calls = 0

    def quantize(self, raw: int) -> float | None:
        self._calls += 1
        if self._calls == 1:
            return None
        return super().quantize(raw)


class TestPassFilter:
    def test_PassFilter_Init_PrecisionZero_ComputesScaleOfOne(self) -> None:
        pass_filter = PassFilter(min_val=0.0, max_val=10.0, precision=0)

        assert pass_filter.scale == 1

    def test_PassFilter_Init_PrecisionTwo_ComputesScaleOf100(self) -> None:
        pass_filter = PassFilter(min_val=0.0, max_val=10.0, precision=2)

        assert pass_filter.scale == 100

    def test_PassFilter_Init_ComputesScaledMinAndMaxBounds(self) -> None:
        pass_filter = PassFilter(min_val=1.5, max_val=3.5, precision=1)

        assert pass_filter._min_scaled == 15
        assert pass_filter._max_scaled == 35

    def test_PassFilter_Init_GridSizeIsPowerOfTwo_Pow2RangeEqualsGridSize(self) -> None:
        # min=0, max=3 -> 4 grid points, already a power of two.
        pass_filter = PassFilter(min_val=0.0, max_val=3.0, precision=0)

        assert pass_filter._grid_size == 4
        assert pass_filter._pow2_range == 4

    def test_PassFilter_Init_GridSizeNotPowerOfTwo_Pow2RangeRoundsUp(self) -> None:
        # min=0, max=4 -> 5 grid points -> smallest covering power of two is 8.
        pass_filter = PassFilter(min_val=0.0, max_val=4.0, precision=0)

        assert pass_filter._grid_size == 5
        assert pass_filter._pow2_range == 8

    def test_PassFilter_Init_MinEqualsMax_ComputesSinglePointGrid(self) -> None:
        pass_filter = PassFilter(min_val=5.0, max_val=5.0, precision=0)

        assert pass_filter._grid_size == 1
        assert pass_filter._pow2_range == 1

    def test_PassFilter_Init_MinGreaterThanMax_RaisesValueError(self) -> None:
        with pytest.raises(ValueError):
            PassFilter(min_val=10.0, max_val=0.0, precision=0)

    def test_PassFilter_Density_AnyValue_ReturnsOne(self) -> None:
        pass_filter = PassFilter(min_val=0.0, max_val=10.0, precision=0)

        assert pass_filter.density(0.0) == 1.0
        assert pass_filter.density(10.0) == 1.0
        assert pass_filter.density(-100.0) == 1.0

    def test_PassFilter_Quantize_RawWithinGrid_ReturnsGridValue(self) -> None:
        pass_filter = PassFilter(min_val=0.0, max_val=3.0, precision=0)

        result = pass_filter.quantize(raw=2)

        assert result == 2.0

    def test_PassFilter_Quantize_RawOutsideGrid_ReturnsNone(self) -> None:
        # grid_size=5, pow2_range=8 -> raw % 8 in {5, 6, 7} falls outside the grid.
        pass_filter = PassFilter(min_val=0.0, max_val=4.0, precision=0)

        result = pass_filter.quantize(raw=5)

        assert result is None

    def test_PassFilter_Quantize_AppliesPrecisionScale(self) -> None:
        pass_filter = PassFilter(min_val=0.0, max_val=3.0, precision=1)

        result = pass_filter.quantize(raw=5)

        assert result == pytest.approx(0.5)


class TestMinMaxFilter:
    def test_MinMaxFilter_Density_AnyValue_ReturnsOne(self) -> None:
        pass_filter = MinMaxFilter(min_val=0.0, max_val=10.0, precision=1)

        assert pass_filter.density(3.7) == 1.0

    def test_MinMaxFilter_Init_ComputesSameGridAsPassFilter(self) -> None:
        pass_filter = MinMaxFilter(min_val=0.0, max_val=4.0, precision=0)

        assert pass_filter._grid_size == 5
        assert pass_filter._pow2_range == 8

    def test_MinMaxFilter_Init_MinGreaterThanMax_RaisesValueError(self) -> None:
        with pytest.raises(ValueError):
            MinMaxFilter(min_val=10.0, max_val=0.0, precision=0)


class TestNormalFilter:
    def test_NormalFilter_Density_AtMean_ReturnsOne(self) -> None:
        pass_filter = NormalFilter(mean=5.0, std=1.0, min_val=0.0, max_val=10.0, precision=1)

        assert pass_filter.density(5.0) == pytest.approx(1.0)

    def test_NormalFilter_Density_AwayFromMean_ReturnsLessThanOne(self) -> None:
        pass_filter = NormalFilter(mean=5.0, std=1.0, min_val=0.0, max_val=10.0, precision=1)

        assert pass_filter.density(7.0) < 1.0

    def test_NormalFilter_Density_SymmetricAroundMean_ReturnsEqualValues(self) -> None:
        pass_filter = NormalFilter(mean=5.0, std=1.0, min_val=0.0, max_val=10.0, precision=1)

        assert pass_filter.density(3.0) == pytest.approx(pass_filter.density(7.0))

    def test_NormalFilter_Density_FartherFromMean_ReturnsLowerDensity(self) -> None:
        pass_filter = NormalFilter(mean=5.0, std=1.0, min_val=0.0, max_val=10.0, precision=1)

        assert pass_filter.density(6.0) > pass_filter.density(8.0)

    def test_NormalFilter_Init_StdZero_RaisesValueError(self) -> None:
        with pytest.raises(ValueError):
            NormalFilter(mean=5.0, std=0.0, min_val=0.0, max_val=10.0, precision=0)

    def test_NormalFilter_Init_StdNegative_RaisesValueError(self) -> None:
        with pytest.raises(ValueError):
            NormalFilter(mean=5.0, std=-1.0, min_val=0.0, max_val=10.0, precision=0)

    def test_NormalFilter_Init_MinGreaterThanMax_RaisesValueError(self) -> None:
        with pytest.raises(ValueError):
            NormalFilter(mean=5.0, std=1.0, min_val=10.0, max_val=0.0, precision=0)


class TestVariationGenerator:
    # -- should_vary --------------------------------------------------------------------

    def test_VariationGenerator_ShouldVary_FrequencyZero_AlwaysReturnsFalse(self) -> None:
        generator = VariationGenerator(seed=42)

        assert generator.should_vary("speech_rate", frequency=0.0) is False

    def test_VariationGenerator_ShouldVary_FrequencyOne_AlwaysReturnsTrue(self) -> None:
        generator = VariationGenerator(seed=42)

        assert generator.should_vary("speech_rate", frequency=1.0) is True

    def test_VariationGenerator_ShouldVary_FrequencyAboveComputedProbability_ReturnsTrue(
        self,
    ) -> None:
        seed = 42
        name = "speech_rate"
        digest = hashlib.sha256(f"{seed}:{name}:vary".encode("utf-8")).digest()
        probability = int.from_bytes(digest[:8], "big") / 2**64
        generator = VariationGenerator(seed)

        result = generator.should_vary(name, frequency=min(probability + 0.01, 1.0))

        assert result is True

    def test_VariationGenerator_ShouldVary_FrequencyBelowComputedProbability_ReturnsFalse(
        self,
    ) -> None:
        seed = 42
        name = "speech_rate"
        digest = hashlib.sha256(f"{seed}:{name}:vary".encode("utf-8")).digest()
        probability = int.from_bytes(digest[:8], "big") / 2**64
        generator = VariationGenerator(seed)

        result = generator.should_vary(name, frequency=max(probability - 0.01, 0.0))

        assert result is False

    def test_VariationGenerator_ShouldVary_SameSeedAndName_IsDeterministic(self) -> None:
        first = VariationGenerator(seed=42).should_vary("speech_rate", frequency=0.5)
        second = VariationGenerator(seed=42).should_vary("speech_rate", frequency=0.5)

        assert first == second

    # -- generate -------------------------------------------------------------------------

    def test_VariationGenerator_Generate_ReturnsValueWithinFilterBounds(self) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = MinMaxFilter(min_val=0.0, max_val=10.0, precision=1)

        result = generator.generate("speech_rate", pass_filter)

        assert 0.0 <= result <= 10.0

    def test_VariationGenerator_Generate_SameSeedAndName_IsDeterministic(self) -> None:
        pass_filter = MinMaxFilter(min_val=0.0, max_val=10.0, precision=1)

        first = VariationGenerator(seed=7).generate("speech_rate", pass_filter)
        second = VariationGenerator(seed=7).generate("speech_rate", pass_filter)

        assert first == second

    def test_VariationGenerator_Generate_MinEqualsMax_ReturnsThatSingleValue(self) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = MinMaxFilter(min_val=5.0, max_val=5.0, precision=0)

        result = generator.generate("speech_rate", pass_filter)

        assert result == 5.0

    def test_VariationGenerator_Generate_NeverAcceptingFilter_RaisesValueErrorAfter1000Attempts(
        self,
    ) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = _NeverAcceptingFilter(min_val=0.0, max_val=10.0, precision=0)

        with pytest.raises(ValueError):
            generator.generate("speech_rate", pass_filter)

    def test_VariationGenerator_Generate_FirstCandidateOutsideGrid_RetriesAndSucceeds(
        self,
    ) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = _RejectsFirstQuantizeCallFilter(min_val=0.0, max_val=10.0, precision=0)

        result = generator.generate("speech_rate", pass_filter)

        assert 0.0 <= result <= 10.0
        assert pass_filter._calls >= 2

    # -- generate_int -----------------------------------------------------------------------

    def test_VariationGenerator_GenerateInt_RangeZero_ReturnsMinValImmediately(self) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = MinMaxFilter(min_val=3.0, max_val=3.0)

        result = generator.generate_int("pitch", pass_filter)

        assert result == 3

    def test_VariationGenerator_GenerateInt_ReturnsValueWithinRange(self) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = MinMaxFilter(min_val=0.0, max_val=10.0)

        result = generator.generate_int("pitch", pass_filter)

        assert 0 <= result <= 10

    def test_VariationGenerator_GenerateInt_SameSeedAndName_IsDeterministic(self) -> None:
        pass_filter = MinMaxFilter(min_val=0.0, max_val=10.0)

        first = VariationGenerator(seed=7).generate_int("pitch", pass_filter)
        second = VariationGenerator(seed=7).generate_int("pitch", pass_filter)

        assert first == second

    def test_VariationGenerator_GenerateInt_TruncatesFloatBoundsTowardZero(self) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = MinMaxFilter(min_val=3.9, max_val=3.9)

        result = generator.generate_int("pitch", pass_filter)

        assert result == 3

    def test_VariationGenerator_GenerateInt_CandidateAlwaysOutsideRange_RaisesValueErrorAfter1000Attempts(
        self, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        generator = VariationGenerator(seed=7)
        pass_filter = MinMaxFilter(min_val=0.0, max_val=2.0)
        # mask for a range of 2 is 3 (0b11); a raw draw of 3 always exceeds the range (2), so
        # every attempt is rejected.
        monkeypatch.setattr(generator, "_digest_uint64", lambda message: 3)

        with pytest.raises(ValueError):
            generator.generate_int("pitch", pass_filter)

    # -- choose -----------------------------------------------------------------------------

    def test_VariationGenerator_Choose_SelectsOptionAtHashDerivedIndex(self) -> None:
        seed = 7
        name = "voice"
        options = ["Jenny", "Guy", "Aria"]
        digest = hashlib.sha256(f"{seed}:{name}:0".encode("utf-8")).digest()
        expected_index = int.from_bytes(digest[:8], "big") % len(options)
        generator = VariationGenerator(seed)

        result = generator.choose(name, options)

        assert result == options[expected_index]

    def test_VariationGenerator_Choose_SingleOption_AlwaysReturnsThatOption(self) -> None:
        generator = VariationGenerator(seed=7)

        result = generator.choose("voice", ["OnlyOption"])

        assert result == "OnlyOption"

    def test_VariationGenerator_Choose_SameSeedAndName_IsDeterministic(self) -> None:
        options = ["Jenny", "Guy", "Aria"]

        first = VariationGenerator(seed=7).choose("voice", options)
        second = VariationGenerator(seed=7).choose("voice", options)

        assert first == second

    def test_VariationGenerator_Choose_EmptyOptions_RaisesValueError(self) -> None:
        generator = VariationGenerator(seed=7)

        with pytest.raises(ValueError):
            generator.choose("voice", [])

    # -- independence property ---------------------------------------------------------------

    def test_VariationGenerator_AllMethods_ValueForOneNameIndependentOfCallOrder(self) -> None:
        """Per the spec's "Seed-based randomisation with pass filters" decision: each variable's
        derived value must not depend on what other variables were requested, or in what order --
        e.g. adding or reordering variables in a hypothetical `_get_applied_values` must not
        change any other variable's own value."""
        seed = 999
        float_filter = MinMaxFilter(min_val=0.0, max_val=100.0, precision=1)
        int_filter = MinMaxFilter(min_val=0.0, max_val=50.0)
        options = ["Jenny", "Guy", "Aria"]

        forward = VariationGenerator(seed)
        forward_should_vary = forward.should_vary("delay", frequency=0.5)
        forward_generate = forward.generate("speech_rate", float_filter)
        forward_generate_int = forward.generate_int("pitch", int_filter)
        forward_choose = forward.choose("voice", options)

        reversed_order = VariationGenerator(seed)
        reversed_choose = reversed_order.choose("voice", options)
        reversed_generate_int = reversed_order.generate_int("pitch", int_filter)
        reversed_generate = reversed_order.generate("speech_rate", float_filter)
        reversed_should_vary = reversed_order.should_vary("delay", frequency=0.5)

        assert reversed_should_vary == forward_should_vary
        assert reversed_generate == forward_generate
        assert reversed_generate_int == forward_generate_int
        assert reversed_choose == forward_choose
