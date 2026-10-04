# Borrowed Hex audio library

Original audio assets for the approved A-L inventory. No scene bindings, runtime
audio components, mixer assets, game scripts, or settings have been added.

## Delivery order

1. Core borrowing, projectiles, parries, Overcharge, backfire, dash, hurt, death,
   and regular-enemy attack warnings.
2. Main menu, arena and Collector scores, Collector effects, main run stingers.
3. Shared menus, footsteps, survival textures, regular enemies and evolution.
4. Run upgrades, passive proc effects, chains, mastery, achievements and records.
5. Arena ambience, material cover sounds, reality morphs and Sanctum travel.
6. Endless music layers, training, upgrade/results scores and planned dialogue tick.

`manifest.json` maps every approved ID to its files, including explicit shared
cue aliases. A family can include several operations and variations. Filenames
end in `_v01`, `_v02`, etc.; randomly choose one variation rather than layering
all variations. Music filenames do not carry a variation suffix.

## Masters and import

- WAV, 48 kHz, 24-bit PCM masters; the dialogue tick uses 22,050 Hz as required
  by Lore Implementation Plan Task 5.
- Gameplay effects are mono for future spatial placement. Music and arena
  ambient beds are stereo. Fire emitters and mechanical texture loops are mono.
- One-shots include their designed tails and have silent endpoint samples.
- Loop assets play over their entire frame range. See the manifest's `loop` flag;
  a WAV filename alone does not tell an audio source to loop.
- Unity metadata gives short cues PCM imports and long Music/Ambience files
  streaming Vorbis imports at quality 0.8. The on-disk WAV masters stay lossless.
- Four-times oversampled true-peak caps are -3 dBTP for effects and -2 dBTP for
  scores. Music aims at -18 LUFS and endless overlay stems at -27 LUFS, with
  gain reduced further where necessary to retain peak headroom.
- Quiet UI, footsteps, healing, flight, and environment cues intentionally retain
  lower levels. Do not normalize every clip to the same loudness.

## Musical identity

The D-based borrowing motif recurs in menu and encounter scores. The Collector
inverts it; victory gives it a major/modal answer. Arrangements use original
synthetic celesta, modal bells, plucked and bowed textures, bass and ritual drums.
Each loop has multiple phrases with variations, fills and breathing space.

Courtyard, Graveyard and Cave share 112 BPM and 24 bars so their transitions and
the endless arena overlay can be beat-aligned. Collector uses 138 BPM and
32 bars, matched by its own endless overlay. Overlay stems contain additional
rhythm/ostinato, not another full theme; maintain their exported relative gain.

The Sanctum introduction is a 7.8-second one-shot matched to the current reveal:
rise 0-1.6 s, darkness 1.6-2.2 s, eight lights from 2.6-5.4 s, title at 5.4 s.
When integrating, choose whether the score or separate light/reveal accents carry
each moment; stacking every accent would be excessive.

## Integration notes for later

- Prioritize wind-ups, player damage and hex state changes over ambience and
  projectile travel. Rate-limit repeated rejections and healing/chain cues.
- Use one volley-launch sound per volley, not one full-volume launch per pellet.
- Limit flight loop voices. Stop held-hex/orbit textures when their state ends.
- The charge loop is a periodic layer, not a baked three-second countdown;
  warning, ready and Overcharge are distinct cues for state-driven playback.
- Swap/bank/resume layers form one action; do not play all three at full volume.
- Preserve the narrative's silent black lore pages and silent combat captions.
  Dialogue has one mono 22,050 Hz, nominal 25 ms, 640 Hz tick with 5 ms ramps and
  amplitude 0.08, at fixed pitch across speakers. Its later caller uses a saved
  dialogue volume (default 35%) and limits ticks to one per 0.06 s. See
  `Narrative/LORE_AUDIO_READINESS.md` for the complete lore asset handoff.
- Provide independent volume/mute control for repetitive danger/heartbeat cues.
- Boss rupture, collapse, tail, victory stinger and results theme form a sequence;
  avoid playing all of them at the same moment.

## Source, review, and verification

`Source~` is Unity-ignored offline source, not runtime code. It includes the
authored catalog, synthesis/composition renderer, pinned requirements, a complete
delivery ledger, measured asset data, QA report, and review-only audition mixes
with timestamped cue lists. Do not wire the audition montages into the game.

Reproduce with Python 3.13 in an isolated environment:

```text
python -m pip install -r Assets/SFX/Source~/requirements.txt
python Assets/SFX/Source~/render_audio.py --batch 1
python Assets/SFX/Source~/render_audio.py --batch 2
python Assets/SFX/Source~/render_audio.py --batch 3
python Assets/SFX/Source~/render_audio.py --batch 4
python Assets/SFX/Source~/render_audio.py --batch 5
python Assets/SFX/Source~/render_audio.py --batch 6
python Assets/SFX/Source~/render_audio.py --verify
```

To revise one family without rerendering the rest of its batch, add
`--only family_name` (for example, `--batch 2 --only sanctum_intro`).

Technical verification checks decoding, format, finite samples, non-silence, DC,
true peaks, endpoint/seam discontinuities, duplicate files, GUIDs and complete
coverage. These checks do not certify aesthetic quality, naturalism, or a final
game mix. Human listening review and in-engine playback checks remain necessary.

All waveforms and compositions are original procedural output. No external audio
samples, soundfonts, recorded speech, commercial music, or model-generated audio
were used. The Python dependency licenses apply to the tools, not to a borrowed
audio library. No third-party audio attribution is required.
