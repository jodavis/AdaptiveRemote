from __future__ import annotations

import hashlib
import math
from abc import ABC, abstractmethod
from typing import Sequence, TypeVar

T = TypeVar("T")

_TWO_TO_64 = 2**64
_MAX_ATTEMPTS = 1000


def _hash_int(key: str) -> int:
    return int.from_bytes(hashlib.sha256(key.encode()).digest()[:8], "big")


class PassFilter(ABC):
    @abstractmethod
    def density(self, value: float) -> float:
        """Normalised density; max == 1.0. Acceptance probability in rejection sampling."""
        ...

    @abstractmethod
    def sample_domain(self) -> tuple[float, float]:
        """(low, high): range for uniform candidate generation."""
        ...


class MinMaxFilter(PassFilter):
    """Uniform over [min_val, max_val]. density() == 1.0 in range, 0.0 outside."""

    def __init__(self, min_val: float, max_val: float) -> None:
        if min_val > max_val:
            raise ValueError(
                f"min_val must be <= max_val, got min_val={min_val}, max_val={max_val}"
            )
        self._min_val = min_val
        self._max_val = max_val

    def density(self, value: float) -> float:
        return 1.0 if self._min_val <= value <= self._max_val else 0.0

    def sample_domain(self) -> tuple[float, float]:
        return (self._min_val, self._max_val)


class NormalFilter(PassFilter):
    """Gaussian. density(x) = gaussian_pdf(x)/gaussian_pdf(mean); peak == 1.0."""

    def __init__(self, mean: float, std_dev: float) -> None:
        if std_dev <= 0:
            raise ValueError(f"std_dev must be positive, got {std_dev}")
        self._mean = mean
        self._std_dev = std_dev

    def density(self, value: float) -> float:
        z = (value - self._mean) / self._std_dev
        return math.exp(-0.5 * z * z)

    def sample_domain(self) -> tuple[float, float]:
        return (self._mean - 5 * self._std_dev, self._mean + 5 * self._std_dev)


class VariationGenerator:
    def __init__(self, sample_seed: int) -> None:
        self._seed = sample_seed

    def should_vary(self, variable_name: str, frequency: float) -> bool:
        """True with probability frequency using sha256-based deterministic hash."""
        if not 0.0 <= frequency <= 1.0:
            raise ValueError(f"frequency must be in [0.0, 1.0], got {frequency}")
        raw = _hash_int(f"{self._seed}:{variable_name}:vary")
        return (raw / _TWO_TO_64) < frequency

    def generate(self, variable_name: str, pass_filter: PassFilter) -> float:
        """Rejection-sample deterministically using attempt-indexed sha256 hashes."""
        domain_low, domain_high = pass_filter.sample_domain()
        domain_width = domain_high - domain_low
        for n in range(_MAX_ATTEMPTS):
            raw = _hash_int(f"{self._seed}:{variable_name}:{n}")
            candidate = domain_low + (raw / _TWO_TO_64) * domain_width
            accept_raw = _hash_int(f"{self._seed}:{variable_name}:{n}:accept")
            if (accept_raw / _TWO_TO_64) < pass_filter.density(candidate):
                return candidate
        raise ValueError(
            f"generate() failed to find an accepted candidate after {_MAX_ATTEMPTS} "
            f"attempts for variable '{variable_name}'"
        )

    def generate_int(self, variable_name: str, pass_filter: MinMaxFilter) -> int:
        """Integer in [int(min_val), int(max_val)] inclusive using bitmask rejection."""
        domain_low, domain_high = pass_filter.sample_domain()
        min_val = int(domain_low)
        max_val = int(domain_high)
        range_ = max_val - min_val
        if range_ == 0:
            return min_val
        mask = (1 << math.ceil(math.log2(range_ + 1))) - 1
        for n in range(_MAX_ATTEMPTS):
            raw = _hash_int(f"{self._seed}:{variable_name}:{n}")
            candidate = min_val + (raw & mask)
            if candidate <= max_val:
                return candidate
        raise ValueError(
            f"generate_int() failed to find an accepted candidate after {_MAX_ATTEMPTS} "
            f"attempts for variable '{variable_name}'"
        )

    def choose(self, variable_name: str, options: Sequence[T]) -> T:
        """Direct selection via sha256 hash modulo len(options); no rejection loop."""
        if not options:
            raise ValueError("options must be non-empty")
        raw = _hash_int(f"{self._seed}:{variable_name}:0")
        return options[raw % len(options)]
