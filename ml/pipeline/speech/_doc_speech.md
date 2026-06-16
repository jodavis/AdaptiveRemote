## Summary: ml/pipeline/speech

Speech augmentation stages for the OOP pipeline. Each stage is a `ModifierStage`
subtype that takes an `AudioSample` manifest (or `TextSample` for the TTS stage) and
produces a derived `AudioSample` manifest with per-sample variation applied.

## Stages

| Module | Stage class | Transform |
|--------|-------------|-----------|
| [`tts_stage.py`](tts_stage.py) | `TtsSampleGenerator` | `TextSample → AudioSample` via edge_tts |

Planned stages (not yet implemented): `delay_stage.py`, `background_noise_stage.py`,
`mic_noise_stage.py`, `token_stage.py`, `spectrogram_stage.py`.

## Key design decisions

**`TtsProvider` protocol as the synthesis seam.** `TtsSampleGenerator` accepts a
`TtsProvider` and never imports `edge_tts` directly — unit tests supply a stub, the
entry-point supplies `EdgeTtsProvider`. Retries are entirely `EdgeTtsProvider`'s
responsibility; the stage treats `synthesize()` as infallible.

**`applied_values` stores raw int for `speech_rate`, not the formatted string.**
The edge_tts rate string (e.g. `"+5%"`) is assembled inside `_generate_output` at
synthesis time and never persisted. Keeping the raw int stable means the content hash
does not change if the formatting logic changes.

**`AudioSample.transcript` = `TextSample.label` (the canonical command), not
`TextSample.content` (the spoken surface form).** TTS speaks the surface form (which
may include hesitations or pleasantries), but the model is trained to output the
canonical command. `_derive_id` also uses `input_sample.label` as the filename stem
prefix for the same reason.

**Voice list fetched and filtered in the entry-point, not in the stage.** The stage
accepts `list[str]` voices directly. The entry-point runs `asyncio.run(edge_tts.list_voices())`
and filters: `Gender == 'Female'`, `Locale == 'en-US'`, `':' not in ShortName`,
`'DragonHD' not in ShortName`, `'Turbo' not in ShortName`.
