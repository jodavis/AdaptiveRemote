from __future__ import annotations

import hashlib
import random
import re
from typing import Sequence

from pipeline.core.sample import TextSample

# ---------------------------------------------------------------------------
# Variation components (ported from ml/scripts/intent_prediction/01_generate_phrases.py)
# ---------------------------------------------------------------------------

_REPEAT_MODIFIERS: dict[int, list[str]] = {
    1: ["", "once", "one", "one time", "one more time", "another one", "another time", "again"],
    2: ["twice", "two", "two times", "two more times", "another two", "another two times"],
    3: ["three", "three times", "three more times", "another three", "another three times"],
    4: ["four", "four times", "four more times", "another four", "another four times"],
    5: ["five", "five times", "five more times", "another five", "another five times"],
    6: ["six", "six times", "six more times", "another six", "another six times"],
    7: ["seven", "seven times", "seven more times", "another seven", "another seven times"],
    8: ["eight", "eight times", "eight more times", "another eight", "another eight times"],
    9: ["nine", "nine times", "nine more times", "another nine", "another nine times"],
}

_REPEAT_MODIFIER_CHANCE = 0.25
_PLEASANTRY_CHANCE = 0.3
_HESITATION_CHANCE = 0.3
_SPELLING_VARIANT_CHANCE = 0.3
_CASE_VARIANT_CHANCE = 0.3

_PLEASANTRIES = [
    "",
    "please ",
    ", please ",
    "please, ",
    ", please, ",
    "could you ",
    "can you ",
    "would you ",
    ", thank you ",
    ", thanks ",
]

_HESITATIONS = [
    "",
    ", um, ",
    ", uh, ",
    ", umm, ",
    ", err, ",
    ", hmm, ",
    " ... ",
]

_SPELLING_VARIANTS: dict[str, list[str]] = {
    "one": ["one", "won", "1"],
    "to": ["to", "too", "two", "2"],
    "two": ["two", "to", "too", "2"],
    "three": ["three", "3"],
    "for": ["for", "four", "4"],
    "four": ["four", "for", "4"],
    "five": ["five", "5"],
    "six": ["six", "6"],
    "seven": ["seven", "7"],
    "eight": ["eight", "ate", "8"],
    "nine": ["nine", "9"],
    "right": ["right", "rite", "write", "wright"],
    "OK": ["OK", "okay", "ok"],
    "pause": ["pause", "paws"],
}


class PhraseVariator:
    """Generates surface-form variants of command phrases.

    Ports _create_variation() and sanity_check() from the original
    ml/scripts/intent_prediction/01_generate_phrases.py VariationGenerator class.
    Every random.* call is replaced by self.rng.* — logic is otherwise identical.
    """

    def __init__(self, rng: random.Random) -> None:
        self._rng = rng

    def generate(
        self,
        base_phrases: Sequence[tuple[str, str]],
        variations_per_phrase: int,
    ) -> list[TextSample]:
        """Generate up to variations_per_phrase variants per base phrase.

        base_phrases: sequence of (phrase, command) tuples from the input CSV.
        Each valid variant becomes a TextSample with content=surface_form, label=command.
        """
        results: list[TextSample] = []
        for phrase, command in base_phrases:
            for _ in range(variations_per_phrase):
                variation = self._create_variation(phrase)
                variation["canonical_label"] = command
                valid, _ = self.sanity_check([variation])
                if valid:
                    v = valid[0]
                    content = v["surface_form"]
                    content_hash = hashlib.sha256(content.encode("utf-8")).hexdigest()
                    results.append(
                        TextSample(
                            seed=0,
                            content_hash=content_hash,
                            content=content,
                            label=command,
                        )
                    )
        return results

    # ------------------------------------------------------------------
    # Ported from VariationGenerator._create_variation()
    # ------------------------------------------------------------------

    def _create_variation(self, phrase: str) -> dict:
        """Create a single variation of a phrase (ported from original script)."""
        transformations: list[str] = []
        result = phrase
        repeat_count_used = 1

        # Add repeat modifier (for data variety only)
        repeat_modifier = ""
        if self._rng.random() < _REPEAT_MODIFIER_CHANCE:
            repeat_count_used = self._rng.choice(list(_REPEAT_MODIFIERS.keys()))
            modifiers = _REPEAT_MODIFIERS.get(repeat_count_used, [])
            if modifiers:
                repeat_modifier = self._rng.choice(modifiers)
            else:
                repeat_modifier = ""
                repeat_count_used = 1
        if repeat_modifier:
            result = f"{result} {repeat_modifier}"
            transformations.append(f"repeat_modifier:{repeat_count_used}")

        # Add pleasantry
        if self._rng.random() < _PLEASANTRY_CHANCE:
            pleasantry = self._rng.choice(_PLEASANTRIES)
            if pleasantry:
                if (
                    pleasantry.startswith("could you")
                    or pleasantry.startswith("can you")
                    or pleasantry.startswith("would you")
                ):
                    result = f"{pleasantry} {result}"
                    transformations.append(f"prefix_pleasantry:{pleasantry}")
                elif pleasantry.startswith(","):
                    result = f"{result}{pleasantry}"
                    transformations.append(f"suffix_pleasantry:{pleasantry}")
                else:
                    if self._rng.random() > 0.5:
                        result = f"{result}, {pleasantry}"
                    else:
                        result = f"{pleasantry}, {result}"
                    transformations.append(f"pleasantry:{pleasantry}")

        # Save the speech to detect at this point
        speech_to_detect = self._normalize_commas_and_whitespace(result)

        # Add hesitation
        if self._rng.random() < _HESITATION_CHANCE:
            hesitation = self._rng.choice([h for h in _HESITATIONS if h])
            if hesitation:
                words = result.split()
                if len(words) > 1:
                    pos = self._rng.randint(0, len(words))
                    words.insert(pos, hesitation)
                    result = " ".join(words)
                    transformations.append(f"hesitation:{hesitation}")

        # Apply spelling variations
        if self._rng.random() < _SPELLING_VARIANT_CHANCE:
            for word, variants in _SPELLING_VARIANTS.items():
                lower_result = result.lower()
                if word in lower_result and len(variants) > 1:
                    variant = self._rng.choice([v for v in variants if v != word])
                    result_lower = lower_result.replace(word, variant, 1)
                    if result.isupper():
                        result = result_lower.upper()
                    elif result.istitle():
                        result = result_lower.title()
                    else:
                        result = result_lower
                    transformations.append(f"spelling_variant:{word}->{variant}")

        # Random case variations
        if self._rng.random() < _CASE_VARIANT_CHANCE:
            case_transform = self._rng.choice(["lower", "upper", "title", "original"])
            if case_transform == "lower":
                result = result.lower()
                transformations.append("lowercase")
            elif case_transform == "upper":
                result = result.upper()
                transformations.append("uppercase")
            elif case_transform == "title":
                result = result.title()
                transformations.append("titlecase")

        result = self._normalize_commas_and_whitespace(result)

        return {
            "base_phrase": phrase,
            "surface_form": result,
            "speech_to_detect": speech_to_detect,
            "transformations": "|".join(transformations) if transformations else "none",
            "repeat_count": repeat_count_used,
        }

    # ------------------------------------------------------------------
    # Ported from VariationGenerator.sanity_check()
    # ------------------------------------------------------------------

    def sanity_check(
        self, variations: list[dict]
    ) -> tuple[list[dict], list[str]]:
        """Filter variations that fail sanity checks (ported from original script)."""
        valid: list[dict] = []
        issues: list[str] = []

        for var in variations:
            surface = var["surface_form"]
            canonical = var.get("canonical_label", "")

            # Check 1: Not empty
            if not surface or not surface.strip():
                issues.append(f"Empty surface form for {canonical}")
                continue

            # Check 2: Reasonable length (2-150 characters)
            if len(surface) < 2 or len(surface) > 150:
                issues.append(f"Unusual length ({len(surface)}) for: {surface}")
                continue

            # Check 3: Contains at least one letter
            if not any(c.isalpha() for c in surface):
                issues.append(f"No letters in: {surface}")
                continue

            # Check 4: Base phrase recognizable via token overlap
            base_tokens_list = self._tokenize(var["base_phrase"])
            surface_token_set = set(self._tokenize(surface))

            if not base_tokens_list:
                base_tokens_list = self._tokenize(canonical)

            augmented_base_tokens = set(base_tokens_list)

            transformations_str = var.get("transformations", "")
            if transformations_str and transformations_str != "none":
                for t in transformations_str.split("|"):
                    if t.startswith("spelling_variant:"):
                        try:
                            mapping = t.split(":", 1)[1]
                            if "->" in mapping:
                                base_word, variant_word = mapping.split("->", 1)
                                augmented_base_tokens.update(self._tokenize(base_word))
                                augmented_base_tokens.update(self._tokenize(variant_word))
                        except ValueError:
                            pass

            if augmented_base_tokens and surface_token_set:
                if augmented_base_tokens.isdisjoint(surface_token_set):
                    issues.append(
                        f"Base phrase '{var['base_phrase']}' not recognizable in '{surface}'"
                    )
                    continue

            valid.append(var)

        return valid, issues

    # ------------------------------------------------------------------
    # Helpers (ported from original script)
    # ------------------------------------------------------------------

    def _normalize_commas_and_whitespace(self, text: str) -> str:
        text = re.sub(r"(?:\s*,\s*){2,}", ",", text)
        text = re.sub(r"^\s*,\s*|\s*,\s*$", "", text).strip()
        text = re.sub(r"\s+", " ", text).strip()
        text = re.sub(r"\s,", ",", text).strip()
        return text

    def _tokenize(self, text: str) -> list[str]:
        return re.findall(r"[a-z0-9']+", text.lower())
