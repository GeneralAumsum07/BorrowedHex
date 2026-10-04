# Lore audio readiness

Reviewed against `Docs/LORE_IMPLEMENTATION_PLAN.md`, especially Guaranteed
Presentation and Task 5. All required audio assets are available under
`Assets/SFX`. No audio has been wired to gameplay or story presentation.

## Dedicated dialogue sound

`Narrative/dialogue_tick_v01.wav` is the single shared tick for both the Collector
and rogue. It is mono, 22,050 Hz, 24-bit PCM, with a 640 Hz sine, 5 ms attack and
release, and peak amplitude approximately 0.08. It contains no reverb or speech.

The nominal 25 ms duration rounds to 551 samples at this rate (24.989 ms), the
nearest representable duration. The asset retains its existing Unity GUID.
Import uses the source sample rate and disables normalization, preserving its
deliberately quiet source level. There are no speaker-specific pitch variants.

## Required and shared cue mapping

| Plan use | Prepared asset(s) | Future playback rule |
|---|---|---|
| Confrontation dialogue, section E | `Narrative/dialogue_tick_v01.wav` | Tick as new letters/numbers appear. |
| Collector/rogue ending dialogue, section F | Same dialogue tick | Same pitch and volume as the confrontation. |
| Story menu selection and replay controls | `UI/focus_hover_v*.wav`, `UI/button_press_v*.wav`, `UI/confirm_v*.wav`, `UI/back_cancel_v*.wav` | Reuse shared UI sounds for explicit controls. |
| Skip scene control | `UI/button_press_v*.wav` | One button cue; skipping does not play outstanding text ticks. |
| Locked story entry | `UI/action_rejected_v*.wav` | Reuse the quiet rejection cue if a locked entry accepts interaction. |
| Narrative setting toggles | `UI/toggle_on_v*.wav`, `UI/toggle_off_v*.wav` | Reuse shared settings cues. |
| Dialogue volume adjustment | `UI/setting_increase_v*.wav`, `UI/setting_decrease_v*.wav` | Reuse shared settings cues; no extra narrative sample. |
| Sanctum travel before the anomaly | `Music/sanctum_intro.wav`, `World/sanctum_*_v*.wav` | Existing reveal assets; finish travel before the silent anomaly scene. |
| Boss encounter after confrontation | `Music/collector_battle.wav`, `Collector/collector_manifest_v*.wav` | Begin only after dialogue completes/skips; retain the spawn warning. |
| Results after the ending | `Music/victory_results.wav`, `Stingers/short_run_victory_v01.wav` | Sequence after story handoff; do not add score to silent lore pages. |
| Immediately restartable failure results | `Stingers/death_defeat_v01.wav` or `Stingers/time_expiry_defeat_v01.wav`, `Music/failure_results.wav` | Recall caption adds no sound or playback delay. |

The Sanctum score and separate travel accents are alternatives/layers to balance,
not an instruction to play all of them at full volume. Standard narrative
navigation may use the approved shared UI vocabulary; completing partially typed
text remains silent.

## Deliberate silence

- Black lore pages in the prologue, Last Kindness, Forgotten Answer, Anomaly,
  and Open Window: no text ticks or additional story score.
- Mara's written corrections, crossing-out of Enough, and unfiled requests:
  no speaker tick, voice, paper-writing effect, or new scene accent.
- Instant story text, instant completion of a partially typed line, and automatic
  familiar-scene skips: no text sound or catch-up burst.
- Optional combat reactions and the failure recall caption: silent captions.
- Optional still illustrations: no additional audio requirement.

The plan excludes voice acting, speech synthesis, spoken narration, and a
separate cinematic audio production milestone. No lamp/window/Ledger effects are
required for events represented solely as silent lore text.

## Integration contract for later

The eventual `DialogueTextAudio` caller must use the saved dialogue volume,
default 0.35. This gain is not baked into the WAV: mute/volume zero must work
without changing the text or controls. At most one tick plays per 0.06 seconds,
and only newly revealed letters/numbers trigger it, not whitespace/punctuation.

Pause, focus loss, scene change, skip, cancellation, restart, and menu return
stop active ticks and discard pending ticks. Resume never replays missed sounds.
The text reveal speed stays 35 text elements per second independently of the
audio throttle. None of these runtime behaviors have been implemented here.

## Verification

Run `python Assets/SFX/Source~/verify_lore_audio.py` to check the dedicated tick
against the plan and confirm the shared asset paths. Full library QA remains
available through `render_audio.py --verify`.
