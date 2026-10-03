# Larger decaying arenas

Owner approved the revised in-chat design on 3 October: multiple redesigned arenas,
larger spaces, a closer shallower following camera, no visible empty exterior,
many degrading objects, gradual stage transitions, and a magical pull into the
boss Sanctum. Keep the downloaded material/prop kit. Commit on main, do not push.
Show the result before any further player build; Windows remains deferred.

Implementation uses an opt-in world layout in RunSetup. Existing default/test and
tutorial layouts remain supported. Each world arena owns its analytic bounds and
decaying cover, and spawning/collision/view use that same current layout. A new
WorldTransition pause reason freezes combat only during separated Sanctum travel.
The presentation owns camera follow, extended terrain, overlapping environment
groups during morphing, and vortex/pull effects. Layout changes happen at existing
stage boundaries.
Owner refinement: ordinary spaces morph in place during live combat over 24 seconds.
Old cover breaks on staggered deadlines; new cover forms only when actors clear
its footprint. Textures and scenery glitch and transform throughout this period.
Combat is not paused and the player is not relocated. Only Sanctum entry uses a
travel pause and magical pull.
Source config assets and Claude's protected files remain unchanged.

1. [x] RED/GREEN: distinct layouts, clean entry positions, simulation bounds/cover
   changes and transition pause; implement current Arena data and stage selection.
2. [x] Replace placeholder pillars with tombs, ruined walls, rocks and obelisks;
   visible wear, breakup and rubble follow their actual simulation durability.
3. [x] Shallower closer follow camera with safe bounds; continuous terrain and
   surrounding scenery; stage travel and magical Sanctum entry.
4. [x] PlayMode checks for transitions, pause/restart/fallback and camera framing;
   show Editor previews, full tests and one fresh scoped review.
5. [x] Update handoff; verify protected hashes; commit only owned files, no push.

Existing tutorial failures must be reported, not repaired in the protected file.
The already-built Web output is from the first art pass and must not be described
as containing this redesign until a new, previewed build is explicitly made.
