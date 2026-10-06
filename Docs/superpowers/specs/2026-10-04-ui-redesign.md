# Borrowed Hex: clean menus and an arcane skill tree

## 1. Design direction and constraints

Use dark arcane ritual styling throughout: ink-dark surfaces, aged gold, ivory text, restrained engraved ornament, and magic that responds to player actions.

The audit covered the current UI source, live menus, and preview states for run-flow screens. Confirmed layout defects include the main title overlapping its subtitle, capture-style cards crossing the footer, records overflowing their panel, and the tutorial completion heading colliding with its body.

Approved choices: three main destinations; dark arcane ritual styling; preserve all twelve skills and existing progression rules. This is a presentation redesign. Keep gameplay, rewards, save compatibility, platform behavior, and progression intact.

Shared visual rules:

- Palette: midnight ink `#11101B`, panel plum `#201B2C`, aged gold `#C8A66A`, ivory `#EEE7D8`, muted text `#B4ADBD`, warning red `#DA7777`.
- Typography: Cinzel for the logo and headings; Source Sans 3 for controls, descriptions, and numbers. Bundle fonts and their licenses.
- Give each screen one prominent action. Use quieter outlines or text treatments for secondary actions.
- Use consistent padding and an 8-unit spacing scale. Reserve separate header, content, and footer regions.
- Keep ornament around edges and the skill map; keep reading surfaces quiet.
- Replace repeated instructions with visible states, contextual explanations, and optional details. Keep costs, requirements, and warnings explicit.
- Fit layouts to the available screen area and UI scale. Scroll long content inside bounded viewports.
- Combat feedback colors remain separate from the menu palette. In particular, overcharge uses the existing shared `FeedbackColors.Overcharge`, not the muted menu gold.

## 2. Changes across every screen

### Main menu

Replace the ten-button stack with Play, Character, and Records, positioned to the left so the arena and rogue remain visible.

```text
Borrowed Hex                              Training   Settings gear

[ Short run | Endless ]
[         Play         ]

Character   - 2 skill points
Records

Mastery 3 - Snatcher

Cheats                                          Quit
```

- Play starts the selected mode directly. Default to Short run; remember the selection for the current session.
- Character opens a shared screen with Skills and Capture Style tabs.
- Training opens a small menu containing Tutorial and Practice.
- Put the Settings gear in the upper-right corner, with a tooltip and keyboard-focus label.
- Put Cheats and desktop Quit in opposite footer corners. Omit Quit on WebGL.
- Remove the tagline and full XP breakdown from this screen. Keep mastery, current style, and an unspent-point badge.
- When cheats are active, show a compact notice beside Play: "Cheats active - progression disabled."
- Display save failures separately, with readable explanatory text.

### Remaining screens

| Screen | Required changes |
|---|---|
| Character | Shared header with mastery level, XP progress bar, and available points. Skills and Capture Style tabs retain their selection during the session. One consistent Back control. |
| Capture Style | Replace oversized text slabs with three compact cards: icon, name, one trade-off sentence, and aligned comparison values. Use a selected border and checkmark instead of filling the whole card green. Move the full effective-stat breakdown into Details. Remove "Click to select" and the paragraph repeating mechanics shared by every style. Keep immediate selection and saving. |
| Records | Separate Records and Achievements into tabs. Records use aligned rows for mode, style, score, and duration; expanding a row reveals its remaining metadata. Move seed, build version, and internal passive IDs out of the default view; show readable skill names in Details. Give totals their own compact summary. Use a scrollable content area above the fixed footer. |
| Achievements | Compact entries with an emblem, name, condition, and earned indicator. Show the completion count once. Replace repeated "(earned)" text with a checkmark and visual treatment. Provide All, Earned, and Locked filters. |
| Settings | Replace full-width cycling buttons with labeled rows: display selector, UI-scale stepper with percentage, and toggles for flashes and hints. Keep immediate application and saving. Hide parent menu controls while Settings is open; restore the opening control's focus on close. Preserve platform-specific display options. |
| Cheats | Two labeled toggles: Invincibility and All skills unlocked. One explanation: "Session only. Active cheats disable XP, records, and achievements." Keep cheats separate from ordinary Settings. |
| Pause | Resume is prominent, followed by Settings, Restart run, and Main menu. Move desktop Quit to a quiet footer action. Put the Esc/P hint beside Resume. Hide HUD and Practice controls beneath the overlay. Confirm abandoning or restarting an active counted run, explaining that unfinished progress is lost; default focus to Cancel. |
| Practice | Replace the right-edge button wall with a collapsible Practice tools drawer, closed initially. Group existing actions under Spawn, Arena, and Run tools. Put Reset inside the drawer. Preserve every existing tool and its restrictions. |
| Combat HUD | Keep life dominant at top-center, score at top-left, and pause at top-right. Consolidate the objective into a short line, such as "Encounter 2/3 - 6 remaining." Replace the upgrade paragraph with four compact icon/rank entries; expose names and effects through hover/focus and pause inspection. Show chain information only while active. |
| Captured-hex slots | Keep two fixed slot positions and independent countdowns. Use payload icons/counts, capacity, power, and remaining time in consistent positions. Replace the `>` marker with an ivory selection marker. Represent frozen, unstable, fused, locked, and overcharged states with distinct symbols plus short labels. Apply the gold-outline behavior specified below. Shorten hints to "Catch [LMB] - Fire [RMB] - Swap [Q]"; preserve the hints setting. |
| Upgrade offers | Keep three readable offer rows with icon, name, rank, concise effect, and a separate action area. Show owned upgrades once inside the panel and hide the external HUD. Replace repeated flavor copy with a useful life-cost summary and before/after life preview. Keep each paid action's exact cost visible. Shorten the free action to Continue. Show the final-slot warning once beside the held-upgrade count. |
| Swap selection | Use "Replace an upgrade" as the heading. Show the incoming upgrade once, followed by held upgrades with name, rank, and effect. Remove the encounter heading, flavor line, and duplicate Holding list during this step. Keep Back as the default focus. |
| Results | Show outcome, labeled Score/Duration/Kills values, XP progress, and notable rewards first. Put complete statistics and the XP breakdown behind Details. Show new records and achievements; move unchanged record comparisons into Details. Remove redundant outcome text such as "Defeated" followed by "You fell." Keep Play again prominent, Main menu secondary, and the R shortcut visible. Hide the underlying HUD. |
| Tutorial prompts | One lesson indicator, one instruction, and progress within a compact panel. Remove the duplicate lesson number from the HUD. Hide irrelevant score and general hints during lessons. Retain every instruction needed to complete each lesson and suppress prompt flashes when Reduce flashes is enabled. |
| Tutorial completion | A properly sized "Tutorial complete" heading and one reminder: "Your life drains during runs. Defeat enemies to reclaim it." Keep Play and Main menu actions. Hide the HUD and prompt panel beneath it. |
| Boss introduction and transitions | Keep existing cinematic timing and gameplay pause behavior. Present the boss name once per introduction, with restrained ornament. Remove the generic "Boss" label and "Defeat it before the time runs out" sentence. Use the cinematic title when that sequence owns the introduction, and the compact banner as its fallback. Clear titles before results appear. |

Use Life consistently for the draining resource in player-facing descriptions and costs. Keep numerical effects derived from live tuning.

### Explicit overcharge feedback for both hex slots

Current behavior: `PacketIndicator` makes only the thin countdown bar gold when a packet is overcharged. Its gold outline instead indicates ordinary selection, so the meanings are too similar. Replace that behavior with a dedicated overcharge border and an independent ivory selection marker.

| Slot state | Required feedback |
|---|---|
| Selected, ordinary hex | Ivory selection marker; no gold overcharge border. |
| Selected, overcharged hex | Smoothly pulsing gold border, using exactly `FeedbackColors.Overcharge` (`new Color(1f, 0.84f, 0.2f)`), matching the stored attack. Keep the countdown gold and the short Overcharge state label. |
| Frozen, overcharged hex | Steady gold border and countdown. Keep the Frozen state marker so stored power does not imply an immediate firing deadline. |
| Reduce flashes enabled | Steady gold border for every overcharged hex. No pulse or entry flash. |
| Paused while overcharged | Freeze pulse progression with the gameplay clock. Resume without advancing decay or replaying an entry cue. |
| Empty, locked, released, expired, or rebound to a new run | Clear the previous packet's overcharge border immediately. Retain any applicable empty/locked state. |

Implementation details for this cue:

- Use a dedicated border graphic, not a flash of the entire box. The backing and text stay steady and readable.
- Derive overcharge from the slot's own `CapturedPacket.IsOvercharged(sim.Stats.Power)`; never use a global flag, selected-slot state alone, or remaining-time guesswork.
- Start the border at full gold intensity on entering the selected overcharge state, then pulse smoothly between 45% and 100% opacity at 2 Hz. Use gameplay time so pausing freezes the animation. A newly selected overcharged packet starts bright; a frozen packet remains steady.
- Continue respecting priming: an unstable packet must not show a Fire instruction. Its unstable marker remains visible alongside any actual overcharge state.
- Hand-full rejection uses the selection marker and existing brief red rejection cue; it must not erase the independent gold overcharge border.
- Read the shared gold from `FeedbackColors.Overcharge`; remove the duplicate local overcharge color from the slot presenter. Keep the stored-attack presenter on the same shared color source.
- This cue changes presentation only. Preserve decay, frozen-slot behavior, power, release, expiry, and backfire rules.

## 3. Skill tree: The Broken Accord

Replace the four-column list with an incomplete ritual seal built around the rogue's torn Ledger entry.

The central scrap represents the stolen existence sustaining the rogue. Four connected paths extend outward through three concentric tiers. Unlocking a skill illuminates its glyph and the prerequisite connection leading to it. The center is decorative, not an additional purchasable skill.

| Position | Branch | Inner to middle to outer node |
|---|---|---|
| Upper-left | Precision | Wide Grasp, Deep Pockets, Quick Draw |
| Upper-right | Mobility | Light Feet, Quick Recovery, Long Stride |
| Lower-left | Resilience | Steady Nerves, Borrowed Hours, Slippery |
| Lower-right | Blood Price | Leech, Siphon, Blood Debt |

Appearance:

- Thin, broken circular engravings surround the central entry. Slightly curved connections give each branch the appearance of a drawn working.
- Each node has an engraved icon and its name. Effects and requirements stay out of the map.
- Inner and middle nodes use circular seals; outer skills use larger diamond seals.
- Branch emblems communicate grasp, movement, protection, and blood repayment.
- Decorative rings never resemble connections between unrelated branches.

Interaction:

- The map occupies roughly two-thirds of the content area; a selected-node inspector occupies the remainder. All twelve nodes remain visible without panning or zooming.
- Clicking or keyboard-focusing a node selects it; purchasing requires the inspector's "Unlock - 1 point" action.
- The inspector shows name, effect, mastery requirement, prerequisite, and state. Locked nodes remain selectable so players can inspect their requirements.
- Owned nodes show Active without a purchase action. Cheat-enabled nodes show Active through cheats, distinctly from earned ownership.
- Owned: illuminated glyph. Available: gold outline. Locked: muted glyph with lock badge. Selected: an additional ivory focus ring. State never depends on color alone.
- Unlocking produces one brief connection-lighting animation; Reduce flashes uses a steady transition.
- Place Reset skills in the footer, with a confirmation stating the exact refund. Show "Skills apply next run" once near the inspector.

Keep existing mastery gates of 2, 4, and 7, one-point costs, prerequisite chains, automatic activation, and free refunds.

## 4. Implementation approach and interfaces

1. Build the shared UI foundation. Extend existing code-built uGUI helpers with typography roles, button hierarchy, icon controls, toggles, bounded scrolling, and consistent screen framing. Fix overflow through layout and sizing rather than smaller text.
2. Reorganize navigation. Add Character tabs, the mode selector, and the Training utility menu. Centralize screen transitions and focus restoration so one foreground screen owns input.
3. Implement the ritual map and menu screens. Keep visual node positions and icons separate from progression data. Split selection from purchase; continue using existing domain functions for buying and refunds.
4. Clean run presentation. Apply the HUD, explicit per-slot overcharge border, Practice drawer, upgrade, results, tutorial, and introduction changes without altering simulation timing or rewards.
5. Validate and polish. Update affected UI tests, capture representative screens, and check navigation through complete runs.

Preserve existing programmatic menu entry keys where practical by routing them to their new destinations. Keep profile schemas, skill IDs, domain APIs, and loadout resolution unchanged; navigation state stays in memory. The slot presenter consumes each packet's existing state and the shared feedback palette; add presentation state for pulse timing without adding anything to the profile or simulation schema.

Use the existing UI helpers, `PacketIndicator`, and `GameRoot` menu routing as integration points. Keep this change focused on presentation; do not fold in unrelated arena, combat, or VFX work already present in the checkout.

## 5. Acceptance checks

- Every screen fits at 1280x720, 1600x900, 1920x1080, and ultrawide resolutions across existing 80-130% UI scales.
- No headings, descriptions, cards, or buttons overlap. Long records and expanded details scroll without covering navigation.
- Mouse and keyboard reach every action; tooltips work on focus, and closing a screen restores focus correctly.
- Validate fresh profiles, mastery gates, zero points, purchased skills, full refunds, and cheat-only activation.
- Preserve the paid-upgrade opening-click guard, safe Continue/Back focus, swap restrictions, costs, and final-slot behavior.
- Check mixed payloads, unstable/fused/locked slots, full upgrade inventories, and active chains.
- For each of the two hex slots, check the instant before and at the overcharge boundary, selected pulsing gold, frozen steady gold, swapping a frozen overcharge into selection, and two simultaneously overcharged packets. Only the selected packet pulses; both charged packets remain visibly gold.
- Check reduced flashes, paused pulse timing, brief hand-full rejection during overcharge, and exact shared-color equality with the stored attack.
- Check that releasing, expiring, fusion-locking, clearing, restarting, and rebinding remove stale overcharge borders. Verify ordinary selection is ivory, including when the selected slot is empty.
- Check every run ending, large reward summaries, save failures, tutorial completion, and pausing during introductions.
- Confirm modal clicks never become combat actions and WebGL retains its display and Esc/P behavior.
- Use isolated in-memory profiles for validation; finish with Windows and WebGL smoke passes.

Success means three clear main destinations, no overlapping content, visible decisions and costs, unmistakable gold overcharge feedback on the correct hex slot, and a skill screen that feels like a magical working belonging to Borrowed Hex.
