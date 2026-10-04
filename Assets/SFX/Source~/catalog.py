"""Approved A-L inventory. Reused cues are explicit aliases, never duplicate WAVs.

Priorities are delivery batches, not runtime audio priority. Durations below refer
to dry gestures; one-shots include an additional, naturally fading effect tail.
"""

from dataclasses import dataclass


@dataclass(frozen=True)
class Cue:
    id: str
    group: str
    name: str
    recipe: str
    seconds: float = .25
    variants: int = 3
    priority: int = 3
    tone: float = 440
    peak_db: float = -9
    loop: bool = False
    note: str = ""


CUES = []
ALIASES = {}


def add(id, group, name, recipe, seconds=.25, variants=3, priority=3,
        tone=440, peak=-9, loop=False, note=""):
    CUES.append(Cue(id, group, name, recipe, seconds, variants, priority,
                    tone, peak, loop, note))


def rows(group, priority, text):
    # Compact authored table: ID, name, recipe, seconds, variations, root Hz, peak dBFS.
    for line in text.strip().splitlines():
        id, name, recipe, seconds, variants, tone, peak = line.strip().split('|')
        add(id, group, name, recipe, float(seconds), int(variants), priority,
            float(tone), float(peak))


# Batch 1: the borrowing loop and the feedback needed to read active combat.
rows('CoreMagic', 1, '''
E1|catch_snatcher|grasp|.22|4|610|-9
E2|catch_collector_style|grasp|.42|4|360|-10
E3|catch_daredevil|dash_grasp|.18|4|780|-9
E4|catch_window_empty|air|.15|3|1300|-23
E5|capture_success|capture|.18|6|520|-9
E6|capture_append|capture|.11|5|820|-15
E7|capture_perfect|perfect|.30|4|740|-6
E8|packet_capacity_full|seal|.16|3|590|-13
E9|catch_hand_occupied|deny|.13|2|260|-17
E10|catch_slots_occupied|deny|.18|2|190|-16
E11|catch_capacity_rejected|deny|.11|2|330|-17
E12|catch_recovery_unavailable|deny|.09|2|390|-23
E13|packet_forms|form|.22|4|460|-14
E14|packet_primed|ready|.15|3|830|-14
E15|release_unprimed|deny|.12|2|290|-18
E16|release_empty|air|.07|2|1700|-28
E18|packet_expiry_warning|unstable|.32|3|730|-13
E19|overcharge_window_enter|ready|.14|3|1120|-10
E20|overcharge_release|overcharge|.40|4|185|-4
E21|packet_backfire|backfire|.55|4|97|-5
E22|slot_swap|swap|.16|4|660|-15
E23|packet_bank_freeze|freeze|.20|3|760|-20
E24|packet_bank_resume|form|.18|3|480|-19
E25|fusion_slot_lock|seal|.18|2|220|-17
E26|fusion_slot_unlock|ready|.16|2|540|-18
''')
add('E17', 'CoreMagic', 'packet_charge', 'charge_loop', 3, 1, 1, 360, -20, True,
    'Steady periodic texture: later integration may crossfade this with warning and Overcharge layers. Freeze stops playback; this WAV does not encode the timer.')

# Five magic schools have recognisable timbres on both sides of the catch.
for id, name, tone, shot, seconds in [
    ('F1', 'acolyte_bolt', 680, 'bolt', .19),
    ('F2', 'scatter_volley', 980, 'scatter', .15),
    ('F3', 'siege_rocket', 105, 'rocket', .35),
    ('F4', 'collector_bolt', 270, 'boss_bolt', .24),
    ('F5', 'riposte', 1200, 'riposte', .16),
]:
    operations = ['launch', 'returned_launch', 'impact', 'wall_impact', 'dissipate']
    if id == 'F5':
        operations.remove('returned_launch')  # Ripostes have no hostile version.
    for op in operations:
        recipe = shot if 'launch' in op else ('magic_hit' if op == 'impact' else 'wall_hit' if op == 'wall_impact' else 'dissipate')
        add(id, 'Projectiles', name + '_' + op, recipe,
            seconds if 'launch' in op else .24 if op != 'dissipate' else .20,
            6 if op == 'impact' else 4, 1, tone * (1.22 if op == 'returned_launch' else 1),
            -8 if 'launch' in op else -10 if op == 'impact' else -16 if op == 'wall_impact' else -24,
            note='Returned variation adds a clean harmonic answer to the hostile timbre.' if op == 'returned_launch' else '')
    add(id, 'Projectiles', name + '_flight', 'rocket_loop' if id == 'F3' else 'flight_loop',
        2, 1, 1, tone, -26, True, 'Spatial mono loop. Limit simultaneous flight voices in future integration.')
rows('Projectiles', 1, '''
F3|siege_rocket_explosion|explosion|.85|5|74|-5
F3|siege_rocket_debris_tail|rubble|1.10|4|300|-17
F5|riposte_pierce|pierce|.14|4|1400|-11
F6|parry_pursuer|parry|.22|5|940|-5
F7|parry_collector|parry|.34|4|620|-5
F8|parry_stagger|stagger|.24|3|180|-12
F9|parry_opportunity|ready|.07|2|1240|-22
F10|projectile_pierce|pierce|.14|5|1060|-13
F11|impact_school_resisted|resist|.16|4|240|-15
F12|impact_school_amplified|magic_hit|.20|4|850|-10
F13|impact_overcharge_layer|overcharge|.25|4|380|-9
F14|explosion_overcharge_layer|explosion|.60|3|52|-8
F15|mixed_packet_release|overcharge|.22|3|460|-12
''')
rows('Player', 1, '''
D5|dash_launch|dash|.18|6|620|-10
D6|dash_end|footstep|.12|4|185|-19
D7|dash_ready|ready|.11|3|960|-20
D9|player_hit_projectile|hurt|.24|6|180|-7
D10|player_hit_melee|hurt|.30|6|125|-7
D11|player_hit_contact|hurt|.15|4|240|-12
D13|life_danger_enter|warning|.46|2|170|-14
D17|player_death|death|1.10|3|175|-8
D18|borrowed_life_exhausted|death|1.45|2|110|-10
''')
rows('Enemies', 1, '''
G5|acolyte_windup|windup|.65|4|680|-16
G6|scatter_windup|windup|.80|4|980|-16
G7|siege_windup|windup|1.30|4|105|-14
G8|pursuer_windup|melee_windup|.55|4|230|-14
G9|pursuer_strike|slash|.18|6|470|-9
''')

# Batch 2: first impression, encounter identity, and the boss's distinguishing gestures.
rows('Collector', 2, '''
H1|collector_manifest|boss_form|1.35|2|110|-8
H2|collector_footstep|footstep|.20|6|76|-15
H3|collector_stream_windup|windup|.52|3|270|-13
H5|collector_fan_windup|windup|.63|3|390|-13
H6|collector_fan_release|scatter|.28|4|390|-7
H7|collector_sweep_windup|melee_windup|.77|3|160|-11
H8|collector_sweep|slash|.42|4|170|-7
H9|collector_sweep_ground|scrape|.42|4|260|-17
H10|collector_slam_warning|slam_warning|.65|3|75|-11
H11|collector_slam|slam|.90|4|48|-5
H12|collector_summon_windup|summon|.95|3|200|-13
H13|collector_summon_release|boss_form|.48|3|320|-9
H14|collector_summon_arrival|form|.38|4|390|-13
H15|collector_teleport_warning|vortex|.60|3|230|-13
H16|collector_teleport_depart|teleport_out|.23|4|310|-9
H17|collector_teleport_arrive|teleport_in|.28|4|520|-8
H18|collector_hurt|boss_hurt|.32|6|95|-9
H19|collector_stagger|stagger|.45|3|87|-10
H20|collector_death_rupture|backfire|.80|2|60|-5
H20|collector_death_collapse|slam|1.50|2|45|-8
H20|collector_death_binding_tail|death|3.60|1|140|-15
H21|collector_encore_entrance|boss_form|1.70|2|82|-8
''')
ALIASES['H4'] = ['Projectiles/collector_bolt_launch']

MUSIC = [
    # ID, name, tempo, bars, musical role, delivery priority, mood description.
    ('A1','main_menu',78,24,'menu',2,'D minor; fragile celesta motif, bowed pad, low bells. Three eight-bar movements.'),
    ('A2','courtyard_combat',112,24,'courtyard',2,'D minor; hand-drum pulse, pizzicato ostinato, motif enters after eight bars.'),
    ('A3','graveyard_combat',112,24,'graveyard',2,'D minor; darker chords, hollow bells, syncopated low strings. Shares transition tempo with Courtyard.'),
    ('A4','cave_combat',112,24,'cave',2,'D minor with Phrygian colour; metallic plucks and driving percussion. Shared arena tempo.'),
    ('A5','sanctum_intro',0,0,'intro',2,'Fixed 7.8-second reveal: rise at 0, darkness at 1.6, eight lights from 2.6 to 5.4, title at 5.4. LightsAt is the start of the interval timer, not the first ignition.'),
    ('A6','collector_battle',138,32,'boss',2,'D harmonic minor; inverted borrowing motif, low string pulse, ritual drums. Four eight-bar movements.'),
    ('A7','endless_arena_escalation',112,24,'arena_stem',6,'Percussion and ostinato only; beat-aligned with A2-A4. Do not add a second full mix.'),
    ('A7','endless_collector_escalation',138,32,'boss_stem',6,'Additional drums and dissonant pulse only; beat-aligned with A6.'),
    ('A8','training',82,16,'training',6,'Gentle D minor; sparse plucked motif and soft pad.'),
    ('A9','upgrade_selection',78,16,'upgrade',6,'D minor; suspended harmony and restrained clockwork plucks.'),
    ('A10','victory_results',84,16,'victory',6,'D major/modal resolution; returning motif with an open final phrase.'),
    ('A11','failure_results',66,16,'failure',6,'D minor; slow low piano-like plucks and unresolved motif.'),
]

rows('Stingers', 2, '''
B1|run_start|stinger_start|1.10|1|293.665|-10
B2|encounter_clear|stinger_clear|1.75|1|293.665|-9
B3|encounter_begin|stinger_start|.85|1|220|-12
B4|boss_reveal|stinger_boss|2.40|1|146.832|-8
B5|boss_combat_start|stinger_boss|1.15|1|110|-8
B6|collector_defeated|stinger_victory|2.75|1|146.832|-8
B7|short_run_victory|stinger_victory|3.50|1|293.665|-9
B8|death_defeat|stinger_failure|2.40|1|146.832|-11
B9|time_expiry_defeat|stinger_failure|2.85|1|110|-12
''')

# Batch 3: everyday movement, enemies, and the entire shared interface vocabulary.
for id, name, tone in [('D1','courtyard_stone',185),('D2','graveyard_ground',115),
                       ('D3','cave_rock',235),('D4','sanctum_stone',140)]:
    add(id,'Player','footstep_'+name,'footstep',.16,8,3,tone,-19)
rows('Player', 3, '''
D8|dash_unavailable|deny|.09|2|390|-24
D12|grace_protection|freeze|.24|3|1100|-23
D15|life_restored_kill|heal|.24|4|580|-20
D16|life_stolen|siphon|.26|4|380|-22
''')
add('D14','Player','low_life_heartbeat','heartbeat_loop',2,1,3,56,-22,True,
    'One double beat per two seconds. Independently muteable in future integration; do not accelerate with frame rate.')
rows('UI', 3, '''
C1|focus_hover|ui_focus|.045|3|880|-24
C2|button_press|ui_press|.06|3|610|-20
C3|confirm|ui_confirm|.16|3|740|-18
C4|back_cancel|ui_back|.12|3|550|-21
C5|panel_open|paper_open|.23|3|420|-22
C6|panel_close|paper_close|.18|3|350|-23
C7|tab_change|ui_focus|.08|3|700|-22
C8|card_node_select|seal|.11|3|560|-21
C9|toggle_on|ui_confirm|.09|2|840|-22
C10|toggle_off|ui_back|.08|2|670|-23
C11|setting_increase|ui_focus|.055|2|960|-24
C12|setting_decrease|ui_focus|.055|2|720|-24
C13|action_rejected|deny|.13|3|270|-22
C14|confirmation_open|paper_open|.28|2|310|-21
C15|destructive_confirm|seal|.28|2|180|-18
C16|style_equipped|ui_confirm|.30|3|590|-17
C17|details_open|paper_open|.21|2|480|-24
C17|details_close|paper_close|.17|2|410|-24
C18|scroll_tick|paper_tick|.035|3|1900|-32
C19|notification|ready|.27|3|840|-19
''')
for id, name, tone, movement in [
    ('G1','acolyte',640,'cloth_step'),('G2','pursuer',190,'footstep'),
    ('G3','scatter_caster',920,'cloth_step'),('G4','siege_familiar',110,'footstep'),
]:
    for op, recipe, duration, count, peak in [
        ('spawn_warning','warning',.36,3,-19),('materialize','form',.40,4,-14),
        ('movement',movement,.15,6,-25),('hurt','enemy_hurt',.20,6,-13),
        ('death','unbind',.65,6,-10),
    ]:
        add(id,'Enemies',name+'_'+op,recipe,duration,count,3,tone,peak)
rows('Enemies', 3, '''
G10|overstay_warning|unstable|.60|3|330|-17
G11|elite_evolution|evolve|.95|4|150|-8
G12|elite_attack_layer|unstable|.17|4|210|-19
G12|elite_movement_layer|scrape|.14|4|180|-27
G13|elite_hurt_layer|enemy_hurt|.25|4|140|-17
G14|elite_death_accent|unbind|.90|4|110|-12
''')

# Batch 4: paid progression and the mechanics that add layers to the base release.
rows('Upgrades', 4, '''
I1|upgrade_offers|form|.48|2|590|-18
I2|upgrade_buy_life_payment|siphon|.56|3|220|-14
I3|upgrade_rank_up|ui_confirm|.40|3|780|-14
I4|upgrade_life_insufficient|deny|.18|2|190|-20
I6|piercing_return_layer|pierce|.18|4|1600|-18
I7|echo_volley_release|echo|.24|4|650|-16
I8|heavy_orbit_hit|orbit_hit|.20|4|160|-19
I9|parting_gift|gift|.48|4|220|-10
I10|final_second_perfect|perfect|.24|3|1120|-10
I11|overflow_replace|overflow|.32|4|560|-10
I12|fusion_merge|fusion|.58|4|290|-8
I13|quick_draw|quickdraw|.15|4|1180|-13
I14|chain_start|ready|.15|3|590|-18
I15|chain_advance_02|ready|.15|2|660|-18
I15|chain_advance_03|ready|.16|2|740|-18
I15|chain_advance_04|ready|.17|2|880|-17
I15|chain_advance_05|ready|.18|2|988|-17
I16|chain_milestone|perfect|.35|3|1174|-13
I17|chain_break|freeze|.12|2|320|-26
I18|xp_tally_tick|ui_focus|.05|3|930|-26
I18|xp_tally_complete|ui_confirm|.30|2|830|-20
I19|mastery_level_up|stinger_clear|1.30|1|440|-13
I20|mastery_point_awarded|ready|.32|2|990|-20
I21|skill_unlock|fusion|.70|3|440|-13
I22|skill_respec|freeze|.55|2|580|-18
I23|achievement_unlock|stinger_clear|1.65|1|587.33|-14
I24|record_best_score|stinger_victory|1.45|1|440|-14
I25|record_longest_run|stinger_clear|1.55|1|349.228|-15
''')
ALIASES['I5'] = ['UI/confirm']
add('I8','Upgrades','heavy_orbit_active','orbit_loop',2,1,4,160,-28,True,
    'Play only while holding a packet with Heavy Orbit. Short contact pulse is a separate asset.')

# Batch 5: the places, their material vocabulary, and their transformation.
for id, name, tone in [('J1','courtyard',100),('J2','graveyard',76),
                       ('J3','cave',65),('J4','sanctum',73),
                       ('J5','brazier',280),('J6','blue_flame',450),('J7','candle',740)]:
    add(id,'Ambience',name,'amb_'+name,20 if id not in ['J5','J6','J7'] else 12,
        1,5,tone,-22 if id not in ['J5','J6','J7'] else -27,True,
        'Stereo environmental bed.' if id not in ['J5','J6','J7'] else 'Mono emitter loop; quieter than attack cues.')
for id, material, tone in [('J8','stone',170),('J9','ceramic',740),
                           ('J10','rock',110),('J11','wood',260),('J12','crystal',1300)]:
    for op, duration, variants, peak in [('wear',.25,4,-24),('collapse',1.0,4,-12),
                                          ('settle',1.20,3,-24),('form',.65,3,-19)]:
        add(id,'Environment',material+'_'+op,'material_'+material+'_'+op,
            duration,variants,5,tone,peak)
rows('World', 5, '''
J13|morph_begin|vortex|.90|2|190|-18
J14|morph_patch|glitch|.27|6|520|-22
J15|scenery_dissolve|dissipate|.68|4|380|-24
J16|scenery_form|form|.66|4|440|-23
J17|morph_settle|freeze|.70|2|290|-24
J18|sanctum_pull_begin|vortex|.40|2|180|-12
J19|sanctum_rising_transport|rise|1.60|1|110|-11
J20|sanctum_darkness|freeze|.45|1|97|-22
J21|sanctum_light_01|ignite|.30|1|293.665|-19
J21|sanctum_light_02|ignite|.30|1|329.628|-19
J21|sanctum_light_03|ignite|.30|1|349.228|-19
J21|sanctum_light_04|ignite|.30|1|440|-19
J21|sanctum_light_05|ignite|.30|1|466.164|-19
J21|sanctum_light_06|ignite|.30|1|587.33|-19
J21|sanctum_light_07|ignite|.30|1|659.255|-19
J21|sanctum_light_08|ignite|.30|1|698.456|-19
J22|sanctum_reveal_resolve|boss_form|1.40|1|146.832|-12
''')

# Batch 6: finishing the less frequent modes, teaching, and planned text dialogue.
rows('Stingers', 6, '''
B10|endless_wave_clear|stinger_clear|1.15|1|220|-13
B11|endless_cycle_escalates|stinger_boss|1.65|1|164.814|-12
B12|endless_boss_clear|stinger_clear|2.15|1|146.832|-12
B13|run_retired|stinger_neutral|1.50|1|220|-17
''')
rows('Training', 6, '''
K1|lesson_prompt|ready|.20|2|660|-21
K2|objective_progress|ui_confirm|.15|3|880|-20
K3|lesson_complete|stinger_clear|.90|1|440|-16
K4|tutorial_complete|stinger_victory|1.80|1|293.665|-14
''')
ALIASES['K5'] = ['UI/confirm']
add('L1','Narrative','dialogue_tick','dialogue',.025,1,6,640,-21.94,False,
    'Planned story asset: 25 ms 640 Hz sine, 5 ms attack/release, amplitude 0.08. Fixed pitch across speakers; integration caps rate to one tick per 0.06 s.')
ALIASES['L2'] = ['UI/confirm']
ALIASES['L3'] = ['UI/button_press','UI/back_cancel']


def validate_catalog():
    names = [(c.group,c.name) for c in CUES]
    assert len(names) == len(set(names)), 'Two cues would overwrite the same path'
    covered = set(c.id for c in CUES) | set(ALIASES) | set(m[0] for m in MUSIC)
    expected = {f'{letter}{i}' for letter,n in [('A',11),('B',13),('C',19),('D',18),
        ('E',26),('F',15),('G',14),('H',21),('I',25),('J',22),('K',5),('L',3)] for i in range(1,n+1)}
    assert covered == expected, f'Inventory mismatch: missing {expected-covered}; extra {covered-expected}'
    for id, targets in ALIASES.items():
        for target in targets:
            assert tuple(target.split('/')) in names, f'Broken alias {id}: {target}'


validate_catalog()
