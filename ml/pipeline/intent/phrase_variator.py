from __future__ import annotations

import hashlib
import random
import re
from typing import Callable, Sequence

from pipeline.core.randomization import MinMaxFilter, VariationGenerator
from pipeline.core.sample import TextSample
from pipeline.stages.params import GeneratePhraseParams

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

    Uses VariationGenerator (via vgen_factory) for all randomisation decisions,
    giving stable reproducible output independent of parameter changes.
    """

    def __init__(
        self,
        vgen_factory: Callable[[int], VariationGenerator],
        params: GeneratePhraseParams,
    ) -> None:
        self._vgen_factory = vgen_factory
        self._repeat_modifier_chance = params.repeat_modifier_chance
        self._pleasantry_chance = params.pleasantry_chance
        self._hesitation_chance = params.hesitation_chance
        self._spelling_variant_chance = params.spelling_variant_chance
        self._case_variant_chance = params.case_variant_chance

    def generate(
        self,
        base_phrases: Sequence[tuple[str, str]],
        variations_per_phrase: int,
    ) -> list[TextSample]:
        """Generate up to variations_per_phrase variants per base phrase.

        base_phrases: sequence of (phrase, command) tuples from the input CSV.
        Each valid variant becomes a TextSample with content=surface_form, label=command.

        Seeding scheme: use random.Random(42) to draw one phrase_seed per base phrase
        (one .randint call per phrase in iteration order). Then use
        random.Random(phrase_seed) to draw one variant_seed per variant of that phrase.
        Pass each variant_seed to vgen_factory for that variant's _create_variation() call.
        """
        phrase_rng = random.Random(42)
        results: list[TextSample] = []
        for phrase, command in base_phrases:
            phrase_seed = phrase_rng.randint(0, 2**31 - 1)
            variant_rng = random.Random(phrase_seed)
            for _ in range(variations_per_phrase):
                variant_seed = variant_rng.randint(0, 2**31 - 1)
                vgen = self._vgen_factory(variant_seed)
                variation = self._create_variation(phrase, vgen)
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

    def _create_variation(self, phrase: str, vgen: VariationGenerator) -> dict:
        """Create a single variation of a phrase using vgen for all random decisions."""
        transformations: list[str] = []
        result = phrase
        repeat_count_used = 1

        # Add repeat modifier (for data variety only)
        repeat_modifier = ""
        if vgen.should_vary("repeat_modifier_apply", self._repeat_modifier_chance):
            repeat_count_used = vgen.choose("repeat_count", list(_REPEAT_MODIFIERS.keys()))
            modifiers = _REPEAT_MODIFIERS.get(repeat_count_used, [])
            if modifiers:
                repeat_modifier = vgen.choose("repeat_modifier_text", modifiers)
            else:
                repeat_modifier = ""
                repeat_count_used = 1
        if repeat_modifier:
            result = f"{result} {repeat_modifier}"
            transformations.append(f"repeat_modifier:{repeat_count_used}")

        # Add pleasantry
        if vgen.should_vary("pleasantry_apply", self._pleasantry_chance):
            pleasantry = vgen.choose("pleasantry_text", _PLEASANTRIES)
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
                    if vgen.should_vary("pleasantry_side", 0.5):
                        result = f"{result}, {pleasantry}"
                    else:
                        result = f"{pleasantry}, {result}"
                    transformations.append(f"pleasantry:{pleasantry}")

        # Save the speech to detect at this point
        speech_to_detect = self._normalize_commas_and_whitespace(result)

        # Add hesitation
        if vgen.should_vary("hesitation_apply", self._hesitation_chance):
            hesitation = vgen.choose("hesitation_text", [h for h in _HESITATIONS if h])
            if hesitation:
                words = result.split()
                if len(words) > 1:
                    pos = vgen.generate_int("hesitation_pos", MinMaxFilter(0, len(words)))
                    words.insert(pos, hesitation)
                    result = " ".join(words)
                    transformations.append(f"hesitation:{hesitation}")

        # Apply spelling variations
        if vgen.should_vary("spelling_apply", self._spelling_variant_chance):
            for word, variants in _SPELLING_VARIANTS.items():
                lower_result = result.lower()
                if word in lower_result and len(variants) > 1:
                    variant = vgen.choose(f"spelling_variant_{word}", [v for v in variants if v != word])
                    result_lower = lower_result.replace(word, variant, 1)
                    if result.isupper():
                        result = result_lower.upper()
                    elif result.istitle():
                        result = result_lower.title()
                    else:
                        result = result_lower
                    transformations.append(f"spelling_variant:{word}->{variant}")

        # Random case variations
        if vgen.should_vary("case_apply", self._case_variant_chance):
            case_transform = vgen.choose("case_transform", ["lower", "upper", "title", "original"])
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
