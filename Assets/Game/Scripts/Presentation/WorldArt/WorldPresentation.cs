using System;
using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Presentation-only observer. Runs after ArenaView, replacing just the boss pose and
    /// decorating the authored arena. Rebinding releases old event listeners on every restart.
    /// Player, standard enemies, input, mechanics and exact telegraphs stay with their owners.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class WorldPresentation : MonoBehaviour
    {
        [SerializeField] string resourceFolder = "WorldArt";
        GameRoot root;
        ArenaSim sim;
        WorldArtLibrary art;
        WorldGeometry geometry;
        WorldGeometry previousGeometry;
        WorldEffects travelEffects;
        WorldIntroOverlay introOverlay;
        SpriteRenderer hiddenPlayer;
        int litPillars;
        readonly WorldTravel travel = new WorldTravel();
        int arenaRevision = -1;
        Vector3 lastPlayerVisual, pullFrom, pullTo, cameraFocus, pullCameraFrom, pullCameraTo;
        GameObject originalArena;
        bool originalArenaActive;
        // The glitch morph blends the whole night rig with patch progress, so no light pops
        // when the incoming arena's theme is applied at the morph's first kill.
        WorldLighting morphFrom, morphTo;
        // The theme whose braziers are burning; swapped at the morph's halfway point.
        string flamesTheme;
        float nextPullParticle;
        WorldEffects effects;
        WorldEffects terminalEffects;
        double terminalElapsed;
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        readonly Dictionary<Renderer, Material> changed = new Dictionary<Renderer, Material>();
        CharacterView bossBody;
        Sprite originalBossSprite;
        float originalBossScale;
        int bossId = -1, patternCount = -1, sceneryIndex = -1;
        string clip;
        double poseBegan, hurtUntil;
        BossStage previousStage;
        Vector2 beforeTeleport;
        Light sun;
        // Night rig state. moonIntensity is the reveal's target; fireFade is 0 while the
        // Sanctum is dark and rises with the reveal.
        FireLights fireLights;
        float moonIntensity = 1.15f, fireFade = 1;
        Quaternion originalSunRotation; LightShadows originalShadows;
        Camera camera;
        Vector3 originalCameraPosition;
        Quaternion originalCameraRotation;
        float originalFov;
        Color originalSun, originalAmbient, originalBackground, oldFogColor;
        float originalIntensity, oldFogDensity;
        bool oldFog;
        FogMode oldFogMode;

        public ArenaSim BoundSim => sim;
        public string Theme => geometry?.CurrentTheme;
        public int EffectCount => (effects?.ActiveCount ?? 0) + (terminalEffects?.ActiveCount ?? 0);
        public bool Travelling => travel.Active;
        public float MorphProgress => sim?.WorldMorphProgress ?? 1;
        public float IntroElapsed => travel.Elapsed;
        public int IntroLitPillars => travel.Active ? WorldIntroPolicy.LitPillars(travel.Elapsed) : 8;
        public void BindRoot(GameRoot value) => root = value;

        void Start()
        {
            if (root == null) root = FindFirstObjectByType<GameRoot>();
            if (root == null || root.config == null) return;
            camera = Camera.main;
            // Disable the authored placeholder arena as a group. Its old pillar observers
            // cannot re-enable a renderer after this view has hidden it.
            originalArena = GameObject.Find("Arena");
            if (originalArena != null) { originalArenaActive = originalArena.activeSelf; originalArena.SetActive(false); }
            sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                originalSun = sun.color; originalIntensity = sun.intensity;
                originalSunRotation = sun.transform.rotation; originalShadows = sun.shadows;
            }
            originalAmbient = RenderSettings.ambientLight;
            if (camera != null)
            {
                originalBackground = camera.backgroundColor;
                originalCameraPosition = camera.transform.position; originalCameraRotation = camera.transform.rotation;
                originalFov = camera.fieldOfView;
                camera.transform.rotation = WorldCameraPolicy.Rotation(); camera.fieldOfView = 40;
            }
            oldFog = RenderSettings.fog; oldFogColor = RenderSettings.fogColor;
            oldFogDensity = RenderSettings.fogDensity; oldFogMode = RenderSettings.fogMode;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .016f;
            art = new WorldArtLibrary(resourceFolder);
            effects = new WorldEffects(transform, art);
            terminalEffects = new WorldEffects(transform, art, "TerminalEffects");
            travelEffects = new WorldEffects(transform, art, "SanctumPullEffects");
            introOverlay = new WorldIntroOverlay();
            Rebind();
        }

        void LateUpdate()
        {
            if (art == null || root == null || root.Sim == null) return;
            if (!ReferenceEquals(sim, root.Sim)) Rebind();
            if (arenaRevision != sim.ArenaRevision) ChangeArena();
            bool bossScene = sim.LivingBoss() != null || sim.State == RunState.BossIntro || sim.State == RunState.BossCombat
                || (sim.Boss != null && sim.State == RunState.Results);
            int index = sim.Setup.Mode == GameMode.Endless ? Math.Max(0, (sim.Wave - 1) / 2) : sim.Encounter;
            string theme = sim.Setup.WorldArenas && !sim.Setup.Tutorial ? sim.Arena.worldTheme : WorldArtPolicy.Theme(index, bossScene);
            if (geometry.CurrentTheme != theme || sceneryIndex != index) { sceneryIndex = index; ApplyTheme(theme, keepFlames: previousGeometry != null); }
            GlitchField.Upload(sim.Morph, sim.Clock.Now, UI.DisplayOptions.ReduceFlashes, WorldLightingPolicy.Seam(sim.Arena.worldTheme));
            geometry.RenderCover(sim); RenderBoss(); effects.Render(sim.Clock.Now, camera);
            if (previousGeometry != null && !travel.Active)
            {
                float blend = sim.WorldMorphProgress;
                previousGeometry.RenderRetiring(sim); previousGeometry.RenderScenery(sim.Morph, sim.Clock.Now);
                geometry.RenderScenery(sim.Morph, sim.Clock.Now);
                BlendLighting(blend);
                if (blend >= .5f && flamesTheme != geometry.CurrentTheme) SpawnFlames(geometry.CurrentTheme);
                if (blend >= 1)
                {
                    previousGeometry.Dispose(); previousGeometry = null;
                    geometry.RenderScenery(null, sim.Clock.Now); // settle every prop fully solid
                }
            }
            RenderPullAndCamera();
            if (root.View != null && root.View.gameObject.activeInHierarchy)
            {
                var player = root.View.PlayerView.transform.position;
                Vector3? boss = bossBody != null && !bossBody.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>().forceRenderingOff
                    ? bossBody.transform.position : (Vector3?)null;
                geometry.RenderActorOcclusion(camera, player, boss);
                previousGeometry?.RenderActorOcclusion(camera, player, boss);
            }
            // Results pause gameplay permanently. Only the death flourish has a separate
            // terminal clock; manual/menu pauses still freeze every animation.
            if (sim.Clock.HasPauseReason(PauseReason.Results) && !sim.Clock.HasPauseReason(PauseReason.Manual)
                && !sim.Clock.HasPauseReason(PauseReason.Menu) && !sim.Clock.HasPauseReason(PauseReason.FocusLost))
                terminalElapsed += Time.unscaledDeltaTime;
            terminalEffects.Render(sim.Clock.Now + terminalElapsed, camera);
            fireLights?.Render(Time.unscaledTime, fireFade);
        }

        void Rebind()
        {
            if (sim != null) sim.Clock.SetPauseReason(PauseReason.WorldTransition, false);
            Unsubscribe(); RestoreBoss(); sim = root.Sim;
            if (sim == null) return;
            bossBody = null; bossId = -1; patternCount = -1; clip = null; hurtUntil = 0; previousStage = BossStage.Recover;
            geometry?.Dispose(); previousGeometry?.Dispose(); previousGeometry = null;
            geometry = new WorldGeometry(transform, sim.Arena, art, root.config.litMaterial);
            arenaRevision = sim.ArenaRevision;
            sceneryIndex = -1; effects.Clear(); terminalEffects.Clear(); travelEffects.Clear(); terminalElapsed = 0; travel.Reset();
            fireFade = 1;
            lastPlayerVisual = Geometry2D.ToWorld(sim.Player.Position);
            cameraFocus = Geometry2D.ToWorld(WorldCameraPolicy.ClampFocus(sim.Player.Position, sim.Arena.bounds));
            if (camera != null) camera.transform.position = cameraFocus + WorldCameraPolicy.Offset;
            sim.Events.EnemyFired += Fired; sim.Events.EnemyDamaged += Damaged;
            sim.Events.EnemyKilled += Killed;
            introOverlay.Clear();
            if (hiddenPlayer != null) hiddenPlayer.forceRenderingOff = false;
        }

        void ChangeArena()
        {
            previousGeometry?.Dispose(); previousGeometry = geometry;
            geometry = new WorldGeometry(transform, sim.Arena, art, root.config.litMaterial);
            arenaRevision = sim.ArenaRevision; sceneryIndex = -1;
            morphFrom = WorldLightingPolicy.For(previousGeometry != null ? previousGeometry.CurrentTheme : sim.Arena.worldTheme);
            ApplyTheme(sim.Arena.worldTheme, keepFlames: sim.WorldMorphing);
            morphTo = WorldLightingPolicy.For(sim.Arena.worldTheme);
            if (sim.WorldMorphing) BlendLighting(0);
            if (sim.WorldMorphing)
            {
                // Live replacement: no camera relocation and no WorldTransition pause.
                geometry.BeginMorphFrom(previousGeometry);
            }
            else
            {
                // Endless cycles also return from the remote Sanctum. Both directions
                // need the same safe pull rather than combat under a travelling camera.
                travel.Begin(true); sim.Clock.SetPauseReason(PauseReason.WorldTransition, true);
                fireFade = sim.ArenaStage == 3 ? 0 : 1; // the Sanctum starts dark
                effects.Clear(); litPillars = 0;
                pullFrom = lastPlayerVisual; pullTo = Geometry2D.ToWorld(sim.Player.Position);
                pullCameraFrom = cameraFocus;
                pullCameraTo = Geometry2D.ToWorld(WorldCameraPolicy.ClampFocus(sim.Player.Position, sim.Arena.bounds));
                nextPullParticle = 0; travelEffects.Clear();
                travelEffects.Spawn("Vortex", pullFrom + Vector3.up * .08f, 6, 0, new Color(.7f, .4f, 1), ground: true, loop: true, cell: 100);
                travelEffects.Spawn("Vortex", pullTo + Vector3.up * .08f, 7, 0, new Color(.65f, .45f, 1), ground: true, loop: true, cell: 100);
            }
        }

        void RenderPullAndCamera()
        {
            bool held = sim.Clock.HasPauseReason(PauseReason.Manual) || sim.Clock.HasPauseReason(PauseReason.Menu)
                || sim.Clock.HasPauseReason(PauseReason.FocusLost);
            if (travel.Active)
            {
                travel.Advance(Time.unscaledDeltaTime, held);
                float age = travel.Elapsed;
                float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / WorldIntroPolicy.Rise));
                // Rise vertically at the departed location. The remote scene is selected
                // only under an opaque curtain; no sideways flight crosses the world.
                var visual = age < WorldIntroPolicy.Rise ? pullFrom + Vector3.up * (10 * rise) : pullTo;
                if (root.View != null) root.View.PlayerView.transform.position = visual;
                lastPlayerVisual = visual;
                cameraFocus = age < WorldIntroPolicy.Rise ? pullCameraFrom + Vector3.up * (2 * rise) : pullCameraTo;
                bool sanctum = sim.ArenaStage == 3;
                introOverlay.Render(age, sim.Config.collector.displayName, held, sanctum);
                if (root.Flow != null)
                {
                    var banner = root.Flow.transform.Find("BossBanner");
                    if (banner != null) banner.gameObject.SetActive(false);
                }
                if (root.View != null)
                {
                    // The dark room reveal must not leave bright aim markers or other
                    // combat indicators floating in the darkness before the boss appears.
                    root.View.gameObject.SetActive(age < WorldIntroPolicy.Rise || WorldIntroPolicy.ShowTitle(age));
                    hiddenPlayer = root.View.PlayerView.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>();
                    hiddenPlayer.forceRenderingOff = age >= WorldIntroPolicy.Rise && !WorldIntroPolicy.ShowTitle(age);
                }
                if (bossBody != null)
                    bossBody.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>().forceRenderingOff = !WorldIntroPolicy.ShowTitle(age);
                if (sanctum)
                {
                    int lit = WorldIntroPolicy.LitPillars(age);
                    float opening = Mathf.Clamp01((age - WorldIntroPolicy.TitleAt) / WorldIntroPolicy.TitleHold);
                    geometry.RenderReveal(lit, opening);
                    if (sun != null) sun.intensity = Mathf.Lerp(0, moonIntensity, opening);
                    fireFade = opening;
                    RenderSettings.fogColor = Color.Lerp(Color.black, morphTo.Fog, opening);
                    while (litPillars < lit)
                    {
                        travelEffects.Spawn("Blue Flame", WorldGeometry.FlameAnchor(sim.Arena, litPillars), 1.4f, age, Color.white, loop: true, fps: 10);
                        litPillars++;
                    }
                }
                if (age < WorldIntroPolicy.Rise && travel.Elapsed >= nextPullParticle && !held)
                {
                    travelEffects.Spawn("Casting", visual + Vector3.up, 2.5f, travel.Elapsed, new Color(.7f, .5f, 1), cell: 100);
                    nextPullParticle = travel.Elapsed + .14f;
                }
                travelEffects.Render(travel.Elapsed, camera);
                if (!travel.Active)
                {
                    // UI-driven practice summons can relocate outside a fixed step.
                    // Seed both interpolation endpoints before ordinary rendering resumes,
                    // including a render frame that arrives before the next simulation tick.
                    if (root.View != null) { root.View.AfterStep(); root.View.BeforeStep(); }
                    if (hiddenPlayer != null) hiddenPlayer.forceRenderingOff = false;
                    if (root.View != null) root.View.gameObject.SetActive(true);
                    if (bossBody != null) bossBody.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>().forceRenderingOff = false;
                    geometry.FinishReveal(); introOverlay.Clear();
                    sim.Clock.SetPauseReason(PauseReason.WorldTransition, false);
                    previousGeometry?.Dispose(); previousGeometry = null; travelEffects.Clear();
                    fireFade = 1;
                    ApplyTheme(sim.Arena.worldTheme);
                    // A short run's Sanctum scene plays first; GameRoot then starts the fight.
                    if (sim.State == RunState.BossIntro && !root.NarrativeHoldsBossIntro) sim.CompleteBossIntro();
                }
            }
            else
            {
                lastPlayerVisual = root.View != null ? root.View.PlayerView.transform.position : Geometry2D.ToWorld(sim.Player.Position);
                var target = Geometry2D.ToWorld(WorldCameraPolicy.ClampFocus(sim.Player.Position, sim.Arena.bounds));
                if (!held) cameraFocus = Vector3.Lerp(cameraFocus, target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            }
            if (camera != null) camera.transform.position = cameraFocus + WorldCameraPolicy.Offset;
        }

        void Unsubscribe()
        {
            if (sim == null) return;
            sim.Events.EnemyFired -= Fired; sim.Events.EnemyDamaged -= Damaged;
            sim.Events.EnemyKilled -= Killed;
        }

        void BlendLighting(float blend)
        {
            var ambient = Color.Lerp(morphFrom.Ambient, morphTo.Ambient, blend);
            RenderSettings.ambientLight = ambient; Shader.SetGlobalColor("_WorldAmbient", ambient);
            RenderSettings.fogColor = Color.Lerp(morphFrom.Fog, morphTo.Fog, blend);
            if (camera != null) camera.backgroundColor = RenderSettings.fogColor;
            if (sun != null)
            {
                sun.color = Color.Lerp(morphFrom.Moon, morphTo.Moon, blend);
                sun.intensity = Mathf.Lerp(morphFrom.MoonIntensity, morphTo.MoonIntensity, blend);
            }
            fireLights?.Tint(Color.Lerp(morphFrom.Fire, morphTo.Fire, blend));
        }

        // Torch or blue flame plus fog drift at the eight anchors. Kept apart from ApplyTheme so
        // a morph can keep the outgoing braziers burning until its halfway point.
        void SpawnFlames(string name)
        {
            effects.Clear(); flamesTheme = name;
            if (travel.Active) return; // the reveal lights each Sanctum pillar independently
            for (int i = 0; i < 8; i++)
            {
                var at = WorldGeometry.FlameAnchor(sim.Arena, i);
                effects.Spawn(name == "Cave" || name == "Sanctum" ? "Blue Flame" : "Torch", at, 1.4f, sim.Clock.Now, Color.white, loop: true, fps: 10);
                effects.Spawn("Fog Drift", at + Vector3.up * .5f, 3, sim.Clock.Now, new Color(.55f, .6f, .66f, .16f), loop: true, fps: 6);
            }
        }

        void ApplyTheme(string name, bool keepFlames = false)
        {
            geometry.SetTheme(name);
            var lighting = WorldLightingPolicy.For(name);
            RenderSettings.ambientLight = lighting.Ambient;
            // PaintedWorld reads ambient from this global, not from the SH probe.
            Shader.SetGlobalColor("_WorldAmbient", lighting.Ambient);
            RenderSettings.fogColor = lighting.Fog;
            if (camera != null) camera.backgroundColor = lighting.Fog;
            moonIntensity = lighting.MoonIntensity;
            if (sun != null)
            {
                sun.color = lighting.Moon; sun.intensity = lighting.MoonIntensity;
                sun.transform.rotation = WorldLightingPolicy.MoonRotation; sun.shadows = LightShadows.Soft;
            }
            fireLights?.Dispose();
            fireLights = new FireLights(transform, sim.Arena, lighting.Fire);
            if (!keepFlames) SpawnFlames(name);
        }

        void RenderBoss()
        {
            EnemyActor actor = null;
            foreach (var e in sim.Enemies) if (e.IsBoss && e.Alive) { actor = e; break; }
            if (actor == null || !art.HasBoss || root.View == null) return;
            if (bossBody == null || bossId != actor.ActorId)
            {
                var target = root.View.transform.Find("Enemies/Boss#" + actor.ActorId);
                if (target == null) return;
                bossBody = target.GetComponent<CharacterView>(); bossId = actor.ActorId; patternCount = -1;
                var sprite = target.Find("Billboard/Sprite").GetComponent<SpriteRenderer>();
                originalBossSprite = sprite.sprite; originalBossScale = sprite.transform.localScale.x;
                bossBody.SetVisualScale(2f);
            }
            var state = actor.Boss;
            string next = WorldArtPolicy.Clip(state.Stage, state.Pattern);
            if (next != clip || patternCount != state.PatternsStarted)
            {
                clip = next;
                // A skipped render frame must not delay the cast animation relative to its hit.
                poseBegan = state.Stage == BossStage.Telegraph || state.Stage == BossStage.Active
                    ? state.TelegraphStartedAt : sim.Clock.Now;
                patternCount = state.PatternsStarted;
            }
            if (state.Pattern == BossPattern.Summon && state.Stage == BossStage.Telegraph && previousStage != BossStage.Telegraph)
                Effect("Vortex", actor.Position, 2.4f, new Color(.7f, .35f, 1), cell: 100);
            if (state.Stage == BossStage.Teleport && previousStage != BossStage.Teleport)
            { beforeTeleport = actor.Position; Effect("Vortex", actor.Position, 2.4f, new Color(1, .35f, .4f), cell: 100); }
            if (previousStage == BossStage.Teleport && state.Stage != BossStage.Teleport)
            { Effect("Teleport", actor.Position, 2.4f, new Color(.8f, .6f, 1)); Effect("Dust Cloud", beforeTeleport, 2, new Color(.5f, .4f, .6f)); }
            previousStage = state.Stage;
            bool hurt = sim.Clock.Now < hurtUntil;
            var frames = art.Boss(hurt ? "Hurt" : clip);
            double began = hurt ? hurtUntil - 5.0 / 12 : poseBegan;
            bool loop = clip == "Idle" || clip == "Run";
            float fps = hurt || loop ? 12f : WorldArtPolicy.AttackFps(state.Stage, state.Pattern, sim.Config.collector);
            if (frames.Length > 0) bossBody.SetSprite(frames[WorldArtPolicy.Frame(sim.Clock.Now - began, frames.Length, fps, !hurt && loop)]);
        }

        void Effect(string name, Vector2 at, float size, Color color, bool ground = false, int cell = 32)
            => effects.Spawn(name, Geometry2D.ToWorld(at, ground ? .065f : .85f), size, sim.Clock.Now, color, ground, cell: cell);

        void Fired(EnemyActor actor)
        {
            if (!actor.IsBoss) return;
            if (actor.Boss.Pattern == BossPattern.Slam)
            { Effect("Shockwave", actor.Position, 3.8f, new Color(.9f, .45f, .5f), true); Effect("Rock Burst", actor.Position, 2.5f, Color.white); }
            else if (actor.Boss.Pattern == BossPattern.Sweep) Effect("Wide Cleave", actor.Position, 3, new Color(1, .85f, .4f), true);
            else if (actor.Boss.Pattern == BossPattern.Summon)
            {
                Effect("Dark Curse", actor.Position, 2, new Color(.7f, .35f, 1));
                foreach (var position in actor.Boss.SummonedPositions)
                    Effect("Teleport", position, 2, new Color(.7f, .45f, 1));
            }
            else Effect("Midnight", actor.Position, 1.2f, Color.white, cell: 100);
        }
        void Damaged(EnemyActor actor, DamageEvent damage)
        { if (!actor.IsBoss) return; hurtUntil = sim.Clock.Now + 5.0 / 12; Effect("Heavy Hit", actor.Position, 1.1f, Color.white); }
        void Killed(EnemyActor actor, DamageEvent damage)
        {
            if (!actor.IsBoss || !art.HasBoss) return;
            terminalEffects.Spawn(art.Boss("Death"), Geometry2D.ToWorld(actor.Position), 2f, sim.Clock.Now, Color.white, false, false, 12);
            terminalEffects.Spawn("Dark Curse", Geometry2D.ToWorld(actor.Position, .85f), 2.2f, sim.Clock.Now, new Color(.8f, .4f, .7f));
        }
        void RestoreBoss()
        {
            if (bossBody != null)
            {
                bossBody.transform.Find("Billboard/Sprite").GetComponent<SpriteRenderer>().forceRenderingOff = false;
                bossBody.SetSprite(originalBossSprite); bossBody.SetVisualScale(originalBossScale);
            }
            bossBody = null; originalBossSprite = null;
        }

        void OnDestroy()
        {
            Unsubscribe(); RestoreBoss();
            if (sim != null) sim.Clock.SetPauseReason(PauseReason.WorldTransition, false);
            if (hiddenPlayer != null) hiddenPlayer.forceRenderingOff = false;
            if (root != null && root.View != null) root.View.gameObject.SetActive(true);
            if (originalArena != null) originalArena.SetActive(originalArenaActive);
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in changed) if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
            // Start can allocate the overlay before a run binds any geometry. Owned
            // resources must still be released when that observer is removed early.
            if (art == null) return;
            if (sun != null)
            {
                sun.color = originalSun; sun.intensity = originalIntensity;
                sun.transform.rotation = originalSunRotation; sun.shadows = originalShadows;
            }
            RenderSettings.ambientLight = originalAmbient;
            Shader.SetGlobalColor("_WorldAmbient", originalAmbient);
            GlitchField.Clear();
            RenderSettings.fog = oldFog; RenderSettings.fogColor = oldFogColor; RenderSettings.fogDensity = oldFogDensity; RenderSettings.fogMode = oldFogMode;
            if (camera != null)
            {
                camera.backgroundColor = originalBackground;
                camera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation); camera.fieldOfView = originalFov;
            }
            effects.Dispose(); terminalEffects.Dispose(); travelEffects.Dispose(); previousGeometry?.Dispose();
            geometry?.Dispose(); fireLights?.Dispose(); introOverlay.Dispose(); art.Dispose();
        }
    }
}
