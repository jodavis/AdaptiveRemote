#!/usr/bin/env python3
"""
Training Data Generator for LLM Intent Classification

This script generates training data variations from the 01_input_phrases.csv file
for fine-tuning an LLM to handle remote control commands as a fallback.
"""

import xml.etree.ElementTree as ET
import csv
import random
import re
from pathlib import Path
from typing import List, Dict, Tuple


# =====================
# CONFIGURATION SETTINGS
# =====================
SCRIPT_DIR = Path(__file__).parent
DATA_FOLDER = SCRIPT_DIR / "../../data/intent_prediction/01_generate_phrases/"
INPUT_FILE = SCRIPT_DIR / "01_input_phrases.csv"
OUTPUT_FILE = DATA_FOLDER / "training_data.csv"

# Number of total samples to generate
TARGET_SAMPLES = 10000

# Probability settings for variations
REPEAT_MODIFIER_CHANCE = 0.25
PLEASANTRY_CHANCE = 0.3
HESITATION_CHANCE = 0.3
SPELLING_VARIANT_CHANCE = 0.3
CASE_VARIANT_CHANCE = 0.3

# =====================

# Variation components
REPEAT_MODIFIERS = {
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

PLEASANTRIES = [
    "",
    "please ",
    ", please ",
    "please, ",
    ", please, ",
    "could you ",
    "can you ",
    "would you ",
    ", thank you ",
    ", thanks "
]

HESITATIONS = [
    "",
    ", um, ",
    ", uh, ",
    ", umm, ",
    ", err, ",
    ", hmm, ",
    " ... ",
]

# Homophone/spelling variations
SPELLING_VARIANTS = {
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
    "pause": ["pause", "paws"]
}

class VariationGenerator:
    """Generates variations of command phrases."""

    def __init__(self, target_samples: int):
        self.target_samples = target_samples
        (self.generated, self.existing_variations) = self.load_existing_variations(OUTPUT_FILE)

    def load_existing_variations(self, filepath: Path) -> (set | List[Dict[str, str]]):
        """Load existing variations from a CSV file to avoid duplicates."""
        variations = []
        generated_keys = set()
        if not filepath.exists():
            return generated_keys, variations
        with open(filepath, newline='', encoding='utf-8') as csvfile:
            reader = csv.DictReader(csvfile)
            for row in reader:
                variations.append(row)
                key = self._create_key(row)
                generated_keys.add(key)
        return (generated_keys, variations)
        
    def generate_variations(self, commands: List[Dict[str, str]]) -> List[Dict[str, str]]:
        """Generate variations for all commands."""
        variations = self.existing_variations
        samples_per_phrase = max(1, self.target_samples // len(commands))
        
        for cmd in commands:
            existing_for_phrase = len([v for v in variations if v['base_phrase'] == cmd['phrase']])
            cmd_variations = self._generate_for_command(
                cmd['phrase'], 
                cmd['canonical'],
                samples_per_phrase - existing_for_phrase
            )
            variations.extend(cmd_variations)
        
        # If we haven't reached target, add more variations
        while len(variations) < self.target_samples and len(commands) > 0:
            cmd = random.choice(commands)
            extra = self._generate_for_command(
                cmd['phrase'],
                cmd['canonical'],
                1
            )
            variations.extend(extra)
        
        return variations[:self.target_samples]
    
    def _generate_for_command(self, phrase: str, canonical: str, count: int) -> List[Dict[str, str]]:
        """Generate variations for a single command."""
        variations = []
        attempts = 0
        max_attempts = count * 10  # Prevent infinite loops
        
        while len(variations) < count and attempts < max_attempts:
            attempts += 1
            
            # Generate a variation
            variation = self._create_variation(phrase)
            
            # Check for duplicates
            variant_key = self._create_key(variation)
            if variant_key not in self.generated:
                self.generated.add(variant_key)
                variation['canonical_label'] = canonical
                variations.append(variation)
        
        return variations
    
    def _create_key(self, row: Dict[str, str]) -> str:
        """Create a unique key for a variation based on base phrase and canonical label."""
        return row['base_phrase'].lower() + "|" + row['transformations']

    def _create_variation(self, phrase: str) -> Dict[str, str]:
        """Create a single variation of a phrase."""
        transformations = []
        result = phrase
        repeat_count_used = 1
                
        # Add repeat modifier (for data variety only)
        repeat_modifier = ""
        if random.random() < REPEAT_MODIFIER_CHANCE:
            repeat_count_used = random.choice(list(REPEAT_MODIFIERS.keys()))
            modifiers = REPEAT_MODIFIERS.get(repeat_count_used, [])
            if modifiers:
                repeat_modifier = random.choice(modifiers)
            else:
                repeat_modifier = ""
                repeat_count_used = 1
        if repeat_modifier:
            result = f"{result} {repeat_modifier}"
            transformations.append(f"repeat_modifier:{repeat_count_used}")

        # Add pleasantry
        if random.random() < PLEASANTRY_CHANCE:
            pleasantry = random.choice(PLEASANTRIES)
            if pleasantry:
                if pleasantry.startswith("could you") or pleasantry.startswith("can you") or pleasantry.startswith("would you"):
                    result = f"{pleasantry} {result}"
                    transformations.append(f"prefix_pleasantry:{pleasantry}")
                elif pleasantry.startswith(","):
                    result = f"{result}{pleasantry}"
                    transformations.append(f"suffix_pleasantry:{pleasantry}")
                else:
                    if random.random() > 0.5:
                        result = f"{result}, {pleasantry}"
                    else:
                        result = f"{pleasantry}, {result}"
                    transformations.append(f"pleasantry:{pleasantry}")

        # Save the speech to detect at this point. This is what the STT system should recognize.
        speech_to_detect = self._normalize_commas_and_whitespace(result)

        # Add hesitation
        if random.random() < HESITATION_CHANCE:
            hesitation = random.choice([h for h in HESITATIONS if h])
            if hesitation:
                # Insert hesitation at random position
                words = result.split()
                if len(words) > 1:
                    pos = random.randint(0, len(words))
                    words.insert(pos, hesitation)
                    result = " ".join(words)
                    transformations.append(f"hesitation:{hesitation}")

        # Apply spelling variations
        if random.random() < SPELLING_VARIANT_CHANCE:
            for word, variants in SPELLING_VARIANTS.items():
                lower_result = result.lower()
                if word in lower_result and len(variants) > 1:
                    variant = random.choice([v for v in variants if v != word])
                    result_lower = lower_result.replace(word, variant, 1)
                    # Match original casing roughly
                    if result.isupper():
                        result = result_lower.upper()
                    elif result.istitle():
                        result = result_lower.title()
                    else:
                        result = result_lower
                    transformations.append(f"spelling_variant:{word}->{variant}")
        
        # Random case variations
        if random.random() < CASE_VARIANT_CHANCE:
            case_transform = random.choice(["lower", "upper", "title", "original"])
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
            'base_phrase': phrase,
            'surface_form': result,
            'speech_to_detect': speech_to_detect,
            'transformations': "|".join(transformations) if transformations else "none",
            'repeat_count': repeat_count_used
        }

    def _normalize_commas_and_whitespace(self, text: str) -> str:
        """Normalize commas and whitespace in the given text."""
        # Collapse consecutive commas into a single comma
        text = re.sub(r'(?:\s*,\s*){2,}', ',', text)
        # Remove leading/trailing commas and normalize whitespace
        text = re.sub(r'^\s*,\s*|\s*,\s*$', '', text).strip()
        text = re.sub(r'\s+', ' ', text).strip()
        text = re.sub(r'\s,', ',', text).strip()
        return text

    def _tokenize(self, text: str) -> List[str]:
        """Tokenize text into alphanumeric lowercase tokens."""
        return re.findall(r"[a-z0-9']+", text.lower())
    
    def sanity_check(self, variations: List[Dict[str, str]]) -> Tuple[List[Dict[str, str]], List[str]]:
        """Perform sanity checks on generated variations."""
        valid = []
        issues = []
        
        for var in variations:
            surface = var['surface_form']
            canonical = var['canonical_label']
            
            # Check 1: Not empty
            if not surface or not surface.strip():
                issues.append(f"Empty surface form for {canonical}")
                continue
            
            # Check 2: Reasonable length (5-150 characters)
            if len(surface) < 2 or len(surface) > 150:
                issues.append(f"Unusual length ({len(surface)}) for: {surface}")
                continue
            
            # Check 3: Contains at least one letter
            if not any(c.isalpha() for c in surface):
                issues.append(f"No letters in: {surface}")
                continue
            
            # Check 4: Base phrase recognizable via token overlap (with punctuation stripped)
            base_tokens_list = self._tokenize(var['base_phrase'])
            surface_token_set = set(self._tokenize(surface))
            
            # Fallback to canonical label tokens if base tokens are empty
            if not base_tokens_list:
                base_tokens_list = self._tokenize(canonical)
            
            augmented_base_tokens = set(base_tokens_list)
            
            transformations_str = var['transformations']
            if transformations_str and transformations_str != "none":
                for t in transformations_str.split('|'):
                    if t.startswith("spelling_variant:"):
                        try:
                            mapping = t.split(":", 1)[1]
                            if "->" in mapping:
                                base_word, variant_word = mapping.split("->", 1)
                                augmented_base_tokens.update(self._tokenize(base_word))
                                augmented_base_tokens.update(self._tokenize(variant_word))
                        except ValueError:
                            # Ignore malformed transformation metadata
                            pass
            
            if augmented_base_tokens and surface_token_set:
                if augmented_base_tokens.isdisjoint(surface_token_set):
                    issues.append(f"Base phrase '{var['base_phrase']}' not recognizable in '{surface}'")
                    continue
            
            valid.append(var)
        
        return valid, issues


def main():
    """Main execution function."""
    print("Training Data Generator for LLM Intent Classification")
    print("=" * 60)
    
    # Step 1: Extract commands from CSV
    print("\n1. Extracting commands from " + INPUT_FILE.as_posix() + "...")
    csv_path = INPUT_FILE
    commands = []
    with open(csv_path, newline='', encoding='utf-8') as csvfile:
        reader = csv.DictReader(csvfile)
        for row in reader:
            commands.append({
                'phrase': row['phrase'],
                'canonical': row['command']
            })
    print(f"   Found {len(commands)} command phrases")
    
    # Step 2: Generate variations
    print(f"\n2. Generating {TARGET_SAMPLES} variations...")
    generator = VariationGenerator(TARGET_SAMPLES)
    variations = generator.generate_variations(commands)
    print(f"   Generated {len(variations)} initial variations")
    
    # Step 3: Sanity check
    print("\n3. Running sanity checks...")
    valid_variations, issues = generator.sanity_check(variations)
    print(f"   Valid: {len(valid_variations)}")
    print(f"   Issues: {len(issues)}")
    if issues:
        print("\n   Sample issues:")
        for issue in issues[:5]:
            print(f"   - {issue}")
    
    # Step 4: Write to CSV
    print(f"\n4. Writing to {OUTPUT_FILE}...")
    output_path = Path(OUTPUT_FILE)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    with open(output_path, 'w', newline='', encoding='utf-8') as f:
        fieldnames = [
            'surface_form',
            'base_phrase',
            'speech_to_detect',
            'canonical_label',
            'repeat_count',
            'transformations'
        ]
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for row in valid_variations:
            writer.writerow({
                'surface_form': row['surface_form'],
                'base_phrase': row['base_phrase'],
                'speech_to_detect': row['speech_to_detect'],
                'canonical_label': row['canonical_label'],
                'repeat_count': row.get('repeat_count', ''),
                'transformations': row['transformations']
            })
    
    print(f"\n✓ Successfully generated {len(valid_variations)} training samples")
    print(f"✓ Output saved to: {OUTPUT_FILE}")
    
    # Step 5: Show sample outputs
    print("\n5. Sample outputs:")
    print("-" * 60)
    samples = random.sample(valid_variations, min(10, len(valid_variations)))
    for i, sample in enumerate(samples, 1):
        print(f"\n{i}. Surface form: {sample['surface_form']}")
        print(f"   Canonical: {sample['canonical_label']}")
        print(f"   Base: {sample['base_phrase']}")
        print(f"   Repeat count: {sample.get('repeat_count', '')}")
        print(f"   Transforms: {sample['transformations']}")
    
    print("\n" + "=" * 60)
    print("Done!")


if __name__ == "__main__":
    main()
