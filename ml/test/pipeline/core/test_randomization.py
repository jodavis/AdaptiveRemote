from __future__ import annotations

import math
import pytest

from pipeline.core.randomization import (
    MinMaxFilter,
    NormalFilter,
    PassFilter,
    VariationGenerator,
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

class _NeverAcceptFilter(PassFilter):
    """PassFilter whose density() always returns 0.0, so every candidate is rejected."""

    def density(self, value: float) -> float:
        return 0.0

    def sample_domain(self) -> tuple[float, float]:
        return (0.0, 1.0)


# ---------------------------------------------------------------------------
# MinMaxFilter — density
# ---------------------------------------------------------------------------

class TestMinMaxFilterDensity:
    def test_density_at_min_returns_1(self) -> None:
        f = MinMaxFilter(2.0, 8.0)
        assert f.density(2.0) == 1.0

    def test_density_at_max_returns_1(self) -> None:
        f = MinMaxFilter(2.0, 8.0)
        assert f.density(8.0) == 1.0

    def test_density_in_range_returns_1(self) -> None:
        f = MinMaxFilter(0.0, 10.0)
        assert f.density(5.0) == 1.0

    def test_density_below_min_returns_0(self) -> None:
        f = MinMaxFilter(2.0, 8.0)
        assert f.density(1.9) == 0.0

    def test_density_above_max_returns_0(self) -> None:
        f = MinMaxFilter(2.0, 8.0)
        assert f.density(8.1) == 0.0

    def test_sample_domain_returns_min_max(self) -> None:
        f = MinMaxFilter(3.0, 7.5)
        assert f.sample_domain() == (3.0, 7.5)

    def test_min_greater_than_max_raises_value_error(self) -> None:
        with pytest.raises(ValueError):
            MinMaxFilter(8.0, 2.0)

    def test_equal_min_max_constructs_successfully(self) -> None:
        f = MinMaxFilter(5.0, 5.0)
        assert f.density(5.0) == 1.0


# ---------------------------------------------------------------------------
# NormalFilter — density and domain
# ---------------------------------------------------------------------------

class TestNormalFilterDensity:
    def test_density_at_mean_is_1(self) -> None:
        f = NormalFilter(5.0, 2.0)
        assert f.density(5.0) == 1.0

    def test_density_at_one_std_dev_from_mean(self) -> None:
        f = NormalFilter(5.0, 2.0)
        # exp(-0.5 * 1^2) = exp(-0.5)
        assert f.density(7.0) == pytest.approx(math.exp(-0.5))
        assert f.density(3.0) == pytest.approx(math.exp(-0.5))

    def test_density_decreases_away_from_mean(self) -> None:
        f = NormalFilter(5.0, 2.0)
        assert f.density(5.0) > f.density(6.0) > f.density(8.0)

    def test_density_is_symmetric_around_mean(self) -> None:
        f = NormalFilter(5.0, 2.0)
        assert f.density(3.0) == pytest.approx(f.density(7.0))

    def test_sample_domain_is_5_std_devs(self) -> None:
        f = NormalFilter(5.0, 2.0)
        low, high = f.sample_domain()
        assert low == pytest.approx(5.0 - 5 * 2.0)
        assert high == pytest.approx(5.0 + 5 * 2.0)


# ---------------------------------------------------------------------------
# NormalFilter — validation
# ---------------------------------------------------------------------------

class TestNormalFilterValidation:
    def test_std_dev_zero_raises_value_error(self) -> None:
        with pytest.raises(ValueError):
            NormalFilter(5.0, 0.0)

    def test_std_dev_negative_raises_value_error(self) -> None:
        with pytest.raises(ValueError):
            NormalFilter(5.0, -1.0)

    def test_positive_std_dev_constructs_successfully(self) -> None:
        f = NormalFilter(0.0, 0.001)
        assert f.density(0.0) == 1.0


# ---------------------------------------------------------------------------
# VariationGenerator — should_vary
# ---------------------------------------------------------------------------

class TestVariationGeneratorShouldVary:
    def test_same_seed_and_name_returns_same_result(self) -> None:
        result_a = VariationGenerator(42).should_vary("prefix_delay", 0.5)
        result_b = VariationGenerator(42).should_vary("prefix_delay", 0.5)
        assert result_a == result_b

    def test_frequency_zero_always_returns_false(self) -> None:
        for seed in [0, 1, 42, 99, 12345]:
            assert VariationGenerator(seed).should_vary("x", 0.0) is False

    def test_frequency_one_always_returns_true(self) -> None:
        for seed in [0, 1, 42, 99, 12345]:
            assert VariationGenerator(seed).should_vary("x", 1.0) is True

    def test_different_seeds_can_give_different_results(self) -> None:
        # seed=0 returns True, seed=42 returns False for freq=0.5
        assert VariationGenerator(0).should_vary("prefix_delay", 0.5) is True
        assert VariationGenerator(42).should_vary("prefix_delay", 0.5) is False

    def test_probability_converges_over_many_seeds(self) -> None:
        frequency = 0.7
        n_seeds = 1000
        true_count = sum(
            1 for s in range(n_seeds)
            if VariationGenerator(s).should_vary("v", frequency)
        )
        ratio = true_count / n_seeds
        # Expect within 8% of the target frequency for 1000 samples
        assert abs(ratio - frequency) < 0.08

    def test_different_variable_names_are_independent(self) -> None:
        g = VariationGenerator(3)
        a = g.should_vary("prefix_delay_s", 0.5)
        b = g.should_vary("suffix_delay_s", 0.5)
        # Same seed, different variable names -> independent hashes -> different values
        assert a != b

    def test_frequency_below_zero_raises_value_error(self) -> None:
        with pytest.raises(ValueError):
            VariationGenerator(0).should_vary("x", -0.1)

    def test_frequency_above_one_raises_value_error(self) -> None:
        with pytest.raises(ValueError):
            VariationGenerator(0).should_vary("x", 1.1)


# ---------------------------------------------------------------------------
# VariationGenerator — generate (float)
# ---------------------------------------------------------------------------

class TestVariationGeneratorGenerate:
    def test_same_seed_and_name_returns_same_value(self) -> None:
        v1 = VariationGenerator(0).generate("speed", MinMaxFilter(0.0, 1.0))
        v2 = VariationGenerator(0).generate("speed", MinMaxFilter(0.0, 1.0))
        assert v1 == v2

    def test_different_seeds_produce_different_values(self) -> None:
        v0 = VariationGenerator(0).generate("speed", MinMaxFilter(0.0, 1.0))
        v42 = VariationGenerator(42).generate("speed", MinMaxFilter(0.0, 1.0))
        assert v0 != v42

    def test_minmax_filter_value_in_range(self) -> None:
        f = MinMaxFilter(2.0, 8.0)
        val = VariationGenerator(0).generate("speed", f)
        assert 2.0 <= val <= 8.0

    def test_normal_filter_value_in_domain(self) -> None:
        f = NormalFilter(5.0, 2.0)
        low, high = f.sample_domain()
        val = VariationGenerator(0).generate("noise_vol", f)
        assert low <= val <= high

    def test_normal_filter_exact_value_is_deterministic(self) -> None:
        # seed=0, "noise_vol", NormalFilter(5,2) -> 2.6291845902658038
        val = VariationGenerator(0).generate("noise_vol", NormalFilter(5.0, 2.0))
        assert val == pytest.approx(2.6291845902658038)

    def test_minmax_filter_exact_value_is_deterministic(self) -> None:
        # seed=0, "speed", MinMaxFilter(0,1) -> 0.1042678383550995
        val = VariationGenerator(0).generate("speed", MinMaxFilter(0.0, 1.0))
        assert val == pytest.approx(0.1042678383550995)

    def test_different_variable_names_produce_independent_values(self) -> None:
        g = VariationGenerator(99)
        va = g.generate("prefix_delay_s", MinMaxFilter(0.0, 1.0))
        vb = g.generate("suffix_delay_s", MinMaxFilter(0.0, 1.0))
        # Same seed, different variable names -> different hash keys -> independent
        assert va != vb

    def test_raises_value_error_after_1000_failed_attempts(self) -> None:
        with pytest.raises(ValueError):
            VariationGenerator(0).generate("x", _NeverAcceptFilter())


# ---------------------------------------------------------------------------
# VariationGenerator — generate_int
# ---------------------------------------------------------------------------

class TestVariationGeneratorGenerateInt:
    def test_same_seed_and_name_returns_same_value(self) -> None:
        v1 = VariationGenerator(0).generate_int("speed", MinMaxFilter(0, 10))
        v2 = VariationGenerator(0).generate_int("speed", MinMaxFilter(0, 10))
        assert v1 == v2

    def test_different_seeds_can_produce_different_values(self) -> None:
        v0 = VariationGenerator(0).generate_int("speed", MinMaxFilter(0, 10))
        v42 = VariationGenerator(42).generate_int("speed", MinMaxFilter(0, 10))
        assert v0 != v42

    def test_value_in_range(self) -> None:
        for seed in range(20):
            val = VariationGenerator(seed).generate_int("x", MinMaxFilter(0, 10))
            assert 0 <= val <= 10

    def test_returns_int_type(self) -> None:
        val = VariationGenerator(0).generate_int("speed", MinMaxFilter(0, 10))
        assert isinstance(val, int)

    def test_range_zero_returns_min_val_immediately(self) -> None:
        # Special case: min_val == max_val -> return min_val without looping
        assert VariationGenerator(0).generate_int("x", MinMaxFilter(5, 5)) == 5
        assert VariationGenerator(42).generate_int("y", MinMaxFilter(0, 0)) == 0

    def test_exact_value_seed_0(self) -> None:
        # seed=0, "speed", MinMaxFilter(0,10) -> 0
        assert VariationGenerator(0).generate_int("speed", MinMaxFilter(0, 10)) == 0

    def test_exact_value_seed_42(self) -> None:
        # seed=42, "speed", MinMaxFilter(0,10) -> 2
        assert VariationGenerator(42).generate_int("speed", MinMaxFilter(0, 10)) == 2

    def test_different_variable_names_are_independent(self) -> None:
        g = VariationGenerator(0)
        v1 = g.generate_int("speech_rate", MinMaxFilter(-20, 20))
        v2 = g.generate_int("pitch", MinMaxFilter(-20, 20))
        assert v1 != v2


# ---------------------------------------------------------------------------
# VariationGenerator — generate_int stability across range changes
#
# Seeds computed offline to exhibit specific bitmask-rejection behaviors:
#   seed=1: generate_int("x", MinMaxFilter(0,10)) == 2
#           generate_int("x", MinMaxFilter(0,20)) == 2  (stable: same value)
#           generate_int("x", MinMaxFilter(0,5))  == 2  (stable: value is in lower range)
#   seed=2: generate_int("x", MinMaxFilter(0,10)) == 3
#           generate_int("x", MinMaxFilter(0,20)) == 11 (changes: bitmask bit-4 set, 3+16=19>10)
#   seed=5: generate_int("x", MinMaxFilter(0,10)) == 7
#           generate_int("x", MinMaxFilter(0,5))  == 1  (changes: 7>5 rejected, resampled)
# ---------------------------------------------------------------------------

class TestVariationGeneratorGenerateIntStability:
    def test_stable_seed_same_value_when_max_widened(self) -> None:
        # seed=1: value 2 is below 10 and its raw bits have bit-4 == 0, so mask=31 gives
        # the same 2 as mask=15.
        v_narrow = VariationGenerator(1).generate_int("x", MinMaxFilter(0, 10))
        v_wide = VariationGenerator(1).generate_int("x", MinMaxFilter(0, 20))
        assert v_narrow == 2
        assert v_wide == 2

    def test_changing_seed_gets_higher_when_max_widened(self) -> None:
        # seed=2: raw_0 & 31 == 11 (accepted at n=0 for max=20), but for max=10 the value
        # 11 > 10 is rejected; the narrow case loops to n=2 where raw_2 & 15 == 3 (<= 10).
        # The two ranges draw from different attempt indices, so the values differ.
        v_narrow = VariationGenerator(2).generate_int("x", MinMaxFilter(0, 10))
        v_wide = VariationGenerator(2).generate_int("x", MinMaxFilter(0, 20))
        assert v_narrow == 3
        assert v_wide == 11

    def test_stable_seed_same_value_when_max_narrowed(self) -> None:
        # seed=1: value 2 is within the narrowed range [0,5], so it stays the same.
        v_original = VariationGenerator(1).generate_int("x", MinMaxFilter(0, 10))
        v_narrow = VariationGenerator(1).generate_int("x", MinMaxFilter(0, 5))
        assert v_original == 2
        assert v_narrow == 2

    def test_changing_seed_when_max_narrowed(self) -> None:
        # seed=5: raw_0 & 15 == 7 (accepted for max=10), but 7 > 5 so rejected for max=5;
        # resampling produces a different value.
        v_original = VariationGenerator(5).generate_int("x", MinMaxFilter(0, 10))
        v_narrow = VariationGenerator(5).generate_int("x", MinMaxFilter(0, 5))
        assert v_original == 7
        assert v_narrow == 1


# ---------------------------------------------------------------------------
# VariationGenerator — choose
# ---------------------------------------------------------------------------

class TestVariationGeneratorChoose:
    def test_same_seed_and_name_returns_same_item(self) -> None:
        options = ["a", "b", "c", "d"]
        c1 = VariationGenerator(0).choose("voice", options)
        c2 = VariationGenerator(0).choose("voice", options)
        assert c1 == c2

    def test_result_is_one_of_the_options(self) -> None:
        options = ["en-US-JennyNeural", "en-US-GuyNeural", "en-GB-LibbyNeural"]
        result = VariationGenerator(0).choose("voice", options)
        assert result in options

    def test_uses_index_zero_hash_key(self) -> None:
        # choose must use sha256("{seed}:{variable_name}:0") with index 0, no loop.
        # Verified by exact match against the expected hash formula.
        import hashlib
        options = ["a", "b", "c", "d"]
        seed = 0
        raw = int.from_bytes(
            hashlib.sha256(f"{seed}:voice:0".encode()).digest()[:8], "big"
        )
        expected = options[raw % len(options)]
        assert VariationGenerator(seed).choose("voice", options) == expected

    def test_different_seeds_can_choose_different_items(self) -> None:
        options = ["en-US-JennyNeural", "en-US-GuyNeural", "en-GB-LibbyNeural"]
        # seed=0 -> JennyNeural, seed=1 -> GuyNeural
        assert VariationGenerator(0).choose("voice", options) == "en-US-JennyNeural"
        assert VariationGenerator(1).choose("voice", options) == "en-US-GuyNeural"

    def test_different_variable_names_select_independently(self) -> None:
        options = ["x", "y", "z"]
        g = VariationGenerator(0)
        # Different keys -> independent hash values -> different selections
        a = g.choose("noise_file", options)
        b = g.choose("voice", options)
        assert a != b

    def test_single_option_list_always_returns_it(self) -> None:
        only = ["only-choice"]
        for seed in [0, 1, 42, 999]:
            assert VariationGenerator(seed).choose("x", only) == "only-choice"

    def test_empty_options_raises_value_error(self) -> None:
        with pytest.raises(ValueError):
            VariationGenerator(0).choose("x", [])
