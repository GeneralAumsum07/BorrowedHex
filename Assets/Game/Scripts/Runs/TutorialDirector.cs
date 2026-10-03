using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>The tutorial's lessons, in the order they are taught (D86).</summary>
    public enum TutorialStep { Move, Dash, Capture, Slots, Parry, Evolve, Complete }

    /// <summary>
    /// Phase 14: the scripted tutorial. It runs inside an ordinary sandbox sim (so it can never
    /// submit anything to the profile) and only WATCHES the sim through its events plus a little
    /// polling — the combat rules are exactly the ones the real game uses, so what the tutorial
    /// teaches is what the player will meet.
    ///
    /// Each lesson has a goal the player must actually perform (catch, fire, swap, parry), not
    /// just "kill the enemy", because most of these enemies could be outlasted without learning
    /// the verb. Lessons cannot be failed: the clock is frozen (RunSetup.Tutorial), and if a
    /// lesson's enemies all die before its goal is met, the same roster is summoned again.
    ///
    /// Between lessons there is a short "Nice!" beat with the arena quiet, so the next prompt is
    /// read before anything new walks in.
    /// </summary>
    public sealed class TutorialDirector
    {
        readonly ArenaSim sim;

        public TutorialStep Step { get; private set; } = TutorialStep.Move;
        /// <summary>The instruction for the player, already worded for the HUD.</summary>
        public string Prompt { get; private set; } = "";
        /// <summary>A short progress line under the prompt ("2/3"), or empty.</summary>
        public string Progress { get; private set; } = "";
        /// <summary>1-based lesson number and how many there are, for the "LESSON n/5" header.</summary>
        public int LessonNumber => Mathf.Min((int)Step + 1, LessonCount);
        public const int LessonCount = 6;
        public bool IsComplete => Step == TutorialStep.Complete;
        /// <summary>True during the quiet beat after a lesson is passed.</summary>
        public bool Celebrating => celebrateUntil > sim.Clock.Now;

        /// <summary>The movement lesson's current target, for the view to draw. Null otherwise.</summary>
        public Vector2? Marker => Step == TutorialStep.Move && !Celebrating && markerIndex < Markers.Length
            ? Markers[markerIndex] : (Vector2?)null;
        /// <summary>How close counts as "on" a marker. Generous: the lesson is about WASD, not precision.</summary>
        public const float MarkerRadius = 1.1f;

        // Spread around the arena and clear of the four pillars (GameConfig.arena), so walking to
        // each one crosses the screen in a different direction. The player starts at (0, -4).
        static readonly Vector2[] Markers = { new Vector2(0f, 3f), new Vector2(-9f, 0f), new Vector2(9f, 0f) };
        public const int DashesNeeded = 3;
        public const int ParriesNeeded = 2;
        /// <summary>
        /// The evolution lesson lets its enemies be seen in their normal form for this long after
        /// their spawn warning ends, then evolves them on the spot (D87). Long enough to register
        /// "that is an Acolyte", short enough that nobody waits.
        /// </summary>
        public const double EvolveAfter = 1.0;
        const double BeatSeconds = 1.6;
        // A summon has a spawn warning; give the roster a moment before judging it "all dead".
        const double RespawnGrace = 0.5;

        int markerIndex, dashes, parries, releases, slotReleases, evolvedKills;
        bool captured, bothSlotsHeld, swapped;
        double celebrateUntil = double.NegativeInfinity;
        TutorialStep pendingStep;
        bool pendingSpawn;
        double rosterSpawnedAt;
        readonly List<EnemyActor> roster = new List<EnemyActor>();
        readonly HashSet<int> slotsFiredFrom = new HashSet<int>();

        public TutorialDirector(ArenaSim sim)
        {
            this.sim = sim;
            // Events count only what the player did in the CURRENT lesson; each handler checks
            // the step so, e.g., a dash during the capture lesson is not credited anywhere.
            sim.Events.Dashed += (_, __) => { if (Step == TutorialStep.Dash && !Celebrating) dashes++; };
            sim.Events.ShotCaptured += (packet, shot, at, result) => captured = true;
            sim.Events.PacketReleased += OnReleased;
            sim.Events.SlotSwapped += _ => { if (Step == TutorialStep.Slots && bothSlotsHeld) swapped = true; };
            sim.Events.StrikeParried += (enemy, at) => { if (Step == TutorialStep.Parry) parries++; };
            // Only an EVOLVED kill counts: the lesson is "an overstayer is tougher, beat it anyway".
            sim.Events.EnemyKilled += (enemy, dmg) => { if (Step == TutorialStep.Evolve && enemy.Overstayed) evolvedKills++; };
            UpdateText();
        }

        void OnReleased(CapturedPacket packet, int rootReleaseId)
        {
            releases++;
            // The slot lesson wants a shot fired from EACH slot, which forces at least one swap
            // between fires (firing always uses the selected slot).
            if (Step == TutorialStep.Slots && swapped) slotsFiredFrom.Add(packet.Slot);
        }

        /// <summary>Called once per sim tick from the run flow (sandbox branch).</summary>
        public void Tick(double now)
        {
            if (IsComplete) return;
            if (Celebrating) return;
            if (pendingSpawn) { pendingSpawn = false; Begin(pendingStep); }

            switch (Step)
            {
                case TutorialStep.Move:
                    if ((sim.Player.Position - Markers[markerIndex]).sqrMagnitude <= MarkerRadius * MarkerRadius)
                    {
                        markerIndex++;
                        if (markerIndex >= Markers.Length) Pass(TutorialStep.Dash);
                    }
                    break;
                case TutorialStep.Dash:
                    if (dashes >= DashesNeeded) Pass(TutorialStep.Capture);
                    break;
                case TutorialStep.Capture:
                    if (captured && releases > 0 && RosterDead()) Pass(TutorialStep.Slots);
                    else KeepRosterAlive(now, captured && releases > 0);
                    break;
                case TutorialStep.Slots:
                    // Polled, not evented: "both held at once" is a state, and a packet can be
                    // created by a catch event before or after the other slot is fired.
                    if (!bothSlotsHeld && StoredCount() >= 2) bothSlotsHeld = true;
                    bool slotsGoal = bothSlotsHeld && swapped && slotsFiredFrom.Count >= 2;
                    if (slotsGoal && RosterDead()) Pass(TutorialStep.Parry);
                    else KeepRosterAlive(now, slotsGoal);
                    break;
                case TutorialStep.Parry:
                    bool parryGoal = parries >= ParriesNeeded;
                    if (parryGoal && RosterDead()) Pass(TutorialStep.Evolve);
                    else KeepRosterAlive(now, parryGoal);
                    break;
                case TutorialStep.Evolve:
                    // Overstay never fires on its own in a tutorial (ArenaSim.OverstaySeconds), so
                    // evolution here is always this explicit call, made once each enemy is active.
                    foreach (var e in roster)
                        if (e.Alive && !e.Overstayed && now >= e.ActiveAt + EvolveAfter) sim.EvolveEnemy(e, now);
                    bool evolveGoal = evolvedKills >= roster.Count;
                    if (evolveGoal && RosterDead()) Pass(TutorialStep.Complete);
                    else if (RosterDead() && now - rosterSpawnedAt >= RespawnGrace) { evolvedKills = 0; Respawn(); }
                    break;
            }
            UpdateText();
        }

        /// <summary>Lesson passed: celebrate, clear leftovers, then start the next one after the beat.</summary>
        void Pass(TutorialStep next)
        {
            celebrateUntil = sim.Clock.Now + BeatSeconds;
            // Leftover hostile shots would otherwise land during the "Nice!" beat; the player's
            // own packets are kept (ClearArena never touches them).
            sim.ClearArena();
            roster.Clear();
            if (next == TutorialStep.Complete) { Step = next; UpdateText(); return; }
            pendingStep = next;
            pendingSpawn = true;
            Prompt = "Nice!";
            Progress = "";
        }

        void Begin(TutorialStep step)
        {
            Step = step;
            switch (step)
            {
                case TutorialStep.Capture: SpawnRoster(ActorCategory.Acolyte); break;
                case TutorialStep.Slots: SpawnRoster(ActorCategory.Acolyte, ActorCategory.SiegeFamiliar); break;
                case TutorialStep.Parry: SpawnRoster(ActorCategory.Pursuer); break;
                // One of each fighting style the player has learned to answer: a caster to catch
                // from and a Pursuer to parry, both stronger and faster once evolved.
                case TutorialStep.Evolve: SpawnRoster(ActorCategory.Acolyte, ActorCategory.Pursuer); break;
            }
        }

        readonly List<ActorCategory> rosterKinds = new List<ActorCategory>();

        void SpawnRoster(params ActorCategory[] kinds)
        {
            rosterKinds.Clear();
            rosterKinds.AddRange(kinds);
            Respawn();
        }

        void Respawn()
        {
            roster.Clear();
            foreach (var k in rosterKinds) roster.Add(sim.SummonEnemy(k));
            rosterSpawnedAt = sim.Clock.Now;
        }

        bool RosterDead()
        {
            foreach (var e in roster) if (e.Alive) return false;
            return true;
        }

        /// <summary>
        /// Lessons cannot be failed: if every enemy of the lesson is gone but the player has not
        /// yet done the thing being taught, bring the same enemies back. Once the goal is met the
        /// roster is left alone, so the player finishes the fight they are in.
        /// </summary>
        void KeepRosterAlive(double now, bool goalMet)
        {
            if (goalMet || now - rosterSpawnedAt < RespawnGrace || !RosterDead()) return;
            Respawn();
        }

        int StoredCount()
        {
            int n = 0;
            foreach (var p in sim.Packets.Packets) if (p.Status == PacketStatus.Stored || p.Status == PacketStatus.Collecting) n++;
            return n;
        }

        void UpdateText()
        {
            if (Celebrating && !IsComplete) return;
            switch (Step)
            {
                case TutorialStep.Move:
                    Prompt = "Move with W A S D. Walk onto the glowing marker.";
                    Progress = $"{markerIndex}/{Markers.Length}";
                    break;
                case TutorialStep.Dash:
                    Prompt = "Press SPACE to dash: a quick burst in the direction you are moving.";
                    Progress = $"{Mathf.Min(dashes, DashesNeeded)}/{DashesNeeded}";
                    break;
                case TutorialStep.Capture:
                    Prompt = !captured
                        ? "An Acolyte! Aim at its bolt with the mouse and press LEFT MOUSE as it reaches you to catch it."
                        : releases == 0
                            ? "Caught! The spell is yours now. A fresh hex is unstable for a moment — then aim at the Acolyte and press RIGHT MOUSE to fire it back."
                            : "Keep catching and firing until the Acolyte falls.";
                    Progress = "";
                    break;
                case TutorialStep.Slots:
                    // D98: under the hand rule (D89) the second slot only fills if the player
                    // pockets the first hex with Q, so the lesson teaches that verb by name.
                    Prompt = !bothSlotsHeld
                        ? "You can only catch with an EMPTY hand. Holding a hex? Press Q to pocket it (it freezes), then catch again. Fill both slots."
                        : !swapped
                            ? "Both slots full. RIGHT MOUSE fires the hex in your hand. Press Q to swap to the other one."
                            : slotsFiredFrom.Count < 2
                                ? "Now fire from both slots: RIGHT MOUSE, then Q, then RIGHT MOUSE again."
                                : "Use everything you have learned to defeat both enemies.";
                    Progress = !bothSlotsHeld ? $"slots filled {Mathf.Min(StoredCount(), 2)}/2"
                        : swapped ? $"fired from {slotsFiredFrom.Count}/2 slots" : "";
                    break;
                case TutorialStep.Parry:
                    Prompt = parries < ParriesNeeded
                        ? "A Pursuer strikes up close. Aim at it and press LEFT MOUSE just as it strikes to parry and hit back."
                        : "Well parried. Finish the Pursuer.";
                    Progress = $"parries {Mathf.Min(parries, ParriesNeeded)}/{ParriesNeeded}";
                    break;
                case TutorialStep.Evolve:
                    bool anyEvolved = false;
                    foreach (var e in roster) if (e.Overstayed) anyEvolved = true;
                    Prompt = !anyEvolved
                        ? "Enemies left alive too long EVOLVE. Watch: these two are about to..."
                        : "They evolved: more health, faster, quicker attacks. Take both down!";
                    Progress = $"evolved defeated {Mathf.Min(evolvedKills, 2)}/2";
                    break;
                case TutorialStep.Complete:
                    Prompt = "Tutorial complete! You are ready for a real run.";
                    Progress = "";
                    break;
            }
        }

        // ---- Test hooks ----------------------------------------------------------------------
        /// <summary>The live enemies of the current lesson (tests and the view read this).</summary>
        public IReadOnlyList<EnemyActor> Roster => roster;
        /// <summary>The movement lesson's marker positions, in order.</summary>
        public static IReadOnlyList<Vector2> MarkerPositions => Markers;
    }
}
