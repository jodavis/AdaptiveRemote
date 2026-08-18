"""Seed-based, precision-quantized randomisation primitives used by every
`RandomizedModifierStage` subclass to derive reproducible per-sample variation values.

`PassFilter`/`MinMaxFilter`/`NormalFilter` implement "Precision-quantized rejection sampling"
(`ml/_spec_OopPipeline.md`): each takes a `precision: int = 0` constructor parameter (decimal
places) and computes, at construction, a quantization grid over `[min_val, max_val]` -- a
`scale` (`10**precision`), shifted/biased integer bounds (`_min_scaled`/`_max_scaled`), and a
power-of-2 range (`_pow2_range`) sized to cover that grid. Drawing candidates from this finite
grid (rather than continuous float interpolation) keeps most values stable across small
constraint changes, which is what makes skip-unchanged detection actually work.

`VariationGenerator` implements "Seed-based randomisation with pass filters": every method's
hash input is `f"{seed}:{name}:..."`, keyed by the caller's own variable `name` (and, for
`generate`/`generate_int`, a per-attempt counter). No state is shared across variables or across
calls, so each variable's derived value is independent of what other variables were requested,
or in what order -- the independence property called out in the spec as easy to break by
accident.
"""

from __future__ import annotations

import hashlib
import math
from typing import Sequence, TypeVar

_MAX_ATTEMPTS = 1000
_UINT64_RANGE = 2**64

T = TypeVar("T")


class PassFilter:
    """Uniform-domain pass filter: computes the precision-quantized grid over
    `[min_val, max_val]` and accepts every value on that grid unconditionally
    (`density()` is always `1.0`).

    Subclasses that need a non-uniform acceptance shape (see `NormalFilter`) override
    `density()`; the quantization grid itself (`quantize()`) is shared, unmodified, by every
    subclass.
    """

    def __init__(self, min_val: float, max_val: float, precision: int = 0) -> None:
        if min_val > max_val:
            raise ValueError(f"min_val ({min_val}) must not exceed max_val ({max_val})")

        self.min_val = min_val
        self.max_val = max_val
        self.precision = precision
        self.scale: int = 10**precision
        self._min_scaled: int = round(min_val * self.scale)
        self._max_scaled: int = round(max_val * self.scale)
        self._grid_size: int = self._max_scaled - self._min_scaled + 1
        self._pow2_range: int = 1 << (self._grid_size - 1).bit_length()

    def quantize(self, raw: int) -> float | None:
        """Map a raw integer draw onto this filter's quantized grid, or `None` if `raw` fell
        outside the grid (the caller should draw a new `raw` and retry)."""
        grid_index = raw % self._pow2_range
        if grid_index >= self._grid_size:
            return None
        return (self._min_scaled + grid_index) / self.scale

    def density(self, value: float) -> float:
        return 1.0


class MinMaxFilter(PassFilter):
    """Semantically-named alias for `PassFilter`'s uniform behavior -- constrains a variable
    strictly to `[min_val, max_val]` with no additional shaping."""


class NormalFilter(PassFilter):
    """Truncated-Gaussian pass filter: the same quantized grid as `PassFilter`, but weights
    acceptance by a Gaussian density centered on `mean` with standard deviation `std`. Peak
    density is normalized to `1.0` at `mean`, so `density()` doubles as the rejection-sampling
    acceptance probability `VariationGenerator.generate()` compares against a hash-derived
    uniform draw.
    """

    def __init__(
        self, mean: float, std: float, min_val: float, max_val: float, precision: int = 0
    ) -> None:
        super().__init__(min_val, max_val, precision)
        if std <= 0:
            raise ValueError(f"std ({std}) must be positive")

        self.mean = mean
        self.std = std

    def density(self, value: float) -> float:
        return math.exp(-0.5 * ((value - self.mean) / self.std) ** 2)


class VariationGenerator:
    """Deterministic, seed-derived random-value generator for `RandomizedModifierStage`
    subclasses."""

    def __init__(self, seed: int) -> None:
        self._seed = seed

    def should_vary(self, name: str, frequency: float) -> bool:
        raw = self._digest_uint64(f"{self._seed}:{name}:vary")
        return (raw / _UINT64_RANGE) < frequency

    def generate(self, name: str, pass_filter: PassFilter) -> float:
        for attempt in range(_MAX_ATTEMPTS):
            digest = hashlib.sha256(f"{self._seed}:{name}:{attempt}".encode("utf-8")).digest()
            raw_candidate = int.from_bytes(digest[:8], "big")
            raw_accept = int.from_bytes(digest[8:16], "big")

            value = pass_filter.quantize(raw_candidate)
            if value is None:
                continue

            acceptance_draw = raw_accept / _UINT64_RANGE
            if acceptance_draw < pass_filter.density(value):
                return value

        raise ValueError(
            f"Could not generate a value for {name!r} after {_MAX_ATTEMPTS} attempts"
        )

    def generate_int(self, name: str, pass_filter: PassFilter) -> int:
        min_int = int(pass_filter.min_val)
        max_int = int(pass_filter.max_val)
        value_range = max_int - min_int
        if value_range == 0:
            return min_int

        mask = (1 << value_range.bit_length()) - 1
        for attempt in range(_MAX_ATTEMPTS):
            raw = self._digest_uint64(f"{self._seed}:{name}:{attempt}")
            candidate = raw & mask
            if candidate <= value_range:
                return min_int + candidate

        raise ValueError(
            f"Could not generate an int for {name!r} after {_MAX_ATTEMPTS} attempts"
        )

    def choose(self, name: str, options: Sequence[T]) -> T:
        if not options:
            raise ValueError("Cannot choose from an empty options sequence")

        raw = self._digest_uint64(f"{self._seed}:{name}:0")
        return options[raw % len(options)]

    def _digest_uint64(self, message: str) -> int:
        digest = hashlib.sha256(message.encode("utf-8")).digest()
        return int.from_bytes(digest[:8], "big")
