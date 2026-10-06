# Borrowed Hex UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild every menu, panel and HUD element as a pixel-art dark-fantasy interface (ornate gold-on-ink frames, gothic pixel lettering, a ritual skill seal) without changing gameplay, rewards, saves or platform behaviour.

**Architecture:** The UI stays code-built uGUI. Three layers are added under it:
1. **Art.** A `UiArtImporter` copies the licensed pixel UI art and fonts into a git-ignored `Resources/UiArt`, slices it into 9-slice sprites, and recolours what does not fit.
2. **Skin.** A runtime `UiSkin` loads that art. A public checkout without the art falls back to flat colours.
3. **Kit.** A themed `Ui` kit (frames, button tiers, rows, tabs, scroll views, tooltips) and a `ScreenStack` router own focus and Esc for every screen.

Each screen is then rebuilt on the kit. Every rule the screens depend on (map geometry, slot cue state, menu routing, record rows) lives in a small pure class with its own EditMode tests.

**Tech Stack:** Unity 6000.3.25f1, URP, C#, uGUI with Legacy `Text` (no TMP, to keep the WebGL build lean), Input System UI module, NUnit EditMode and PlayMode tests.

**Spec:** `Docs/superpowers/specs/2026-10-04-ui-redesign.md` (GPT's rough spec). Section 0 below records where this plan deliberately departs from it, and why. The executor reads both documents. Where they disagree, **this plan wins**.

---

## 0. Changes to the spec (theme pass)

Owner direction (4 Oct 2026): *"dark mage, nightmarish world, pixel art, fantasy RPG"*. The owner also supplied two pixel UI packs and the alagard font, and allowed changes to the spec. The spec's *structure* is sound and is kept: three destinations, Character tabs, the Broken Accord, the overcharge border, and the per-screen cleanups. Its *visual language* reads as a tasteful web app, not a pixel RPG, so the following decisions replace it:

| # | Spec said | This plan does | Why |
|---|---|---|---|
| 0.1 | Cinzel headings + Source Sans 3 body | **alagard** for the logo, headings and button captions. **m5x7** (already in the project for callouts) for body text, numbers and hints. | Smooth vector type next to 32 px pixel frames looks pasted on. alagard is a gothic bitmap face made for fantasy games. m5x7 is already licensed in the project and matches the combat callouts, so the HUD and the world speak with one voice. |
| 0.2 | Flat palette, "restrained engraved ornament" drawn by us | **Dark Ages UI** (Hypnobius) provides the frames, plates, bars, dividers and filigree. **Tiny RPG – Dark Dwellers** (CC0) provides the cursor marker, arrow, close and options buttons, and tabs, **gold-mapped** to match. | Hand-drawn pixel ornament from a real artist beats anything procedural. Dark Ages' gold-on-charcoal *is* the spec's "ink and aged gold" direction. |
| 0.3 | Palette `#C8A66A` gold, `#201B2C` plum | Golds are taken **from the art's own palette** (Honey Gold `#DCC47C`, Camel `#C19149`), so text matches the frames. A **hex-violet** accent `#9B6BE0` marks magic and focus. A **blood red** `#B3373F` marks Life. | Mismatched golds between text and frame read as amateur. Violet ties the UI to the mage. Blood ties Life to the "borrowed existence" drain. |
| 0.4 | Menus sit over the idle arena | Menus also get a **nightmare layer**: a heavy vignette, slow drifting ash and embers (existing `Ash Fall`, `Embers Ambient` and `Dust Motes` sheets), and a slow violet "breath" on the title. All of it is static under Reduce flashes. | This is the cheapest single change that makes the front end feel like a haunted place instead of a settings page. |
| 0.5 | Icons left unspecified | **Code-drawn 12×12 pixel glyphs** (`UiGlyphs`) for the 12 skills, 4 branch emblems, 7 upgrades, 3 hex payloads and 6 slot states, using the same string-bitmap pattern as `PixelSprites`. | There is no licensed icon set in hand. Code glyphs are diffable, need no download, and share one 4-colour ramp, so they look like one set. A downloaded icon pack can replace them later behind the same `UiGlyphs.Get(id)` call. |
| 0.6 | "Thin broken circular engravings, curved connections" | The rings and paths are drawn as **pixel geometry**: runtime textures with one-art-pixel lines and Bresenham arcs, and dotted paths of 2×2 art-pixel dots. They are point-filtered like everything else. | Anti-aliased vector rings inside a pixel UI are the most common way such screens look off. |
| 0.7 | Screen changes not specified | Every screen change goes through `ScreenStack`, with a **0.12 s fade** (unscaled time) and focus restore. Buttons show a **gold pointer marker** (a Dark Dwellers cursor, gold-mapped) beside the focused control. | The spec asks for consistent focus. A visible pointer makes keyboard focus readable in a pixel UI, where a thin outline vanishes. |
| 0.8 | Hover tooltips only | Tooltips appear on hover **and** on focus, on a parchment strip, after a 0.35 s delay. | The spec requires keyboard reachability. Parchment is the one light surface, so a tooltip never reads as another panel. |

Not changed from the spec: the mastery gates (2/4/7), costs, prerequisites, refunds, every domain API, the profile schema, the menu entry keys (they are re-routed), PaidClickGuard, the default-focus rules, WebGL's Esc/P and display behaviour, and the overcharge rules (§2 of the spec, copied exactly into Task 14).

### Open questions (each blocks only the task named)

- **Q1, alagard licence. Blocks shipping, not development.** The download has no licence file. Font aggregators ([FontRiver](https://www.fontriver.com/font/alagard/), [Fonts In Use](https://fontsinuse.com/typefaces/215717/alagard)) describe it as free for commercial use with credit to Hewett Tsoi. That is *secondary* evidence. Before release, confirm on the author's own page and record the exact terms in `Docs/WORLD_ART_HANDOFF.md`. Until then it is git-ignored like all licensed art (Task 1), so nothing is redistributed.
- **Q2, body font.** The default is m5x7, which is already in the project. The alternative is **m6x11** by the same author (Daniel Linssen): taller and easier to read for long text such as Records and Details. It is a separate download (itch.io, roughly 10 KB), so the owner must approve it file by file. The plan is written so that swapping it in is a one-line `UiFonts.BodyFile` change.
- **Q3, Dark Ages panel interior colour.** The pack's interior is a greenish charcoal (`#2E322A`). Task 1 imports it unchanged and adds a capture checkpoint. If it reads green against the violet world, the fix is one `Recolour` entry (a hue shift toward violet), decided with the owner looking at the capture. Nothing else in the plan depends on the answer.

---

## Global Constraints

- **Presentation only.** No edits to `Runs/`, `Combat/`, `Progression/`, `Data/` or `Enemies/` code, except the one read-only accessor named in Task 10, if needed. No profile schema change. No new `PauseReason`.
- **Domain APIs unchanged:** `SkillTree.*`, `Mastery.*`, `CaptureStyles.*`, `Achievements.*`, `Records.*`, `Loadout.Resolve`, `UpgradeInfo.*`, `Cheats.*`, `DisplayOptions.*`, and the sim calls `ChooseUpgrade`, `ContinueFromUpgrade`, `RetireRun`, `TakeCost`, `TakeCostFraction`, `CanSwap`, `UpgradesLocked`.
- **Menu entry keys keep working through `MainMenu.Press(key)`:** `play_short`, `tutorial`, `endless`, `practice`, `mastery`, `style`, `records`, `settings`, `cheats`, `quit`. `quit` is absent on WebGL.
- **Test-facing names kept:**
  - `RunFlowPanels` children: `UpgradeChoice/Panel/Card{i}/{Text,Main,Swap}`, `Back`, `Results/Panel/{Body,Progress}`.
  - `SkillTreePanel` child `Panel` holds 12 `Node_*` children.
  - `Hud.PauseButton`, `Hud.ResetButton`, `Flow.AgainLabel`, `StylePanel.CardBody`, `Main.IsOpen`, `Menu.IsOpen`, `Tree.IsOpen`, `Tree.Show/Hide/Respec`.
  - `Tree.Click(id)` changes meaning (it selects instead of buying). Task 10 updates its one test and adds `Tree.UnlockSelected()`.
- **PaidClickGuard stays 0.35 s on unscaled time.** Continue stays the default focus on offers, Back in the swap step, and Cancel in confirm dialogs.
- **Licensed art and fonts never enter git.** They go only into `Assets/Game/Resources/UiArt/` (git-ignored), rebuilt by `UiArtImporter` from `~/Downloads/UI_elements`. A checkout without them must compile, pass every test, and show a readable flat-colour UI. Tests that need the art guard with `UiSkin.HasArt`.
- **Pixel rules:**
  - Every UI sprite and font atlas is point-filtered.
  - Sprites import at **50 pixels per unit**, so one art pixel is two reference pixels (exact at 1080p).
  - Font sizes are integer multiples of each font's native size (measured in Task 2).
  - No rotation and no non-uniform scale on pixel art.
- **Reduce flashes:**
  - With it on, nothing in the UI pulses, blinks or flashes.
  - Fades are allowed: they are not flashes.
  - This also fixes the existing tutorial prompt gold flash, as the spec requires.
- **Copy:** use "Life" for the drained resource in every player-facing string. Numbers come from live tuning, never literals.
- **Overcharge colour is exactly `FeedbackColors.Overcharge`.** Menu golds never stand in for it.
- **Standing repo rules:**
  - Commit only when the owner asks, and never push.
  - No Claude or tool attribution anywhere.
  - Stage named paths only, and run `git diff --cached --name-only` before every commit.
  - The message goes in `.superpowers/msg.txt` and is committed with `git commit -q -F .superpowers/msg.txt`.
  - Never stage the owner's in-flight files (see the session's never-stage list). Stage `ArenaView.cs`, if touched, via `stage-mine.sh`.
- **Gate:** `bash .superpowers/strict.sh both <tag>` prints `STRICT GATE GREEN` before any task is called done. Filtered runs: `bash .superpowers/rt.sh editor <Filter>` and `bash .superpowers/rtp.sh <Filter>`.

## Review Focus

These are the inputs the spec implies but no screen-level test naturally hits. Each has its pinning test in the named task.

1. **A public checkout with no `UiArt` folder.** Every screen must still build, fit and be readable, without null-reference exceptions from missing sprites or fonts. Pinned in Task 2 (`UiSkinTests.MissingArtFallsBackToFlatColour`) and Task 18 (the layout sweep runs with the skin forced off).
2. **UI scale 130% at 1280×720.** This is the smallest effective canvas the game allows: a 1477×831 reference area. Every screen must fit there with no text spill. Pinned in Task 18 (`LayoutSweep` matrix). Each screen task also runs its own layout test at that corner.
3. **Esc pressed during a fade or with a confirm dialog open.** Esc must close the top-most thing only, never two layers. The run must never unpause while a dialog is up. Pinned in Task 4 (`ScreenStackTests.EscDuringFadeClosesOnlyTheTop`) and Task 12 (`PauseConfirmTests.EscOnConfirmReturnsToPauseNotToTheRun`).
4. **Two hexes overcharged at once, with the selection swapped by Q while paused.** Only the selected slot pulses, both stay gold, and the pulse phase does not jump on resume. Pinned in Task 14 (`SlotCueTests`).
5. **The skill tree's purchase button double-pressed, or pressed with 0 points.** Exactly one point is spent and one node bought. The button reads the reason it is disabled. Pinned in Task 10 (`SkillTreePanelTests.DoubleUnlockSpendsOnePoint`).

---

## File structure

**New: art pipeline (Editor)**
- `Assets/Game/Editor/UiArtImporter.cs`: the sources list, sprite rects and borders, gold-map recolour, and the import menu item.

**New: runtime skin and kit (`Assets/Game/Scripts/UI/Kit/`)**
- `UiPalette.cs`: the colour tokens.
- `UiFonts.cs`: font loading, point filtering, the measured native sizes, and type roles.
- `UiSkin.cs`: loads `Resources/UiArt` sprites by name, exposes `HasArt`, and falls back.
- `UiGlyphs.cs`: code-drawn 12×12 icons.
- `UiKit.cs`: the frame, button tiers, rows, tabs, scroll view and pointer marker builders. These are static, like `Ui`.
- `UiTooltip.cs`: the hover and focus tooltip MonoBehaviour.
- `ScreenStack.cs`: the foreground screen router (focus, Esc, fades).
- `PixelGeometry.cs`: runtime textures for rings, arcs and dotted paths.
- `Atmosphere.cs`: the menu vignette, ash and embers, and title breath.

**New: pure logic (tested without scenes)**
- `UI/Logic/MenuRouting.cs`: maps legacy entry keys to destinations.
- `UI/Logic/AccordLayout.cs`: the Broken Accord node positions and path geometry.
- `UI/Logic/SlotCue.cs`: per-slot visual state (selection marker, overcharge border, pulse alpha, labels).
- `UI/Logic/RecordRows.cs`: record rows, achievement filters and summaries.
- `UI/Logic/HudText.cs`: the objective line and upgrade chips.

**New: screens**
- `UI/Screens/CharacterScreen.cs`: header and tabs, hosting the skill and style views.
- `UI/Screens/TrainingMenu.cs`
- `UI/Screens/ConfirmDialog.cs`
- `UI/Screens/PracticeDrawer.cs`

**Rewritten in place** (names kept so `GameRoot` and the tests still bind):
- `UI/Ui.cs`: it keeps its API and delegates styling to `UiKit`.
- The panels: `MainMenu.cs`, `SettingsPanel.cs`, `CheatsPanel.cs`, `StylePanel.cs`, `SkillTreePanel.cs`, `RecordsPanel.cs`, `PauseMenu.cs`, `GameplayHud.cs`, `PacketIndicator.cs`, `RunFlowPanels.cs`, `TutorialPanel.cs`.

**Modified**
- The `Presentation/GameRoot*.cs` partials (routing through `ScreenStack`, menu entries, Practice drawer).
- `Presentation/Feedback/CalloutText.cs`: it uses `UiFonts.PointFilter` instead of its private copy.
- `.gitignore`
- `Docs/WORLD_ART_HANDOFF.md`
- The spec (a pointer to §0 of this plan).

**Tests (new):** `UiArtImporterTests`, `UiSkinTests`, `UiFontsTests`, `UiGlyphsTests`, `UiKitTests`, `ScreenStackTests`, `MenuRoutingTests`, `AccordLayoutTests`, `SlotCueTests`, `RecordRowsTests`, `HudTextTests` (EditMode). `LayoutSweepTests`, `PauseConfirmTests` and `SkillTreePanelTests` (PlayMode).
**Tests (modified):** `LayoutTests`, `GameRootPlayModeTests`, `StyleTests`.

---

## Phase A: Foundation

### Task 1: UI art import (licensed packs to a git-ignored Resources folder)

**Files:**
- Create: `Assets/Game/Editor/UiArtImporter.cs`
- Create: `Assets/Game/Tests/EditMode/UiArtImporterTests.cs`
- Modify: `.gitignore` (add `/Assets/Game/Resources/UiArt/` and `/Assets/Game/Resources/UiArt.meta`)
- Modify: `Docs/WORLD_ART_HANDOFF.md` (licence table rows)

**Interfaces:**
- Produces:
  - `UiArtImporter.Destination` = `"Assets/Game/Resources/UiArt"`.
  - `UiArtImporter.Sources()`, typed `Dictionary<string,string>`, maps a destination file name to its path under the downloads folder.
  - `UiArtImporter.GoldMaps()`, typed `Dictionary<string,string>`, maps a destination file to a source destination file.
  - `UiArtImporter.GoldMap(Color32[] px)`, which recolours in place.
  - `UiArtImporter.Ramp`, typed `Color32[4]`.
- Later tasks load these files by name through `UiSkin` (Task 2).

Why a gold *map* and not the hue shift `WorldArtImporter` uses: Dark Dwellers is saturated violet-magenta. Rotating its hue gives another saturated colour, never the desaturated antique gold of Dark Ages. A gradient map throws away the hue, keeps each pixel's *relative brightness* (so the shading and bevels survive), and paints that brightness onto the Dark Ages ramp.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.IO;
using BorrowedHex.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    // The UI packs are licensed and git-ignored; Sources() is the committed recipe for rebuilding
    // them. These tests pin the recipe and the recolour maths, not the presence of the files.
    public class UiArtImporterTests
    {
        [Test]
        public void EverySourceHasAnExtensionAndADistinctDestination()
        {
            var s = UiArtImporter.Sources();
            Assert.Greater(s.Count, 10);
            foreach (var pair in s)
            {
                StringAssert.EndsWith(Path.GetExtension(pair.Value), pair.Key, $"{pair.Key} keeps its source type");
                Assert.IsFalse(pair.Key.Contains("/"), "flat destination folder");
            }
        }

        [Test]
        public void GoldMapsReadOnlyFilesThatAreImported()
        {
            foreach (var pair in UiArtImporter.GoldMaps())
                Assert.IsTrue(UiArtImporter.Sources().ContainsKey(pair.Value), $"{pair.Key} maps a copied file");
        }

        [Test]
        public void GoldMapKeepsAlphaAndBrightnessOrder()
        {
            // Three violet pixels of rising brightness plus one clear pixel.
            var px = new[] { new Color32(40, 20, 70, 255), new Color32(90, 50, 150, 255),
                             new Color32(200, 160, 255, 128), new Color32(255, 0, 255, 0) };
            UiArtImporter.GoldMap(px);
            Assert.AreEqual(255, px[0].a); Assert.AreEqual(128, px[2].a); Assert.AreEqual(0, px[3].a);
            float L(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            Assert.Less(L(px[0]), L(px[1])); Assert.Less(L(px[1]), L(px[2]));
            // The darkest opaque pixel lands on the ramp's ink, the brightest on its ivory.
            Assert.AreEqual(UiArtImporter.Ramp[0], px[0]);
            Assert.AreEqual(UiArtImporter.Ramp[3].r, px[2].r);
        }

        [Test]
        public void GoldMapOfAFlatSheetDoesNotDivideByZero()
        {
            var px = new[] { new Color32(80, 40, 120, 255), new Color32(80, 40, 120, 255) };
            UiArtImporter.GoldMap(px);
            Assert.AreEqual(px[0], px[1]);
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure (`UiArtImporter` does not exist)**

Run: `bash .superpowers/uc.sh`
Expected: an `error CS0103` / `CS0246` naming `UiArtImporter`.

- [ ] **Step 3: Implement the importer**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BorrowedHex.EditorTools
{
    /// <summary>
    /// Rebuilds the git-ignored UI art library from the owner's downloads (Downloads/UI_elements).
    /// Same contract as WorldArtImporter: validate every input before touching anything, import
    /// through Unity, never hand-edit .meta YAML. Sprites are NOT sliced here: UiSkin cuts them at
    /// runtime with Sprite.Create (as WorldArtLibrary does), so the rects live in one committed,
    /// unit-tested table and this importer only has to make the textures pixel-exact.
    /// </summary>
    public static class UiArtImporter
    {
        public const string Destination = "Assets/Game/Resources/UiArt";
        const string DarkAges = "DarkAgesUi_v1.0/DarkAgesUi_v1.0/";
        const string Dwellers = "20251126darkDwellers_v_1_0/";

        [MenuItem("Borrowed Hex/Art/Import UI Downloads")]
        public static void ImportDefault() => Import(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads/UI_elements"));

        /// <summary>Destination file name -> path under the downloads folder.</summary>
        public static Dictionary<string, string> Sources() => new Dictionary<string, string>
        {
            // Hypnobius, Dark Ages UI: commercial use and modification allowed, credit optional,
            // no redistribution or resale of the asset itself (LICENSE.txt). Git-ignored for that reason.
            ["DarkAges.png"] = DarkAges + "32x32-Tilesheet.png",
            // Gabriel "tiopalada" Lima, Tiny RPG - Dark Dwellers: CC0 1.0. Copied raw, then gold-mapped.
            ["DwTabRaw.png"] = Dwellers + "20251117darkDwellersTabA1-Sheet.png",
            ["DwPointerRaw.png"] = Dwellers + "20251118darkDwellersHorizontalCursourA1-Sheet.png",
            ["DwCloseRaw.png"] = Dwellers + "20251125closeButton1-Sheet.png",
            ["DwOptionsRaw.png"] = Dwellers + "20251125optionsButton1-Sheet.png",
            ["DwLeftRaw.png"] = Dwellers + "20251125leftArrowButton1-Sheet.png",
            ["DwRightRaw.png"] = Dwellers + "20251125rightArrowButton1-Sheet.png",
            ["DwUpRaw.png"] = Dwellers + "20251125upArrowButton1-Sheet.png",
            ["DwDownRaw.png"] = Dwellers + "20251125downArrowButton1-Sheet.png",
            ["DwPortraitRaw.png"] = Dwellers + "20251125portraitFrameA.png",
            ["DwMouseRaw.png"] = Dwellers + "20251124mouseSmall1-Sheet.png",
            // Hewett Tsoi, alagard. Licence TBD (plan Q1): git-ignored until confirmed.
            ["alagard.ttf"] = "alagard/alagard.ttf",
        };

        /// <summary>Gold-mapped sheet -> the copied raw sheet it is made from.</summary>
        public static Dictionary<string, string> GoldMaps() => new Dictionary<string, string>
        {
            ["DwTab.png"] = "DwTabRaw.png", ["DwPointer.png"] = "DwPointerRaw.png",
            ["DwClose.png"] = "DwCloseRaw.png", ["DwOptions.png"] = "DwOptionsRaw.png",
            ["DwLeft.png"] = "DwLeftRaw.png", ["DwRight.png"] = "DwRightRaw.png",
            ["DwUp.png"] = "DwUpRaw.png", ["DwDown.png"] = "DwDownRaw.png",
            ["DwPortrait.png"] = "DwPortraitRaw.png", ["DwMouse.png"] = "DwMouseRaw.png",
        };

        /// <summary>
        /// Ink, Camel, Honey Gold, ivory. The middle two are Dark Ages' own published swatches
        /// (#C19149, #DCC47C) so mapped pieces sit next to its frames without a seam in tone.
        /// </summary>
        public static readonly Color32[] Ramp =
        {
            new Color32(0x15, 0x12, 0x1C, 255), new Color32(0xC1, 0x91, 0x49, 255),
            new Color32(0xDC, 0xC4, 0x7C, 255), new Color32(0xF2, 0xE8, 0xC9, 255),
        };
        // Where Camel and Honey sit on the 0..1 brightness axis. Camel low enough that a mid
        // bevel reads gold rather than mud; tuned by eye on the tab and pointer sheets.
        static readonly float[] Stops = { 0f, 0.45f, 0.75f, 1f };

        static float Luma(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>
        /// Repaint opaque pixels onto <see cref="Ramp"/> by their brightness, normalised over the
        /// sheet's own opaque range (a dark sheet would otherwise map entirely to ink). Alpha is kept.
        /// </summary>
        public static void GoldMap(Color32[] px)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in px) if (c.a > 0) { float l = Luma(c); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
            float span = hi - lo;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                float t = span < 1e-3f ? 0.5f : (Luma(px[i]) - lo) / span;
                int k = t >= Stops[2] ? 2 : t >= Stops[1] ? 1 : 0;
                float u = (t - Stops[k]) / (Stops[k + 1] - Stops[k]);
                var c = (Color32)Color.Lerp(Ramp[k], Ramp[k + 1], u);
                c.a = px[i].a;
                px[i] = c;
            }
        }

        public static int Import(string source)
        {
            var files = Sources();
            foreach (var pair in files)
                if (!File.Exists(Path.Combine(source, pair.Value))) throw new FileNotFoundException(pair.Value);
            Directory.CreateDirectory(Destination);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var pair in files) File.Copy(Path.Combine(source, pair.Value), Destination + "/" + pair.Key, true);
                foreach (var pair in GoldMaps())
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        tex.LoadImage(File.ReadAllBytes(Destination + "/" + pair.Value));
                        var px = tex.GetPixels32();
                        GoldMap(px);
                        tex.SetPixels32(px);
                        File.WriteAllBytes(Destination + "/" + pair.Key, tex.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tex); }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var pngs = new List<string>(GoldMaps().Keys) { "DarkAges.png" };
            foreach (var name in pngs)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Destination + "/" + name);
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;          // pixel art: never bilinear
                importer.mipmapEnabled = false;                  // UI is never minified on purpose
                importer.npotScale = TextureImporterNPOTScale.None; // 384x352 must stay 384x352
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;       // sliced edges must not bleed
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            // Raw Dwellers sheets were only inputs to the gold map; delete them so nothing loads
            // the off-palette originals by mistake.
            foreach (var raw in GoldMaps().Values) AssetDatabase.DeleteAsset(Destination + "/" + raw);
            return files.Count;
        }
    }
}
```

- [ ] **Step 4: Run the tests (expect PASS)**

Run: `bash .superpowers/rt.sh editor UiArtImporterTests`
Expected: `4 passed`.

- [ ] **Step 5: Ignore the folder, then import**

Append these lines to `.gitignore`, under the WorldArt lines:

```
# Local licensed UI art + fonts: recreate from the owner's downloads with UiArtImporter.
/Assets/Game/Resources/UiArt/
/Assets/Game/Resources/UiArt.meta
```

Then run `bash .superpowers/ev.sh 'return BorrowedHex.EditorTools.UiArtImporter.Import(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads/UI_elements")).ToString();'`. Expected output: `12` (one Dark Ages sheet, ten Dark Dwellers sheets, and alagard).

Then run `git status --short Assets/Game/Resources`. Expected: nothing under `UiArt` is listed.

- [ ] **Step 6: Record the licences**

Add one row per pack to the licence table in `Docs/WORLD_ART_HANDOFF.md`:
- **Dark Ages UI by Hypnobius:** commercial use OK, credit optional, no redistribution, no NFT or AI-training use.
- **Tiny RPG - Dark Dwellers by Gabriel "tiopalada" Lima:** CC0 1.0.
- **alagard by Hewett Tsoi:** **TBD, Q1**. Write the specific open question: "Does the author's own page grant commercial use, and is credit required?"

Use the source folder names exactly as they appear in `Sources()`.

- [ ] **Step 7: Gate and commit (only when the owner asks)**

Run: `bash .superpowers/strict.sh both t1`. Expected: `STRICT GATE GREEN (both)`.
Stage: `git add .gitignore Assets/Game/Editor/UiArtImporter.cs Assets/Game/Editor/UiArtImporter.cs.meta Assets/Game/Tests/EditMode/UiArtImporterTests.cs Assets/Game/Tests/EditMode/UiArtImporterTests.cs.meta Docs/WORLD_ART_HANDOFF.md`
Message: `Import the pixel UI packs and alagard into a local, git-ignored library`

---

### Task 2: Skin, palette and fonts (runtime loading, with a flat fallback)

**Files:**
- Create: `Assets/Game/Scripts/UI/Kit/UiPalette.cs`
- Create: `Assets/Game/Scripts/UI/Kit/UiFonts.cs`
- Create: `Assets/Game/Scripts/UI/Kit/UiSkin.cs`
- Modify: `Assets/Game/Scripts/Presentation/Feedback/CalloutText.cs` (drop its private `PointFilter`, use `UiFonts`)
- Test: `Assets/Game/Tests/EditMode/UiSkinTests.cs`, `Assets/Game/Tests/EditMode/UiFontsTests.cs`

**Interfaces:**
- Consumes: the file names from Task 1.
- Produces:
  - `UiPalette`: `Ink`, `Panel`, `PanelDeep`, `Ivory`, `Muted`, `Honey`, `Camel`, `Violet`, `Blood`, `Warning`, `Good`, and `Scrim`, all `Color`.
  - `UiFonts.Display` and `UiFonts.Body` (`Font`, never null); `UiFonts.HasPixelFonts`; `UiFonts.NativeDisplay` and `UiFonts.NativeBody` (`int`); `UiFonts.Size(Role)` returning `int`; `UiFonts.PointFilter(Font)`.
  - `enum UiFonts.Role { Title, Heading, Sub, Button, Body, Number, Small }`.
  - `UiSkin.HasArt` (`bool`) and `UiSkin.Sprite(string id)` returning `Sprite` or null; `UiSkin.ForceFlat`, a static `bool` for tests; `UiSkin.Ids` (`IReadOnlyCollection<string>`); `UiSkin.Frames(string id)` returning `Sprite[]` for animated pieces.

**Sprite table.** The rects are in Unity texture coordinates, with the origin bottom-left; the tilesheet is 384×352. They were measured by an opaque-island scan of the sheet, run during planning (`islands.py` in the planning scratchpad). Border values are **inferred** from the 32-px tile grid and the visible ornament size. Step 6 checks them visually, and only the border numbers may change there.

| Id | File | Rect (x, y, w, h) | Border (L, B, R, T) | Use |
|---|---|---|---|---|
| `frame.ornate` | DarkAges | 0, 256, 96, 96 | 32, 32, 32, 32 | screens, dialogs |
| `frame.parchment` | DarkAges | 96, 256, 96, 96 | 16, 16, 16, 16 | Details, Records rows |
| `frame.card` | DarkAges | 210, 274, 60, 60 | 12, 12, 12, 12 | cards, inspector, slots |
| `frame.cardAlt` | DarkAges | 306, 274, 60, 60 | 12, 12, 12, 12 | selected card |
| `plate.dark` | DarkAges | 0, 225, 64, 23 | 12, 8, 12, 8 | secondary button |
| `plate.darkAlt` | DarkAges | 64, 225, 64, 23 | 12, 8, 12, 8 | secondary button, highlighted |
| `plate.crest` | DarkAges | 128, 225, 64, 31 | 16, 8, 16, 14 | primary button (gold crest on top) |
| `square.dark` | DarkAges | 194, 224, 28, 27 | 8, 8, 8, 8 | icon button, checkbox |
| `square.crest` | DarkAges | 256, 224, 32, 32 | 8, 8, 8, 12 | icon button, active |
| `scroll.track` | DarkAges | 12, 135, 7, 84 | 0, 8, 0, 8 | scrollbar |
| `bar.tray` | DarkAges | 197, 202, 86, 13 | 8, 0, 8, 0 | Life and XP bar frame |
| `bar.trayDark` | DarkAges | 102, 204, 84, 7 | 6, 0, 6, 0 | small bars (dash, slot) |
| `fill.blue` | DarkAges | 296, 204, 82, 4 | 1, 0, 1, 0 | dash fill |
| `fill.red` | DarkAges | 199, 172, 82, 4 | 1, 0, 1, 0 | Life fill |
| `fill.green` | DarkAges | 295, 172, 82, 4 | 1, 0, 1, 0 | XP fill |
| `gem.0` … `gem.3` | DarkAges | (10,74,13,12) (41,73,12,13) (73,73,12,13) (105,74,13,12) | none | state gems, bullets |
| `gem.4` … `gem.7` | DarkAges | (10,42,13,12) (41,41,12,13) (73,41,12,13) (105,42,13,12) | none | dim variants |
| `mark.x` | DarkAges | 76, 108, 7, 8 | none | close, failed |
| `divider.a` / `divider.b` | DarkAges | (297,76,37,6) / (296,44,37,6) | 12, 0, 12, 0 | section dividers |
| `filigree.a` / `filigree.b` | DarkAges | (192,96,64,64) / (256,96,64,64) | 16, 16, 16, 16 | skill map frame, title |
| `strip.parchment` | DarkAges | 0, 0, 64, 32 | 8, 8, 8, 8 | tooltip |
| `tab` (4 frames) | DwTab | frame i: (96·i, 0, 96, 32) | 12, 8, 12, 8 | tabs; frame 0 = idle, 1 = selected |
| `pointer` (5 frames) | DwPointer | frame i: (32·i, 0, 32, 19) | none | focus marker |
| `btn.close` / `btn.options` (4) | DwClose / DwOptions | frame i: (32·i, 0, 32, 32) | none | Back and Settings |
| `arrow.left/right` (4) | DwLeft / DwRight | frame i: (19·i, 0, 19, 18) | none | stepper |
| `arrow.up/down` (4) | DwUp / DwDown | same as left | none | scroll |
| `portrait` | DwPortrait | 0, 0, 66, 72 | none | style card emblem frame |

- [ ] **Step 1: Write the failing tests**

```csharp
// UiSkinTests.cs
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class UiSkinTests
    {
        [TearDown] public void TearDown() => UiSkin.ForceFlat = false;

        // Review Focus 1: a public checkout has no UiArt; nothing may throw or return junk.
        [Test]
        public void MissingArtFallsBackToFlatColour()
        {
            UiSkin.ForceFlat = true;
            Assert.IsFalse(UiSkin.HasArt);
            foreach (var id in UiSkin.Ids) Assert.IsNull(UiSkin.Sprite(id), id);
            Assert.IsEmpty(UiSkin.Frames("pointer"));
        }

        [Test]
        public void EveryRectLiesInsideItsSheet()
        {
            if (!UiSkin.HasArt) Assert.Ignore("local UI art not imported");
            foreach (var id in UiSkin.Ids)
            {
                var s = UiSkin.Sprite(id);
                Assert.NotNull(s, id);
                Assert.LessOrEqual(s.rect.xMax, s.texture.width, id);
                Assert.LessOrEqual(s.rect.yMax, s.texture.height, id);
                // A border wider than half the sprite collapses the 9-slice centre to nothing.
                Assert.Less(s.border.x + s.border.z, s.rect.width, id);
                Assert.Less(s.border.y + s.border.w, s.rect.height, id);
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, id);
            }
        }

        [Test]
        public void SpritesAreTwoReferencePixelsPerArtPixel()
        {
            if (!UiSkin.HasArt) Assert.Ignore("local UI art not imported");
            Assert.AreEqual(50f, UiSkin.Sprite("frame.card").pixelsPerUnit);
        }
    }
}
```

```csharp
// UiFontsTests.cs
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class UiFontsTests
    {
        [Test]
        public void FontsAreNeverNull() { Assert.NotNull(UiFonts.Display); Assert.NotNull(UiFonts.Body); }

        // Pixel fonts are only crisp at whole multiples of their design size.
        [Test]
        public void EveryRoleIsAWholeMultipleOfItsFontsNativeSize()
        {
            foreach (UiFonts.Role r in System.Enum.GetValues(typeof(UiFonts.Role)))
            {
                int native = UiFonts.UsesDisplay(r) ? UiFonts.NativeDisplay : UiFonts.NativeBody;
                Assert.AreEqual(0, UiFonts.Size(r) % native, r.ToString());
            }
        }

        // Confirms the native size assumption (16) against the real glyphs: at twice the size
        // a pixel font's capital is exactly twice as tall. A wrong native size breaks this.
        [Test]
        public void NativeSizeDoublesExactly()
        {
            if (!UiFonts.HasPixelFonts) Assert.Ignore("local fonts not imported");
            foreach (var (f, n) in new[] { (UiFonts.Display, UiFonts.NativeDisplay), (UiFonts.Body, UiFonts.NativeBody) })
            {
                f.RequestCharactersInTexture("H", n); f.GetCharacterInfo('H', out var a, n);
                f.RequestCharactersInTexture("H", n * 2); f.GetCharacterInfo('H', out var b, n * 2);
                Assert.Greater(a.glyphHeight, 0, f.name);
                Assert.AreEqual(a.glyphHeight * 2, b.glyphHeight, f.name);
            }
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

Run: `bash .superpowers/uc.sh`. Expected: errors naming `UiSkin` and `UiFonts`.

- [ ] **Step 3: Implement `UiPalette`**

```csharp
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Menu colour tokens (plan section 0.3). Golds are the Dark Ages pack's own swatches so
    /// text never fights the frames. Combat colours stay in FeedbackColors and never come from here.
    /// </summary>
    public static class UiPalette
    {
        static Color Hex(uint rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static readonly Color Ink = Hex(0x0E0B14);          // deepest background
        public static readonly Color PanelDeep = Hex(0x15121C, 0.96f);
        public static readonly Color Panel = Hex(0x1E1928, 0.94f);   // flat fallback for frames
        public static readonly Color Ivory = Hex(0xEEE7D8);         // body text
        public static readonly Color Muted = Hex(0x9A90A6);         // secondary text, disabled
        public static readonly Color Honey = Hex(0xDCC47C);         // headings, primary captions
        public static readonly Color Camel = Hex(0xC19149);         // frame lines (flat fallback), quiet buttons
        public static readonly Color Violet = Hex(0x9B6BE0);        // magic: focus, mastery, selection
        public static readonly Color Blood = Hex(0xB3373F);         // Life
        public static readonly Color Warning = Hex(0xDA7777);       // warnings, failures (text)
        public static readonly Color Good = Hex(0x8FC77A);          // earned, saved
        public static readonly Color Scrim = Hex(0x07050B, 0.78f);  // behind modal screens
    }
}
```

- [ ] **Step 4: Implement `UiFonts`**

```csharp
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The two pixel faces (plan 0.1): alagard for display, m5x7 for body. Both are licensed
    /// third-party files in git-ignored folders, so each falls back to the built-in font and the
    /// game stays readable in a public checkout. Sizes come from roles, never literals, so the
    /// whole UI stays on the font's pixel grid.
    /// </summary>
    public static class UiFonts
    {
        public enum Role { Title, Heading, Sub, Button, Body, Number, Small }

        // Design size of each face: one font pixel per screen pixel at this size. 16 for both
        // (inferred from the fonts' 16-px em; pinned by UiFontsTests.NativeSizeDoublesExactly).
        public const int NativeDisplay = 16, NativeBody = 16;
        // Plan Q2: switch to "WorldArt/m6x11" (or wherever it is imported) if the owner adopts it.
        public const string BodyFile = "WorldArt/m5x7";

        static Font display, body;
        static bool loaded;

        public static bool HasPixelFonts { get { Load(); return display != Ui.Font && body != Ui.Font; } }
        public static Font Display { get { Load(); return display; } }
        public static Font Body { get { Load(); return body; } }

        public static bool UsesDisplay(Role r) => r == Role.Title || r == Role.Heading || r == Role.Sub || r == Role.Button;

        /// <summary>Reference-pixel size per role. All are multiples of 16 by construction.</summary>
        public static int Size(Role r)
        {
            switch (r)
            {
                case Role.Title: return 96;    // x6: the logo
                case Role.Heading: return 48;  // x3: screen titles
                case Role.Sub: return 32;      // x2: card names, section heads
                case Role.Button: return 32;
                case Role.Number: return 48;   // score, big stats
                default: return 32;            // Body and Small: hierarchy by colour, not by a blurry size
            }
        }

        public static Font For(Role r) => UsesDisplay(r) ? Display : Body;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            display = Resources.Load<Font>("UiArt/alagard");
            body = Resources.Load<Font>(BodyFile);
            if (display == null) display = Ui.Font; else Watch(display);
            if (body == null) body = Ui.Font; else Watch(body);
        }

        static void Watch(Font f)
        {
            PointFilter(f);
            // A dynamic font rebuilds its atlas when new glyphs appear, and the new texture
            // comes back bilinear. Re-point it every time, or text blurs mid-session.
            Font.textureRebuilt += rebuilt => { if (rebuilt == f) PointFilter(f); };
        }

        public static void PointFilter(Font f)
        {
            if (f != null && f.material != null && f.material.mainTexture != null)
                f.material.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
```

In `CalloutText.cs`, load the font through `UiFonts.Body` and delete the private filter. This keeps one rebuilt-atlas handler per font: two handlers on the same font would be harmless, but they would be duplicated code.

```csharp
            // m5x7 (spec 6), shared with the UI through UiFonts: one font, one point-filter hook.
            font = UI.UiFonts.Body;
```

Remove the `PointFilter` method and the `Font.textureRebuilt += PointFilter;` subscription.

- [ ] **Step 5: Implement `UiSkin`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Named UI sprites cut at runtime from the git-ignored Resources/UiArt sheets. The table is
    /// the single committed record of every rect and 9-slice border (plan Task 2). When the art is
    /// missing every lookup returns null and the kit draws flat colour instead.
    /// </summary>
    public static class UiSkin
    {
        /// <summary>Tests force the public-checkout path regardless of what is imported locally.</summary>
        public static bool ForceFlat;
        public const float PixelsPerUnit = 50f;   // canvas reference PPU is 100: one art pixel = 2 reference px

        struct Spec { public string File; public RectInt Rect; public Vector4 Border; public int Frames; }

        static readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>();
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

        static void Add(string id, string file, int x, int y, int w, int h, int l = 0, int b = 0, int r = 0, int t = 0, int frames = 1)
            => specs[id] = new Spec { File = file, Rect = new RectInt(x, y, w, h), Border = new Vector4(l, b, r, t), Frames = frames };

        static UiSkin()
        {
            const string D = "DarkAges";
            Add("frame.ornate", D, 0, 256, 96, 96, 32, 32, 32, 32);
            Add("frame.parchment", D, 96, 256, 96, 96, 16, 16, 16, 16);
            Add("frame.card", D, 210, 274, 60, 60, 12, 12, 12, 12);
            Add("frame.cardAlt", D, 306, 274, 60, 60, 12, 12, 12, 12);
            Add("plate.dark", D, 0, 225, 64, 23, 12, 8, 12, 8);
            Add("plate.darkAlt", D, 64, 225, 64, 23, 12, 8, 12, 8);
            Add("plate.crest", D, 128, 225, 64, 31, 16, 8, 16, 14);
            Add("square.dark", D, 194, 224, 28, 27, 8, 8, 8, 8);
            Add("square.crest", D, 256, 224, 32, 32, 8, 8, 8, 12);
            Add("scroll.track", D, 12, 135, 7, 84, 0, 8, 0, 8);
            Add("bar.tray", D, 197, 202, 86, 13, 8, 0, 8, 0);
            Add("bar.trayDark", D, 102, 204, 84, 7, 6, 0, 6, 0);
            Add("fill.blue", D, 296, 204, 82, 4, 1, 0, 1, 0);
            Add("fill.red", D, 199, 172, 82, 4, 1, 0, 1, 0);
            Add("fill.green", D, 295, 172, 82, 4, 1, 0, 1, 0);
            int[,] gems = { { 10, 74, 13, 12 }, { 41, 73, 12, 13 }, { 73, 73, 12, 13 }, { 105, 74, 13, 12 },
                            { 10, 42, 13, 12 }, { 41, 41, 12, 13 }, { 73, 41, 12, 13 }, { 105, 42, 13, 12 } };
            for (int i = 0; i < 8; i++) Add("gem." + i, D, gems[i, 0], gems[i, 1], gems[i, 2], gems[i, 3]);
            Add("mark.x", D, 76, 108, 7, 8);
            Add("divider.a", D, 297, 76, 37, 6, 12, 0, 12, 0);
            Add("divider.b", D, 296, 44, 37, 6, 12, 0, 12, 0);
            Add("filigree.a", D, 192, 96, 64, 64, 16, 16, 16, 16);
            Add("filigree.b", D, 256, 96, 64, 64, 16, 16, 16, 16);
            Add("strip.parchment", D, 0, 0, 64, 32, 8, 8, 8, 8);
            Add("tab", "DwTab", 0, 0, 96, 32, 12, 8, 12, 8, frames: 4);
            Add("pointer", "DwPointer", 0, 0, 32, 19, frames: 5);
            Add("btn.close", "DwClose", 0, 0, 32, 32, frames: 4);
            Add("btn.options", "DwOptions", 0, 0, 32, 32, frames: 4);
            Add("arrow.left", "DwLeft", 0, 0, 19, 18, frames: 4);
            Add("arrow.right", "DwRight", 0, 0, 19, 18, frames: 4);
            Add("arrow.up", "DwUp", 0, 0, 19, 18, frames: 4);
            Add("arrow.down", "DwDown", 0, 0, 19, 18, frames: 4);
            Add("portrait", "DwPortrait", 0, 0, 66, 72);
        }

        public static IReadOnlyCollection<string> Ids => specs.Keys;

        public static bool HasArt => !ForceFlat && Resources.Load<Texture2D>("UiArt/DarkAges") != null;

        /// <summary>The sprite (frame 0 of an animated piece), or null without the art.</summary>
        public static Sprite Sprite(string id)
        {
            if (!HasArt || !specs.TryGetValue(id, out var s)) return null;
            if (cache.TryGetValue(id, out var hit) && hit != null) return hit;
            return cache[id] = Cut(s, 0);
        }

        /// <summary>All frames of an animated piece (pointer, tab states, buttons); empty without the art.</summary>
        public static Sprite[] Frames(string id)
        {
            if (!HasArt || !specs.TryGetValue(id, out var s)) return new Sprite[0];
            if (frameCache.TryGetValue(id, out var hit) && hit.Length > 0 && hit[0] != null) return hit;
            var f = new Sprite[s.Frames];
            for (int i = 0; i < f.Length; i++) f[i] = Cut(s, i);
            return frameCache[id] = f;
        }

        static Sprite Cut(Spec s, int frame)
        {
            var tex = Resources.Load<Texture2D>("UiArt/" + s.File);
            if (tex == null) return null;
            var r = new Rect(s.Rect.x + frame * s.Rect.width, s.Rect.y, s.Rect.width, s.Rect.height);
            // FullRect mesh: Tight meshes break 9-slicing. The border makes Image.Type.Sliced work.
            var sp = UnityEngine.Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, s.Border);
            sp.name = s.File + "#" + frame;
            return sp;
        }
    }
}
```

- [ ] **Step 6: Run the tests, then capture an art proof sheet**

Run: `bash .superpowers/rt.sh editor "UiSkinTests|UiFontsTests"`. Expected: all pass. The two art-guarded tests pass where the art is imported and are ignored otherwise.

Visual check of the inferred borders:
1. Run this in Play mode:
   `bash .superpowers/ev.sh 'var c = BorrowedHex.UI.Ui.CreateCanvas("Proof", 99); float x = 40; foreach (var id in BorrowedHex.UI.UiSkin.Ids) { var s = BorrowedHex.UI.UiSkin.Sprite(id); if (s == null) continue; var img = BorrowedHex.UI.Ui.Image(id, c.transform, UnityEngine.Color.white); img.sprite = s; img.type = s.border != UnityEngine.Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple; BorrowedHex.UI.Ui.Place(img.rectTransform, new UnityEngine.Vector2(0,1), new UnityEngine.Vector2(x % 1800, -40 - 260 * (int)(x / 1800)), new UnityEngine.Vector2(240, 180)); x += 260; } return "ok";'`
2. Then run `bash .superpowers/cap.sh ui-proof`.
3. Open `.superpowers/shots/ui-proof.png`. Each sliced piece must show unstretched corners and a clean, repeating centre.
4. Adjust only the border numbers in the table and in `UiSkin`, then re-capture.

Show the capture to the owner for plan Q3 (the panel interior colour).

- [ ] **Step 7: Gate and commit (only when asked)**

Run: `bash .superpowers/strict.sh both t2`. Stage the three new files with their metas, the two test files with their metas, and `CalloutText.cs`.
Message: `Load the UI art and pixel fonts through one skin, with a flat fallback`

---

### Task 3: The themed kit (frames, button tiers, rows, tabs, scrolling, tooltips, focus pointer)

**Files:**
- Create: `Assets/Game/Scripts/UI/Kit/UiKit.cs`
- Create: `Assets/Game/Scripts/UI/Kit/UiTooltip.cs`
- Create: `Assets/Game/Scripts/UI/Kit/FocusPointer.cs`
- Modify: `Assets/Game/Scripts/UI/Ui.cs`. `Label` uses the role fonts. `Button` becomes `UiKit.Button(..., Tier.Secondary)`, so every untouched screen inherits the skin immediately. The constants `Ink`, `Panel`, `Accent` and `ButtonFill` become aliases to `UiPalette`.
- Test: `Assets/Game/Tests/EditMode/UiKitTests.cs`

**Interfaces:**
- Consumes: `UiSkin`, `UiFonts` and `UiPalette` (Task 2).
- Produces:
  - `enum UiKit.Tier { Primary, Secondary, Quiet, Icon }`.
  - `enum UiKit.FrameKind { Ornate, Card, CardSelected, Parchment, Tooltip }`.
  - `Image UiKit.Frame(string name, Transform parent, FrameKind kind)`.
  - `Button UiKit.Button(string name, Transform parent, string text, Action onClick, Tier tier = Tier.Secondary)`.
  - `Text UiKit.Text(string name, Transform parent, string text, UiFonts.Role role, TextAnchor align = MiddleLeft)`.
  - `Image UiKit.Divider(string name, Transform parent)`.
  - `UiKit.Row UiKit.ToggleRow(Transform parent, string label, Func<bool> get, Action<bool> set)`.
  - `UiKit.Row UiKit.StepperRow(Transform parent, string label, Func<string> value, Action<int> step)`.
  - `class UiKit.Row { RectTransform Rect; Text Label; Text Value; Selectable Control; void Refresh(); }`.
  - `UiKit.TabStrip UiKit.Tabs(Transform parent, string[] labels, Action<int> onSelect)`, where `TabStrip` has `int Selected` and `void Select(int i)`.
  - `RectTransform UiKit.ScrollView(string name, Transform parent, out ScrollRect scroll)`, which returns the content rect.
  - `UiTooltip.Attach(Selectable s, Func<string> text)` and `UiTooltip.Tick(float now)`.
  - `FocusPointer.Ensure(Canvas canvas)`, the singleton marker per canvas.

The four button tiers carry the spec's "one prominent action":
- **Primary** is the crest plate with an alagard Honey caption. Use exactly one per screen.
- **Secondary** is the dark plate with an Ivory caption.
- **Quiet** has no plate. It shows a Camel caption, plus a 2-px underline while focused or hovered.
- **Icon** is the square plate with a glyph and a tooltip, for Settings and Back.

Every tier shows the gold `FocusPointer` beside it when selected. Hover lightens the plate by swapping the sprite (`plate.dark` to `plate.darkAlt`), not by a colour tint, because tints muddy the gold.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    public class UiKitTests
    {
        Canvas canvas;
        [SetUp] public void SetUp() { UiSkin.ForceFlat = true; canvas = Ui.CreateCanvas("KitTest", 0); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(canvas.gameObject); UiSkin.ForceFlat = false; }

        [Test]
        public void PrimaryButtonsUseTheDisplayFaceAndHoney()
        {
            var b = UiKit.Button("Go", canvas.transform, "Play", null, UiKit.Tier.Primary);
            var t = b.GetComponentInChildren<Text>();
            Assert.AreSame(UiFonts.Display, t.font);
            Assert.AreEqual(UiPalette.Honey, t.color);
            Assert.AreEqual(UiFonts.Size(UiFonts.Role.Button), t.fontSize);
        }

        [Test]
        public void QuietButtonsHaveNoPlate()
        {
            var b = UiKit.Button("Q", canvas.transform, "Cheats", null, UiKit.Tier.Quiet);
            Assert.AreEqual(0f, b.GetComponent<Image>().color.a, 1e-4, "raycastable but invisible");
        }

        // State is never colour-alone (spec): the toggle says On/Off in words too.
        [Test]
        public void ToggleRowFlipsAndSaysSo()
        {
            bool v = false;
            var row = UiKit.ToggleRow(canvas.transform, "Reduce flashes", () => v, x => v = x);
            Assert.AreEqual("Off", row.Value.text);
            ((Button)row.Control).onClick.Invoke();
            Assert.IsTrue(v); Assert.AreEqual("On", row.Value.text);
        }

        [Test]
        public void StepperCallsBothDirections()
        {
            int total = 0;
            var row = UiKit.StepperRow(canvas.transform, "UI scale", () => total.ToString(), d => total += d);
            row.Rect.Find("Less").GetComponent<Button>().onClick.Invoke();
            row.Rect.Find("More").GetComponent<Button>().onClick.Invoke();
            row.Rect.Find("More").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, total); Assert.AreEqual("1", row.Value.text);
        }

        [Test]
        public void TabsReportAndKeepTheirSelection()
        {
            int picked = -1;
            var tabs = UiKit.Tabs(canvas.transform, new[] { "Skills", "Capture style" }, i => picked = i);
            tabs.Select(1);
            Assert.AreEqual(1, tabs.Selected); Assert.AreEqual(1, picked);
        }

        [Test]
        public void ATooltipWaitsBeforeShowing()
        {
            var b = UiKit.Button("Gear", canvas.transform, "", null, UiKit.Tier.Icon);
            var tip = UiTooltip.Attach(b, () => "Settings");
            tip.Hover(true, 10f);
            tip.Tick(10.2f); Assert.IsFalse(tip.Showing);
            tip.Tick(10.4f); Assert.IsTrue(tip.Showing);
            StringAssert.Contains("Settings", tip.Label.text);
            tip.Hover(false, 10.5f); tip.Tick(10.5f); Assert.IsFalse(tip.Showing);
        }

        [Test]
        public void ScrollViewClipsItsContent()
        {
            var content = UiKit.ScrollView("Rows", canvas.transform, out var scroll);
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>(), "rows never draw over the footer");
            Assert.AreSame(content, scroll.content);
            Assert.IsFalse(scroll.horizontal);
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure.** Run: `bash .superpowers/uc.sh`.

- [ ] **Step 3: Implement `UiKit`**

```csharp
using System;
using BorrowedHex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The themed building blocks every screen uses (plan Task 3). Static and code-only like Ui:
    /// each call returns live uGUI objects. Each piece picks a pixel sprite from UiSkin when the
    /// local art exists, and flat palette colour plus an Outline when it does not, so layout is
    /// identical either way and the layout tests prove both.
    /// </summary>
    public static class UiKit
    {
        public enum Tier { Primary, Secondary, Quiet, Icon }
        public enum FrameKind { Ornate, Card, CardSelected, Parchment, Tooltip }

        public const int Gap = 16;          // the spacing scale: 8, 16, 24, 32, 48
        public const int RowHeight = 56;    // one control row: 2x the 23-px plate + caption room
        public const int ButtonHeight = 64;

        static string FrameId(FrameKind k) => k switch
        {
            FrameKind.Ornate => "frame.ornate", FrameKind.Card => "frame.card",
            FrameKind.CardSelected => "frame.cardAlt", FrameKind.Parchment => "frame.parchment",
            _ => "strip.parchment",
        };

        public static Image Frame(string name, Transform parent, FrameKind kind)
        {
            var img = Ui.Image(name, parent, Color.white);
            var s = UiSkin.Sprite(FrameId(kind));
            if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; }
            else
            {
                // Flat fallback: the panel tone with a camel hairline standing in for the gilded edge.
                bool light = kind == FrameKind.Parchment || kind == FrameKind.Tooltip;
                img.color = light ? new Color(0.85f, 0.78f, 0.6f, 0.97f) : kind == FrameKind.Ornate ? UiPalette.PanelDeep : UiPalette.Panel;
                var o = img.gameObject.AddComponent<Outline>();
                o.effectColor = kind == FrameKind.CardSelected ? UiPalette.Honey : UiPalette.Camel;
                o.effectDistance = new Vector2(2, -2);
            }
            return img;
        }

        public static Text Text(string name, Transform parent, string text, UiFonts.Role role, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = Ui.Label(name, parent, text, UiFonts.Size(role), align);
            t.font = UiFonts.For(role);
            t.color = role == UiFonts.Role.Heading || role == UiFonts.Role.Title || role == UiFonts.Role.Sub ? UiPalette.Honey
                : role == UiFonts.Role.Small ? UiPalette.Muted : UiPalette.Ivory;
            // Pixel faces have tall line boxes; 1.0 spacing keeps rows on the 8-unit grid.
            t.lineSpacing = 1f;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Tier tier = Tier.Secondary)
        {
            var img = Ui.Image(name, parent, Color.white);
            var b = img.gameObject.AddComponent<Button>();
            string idle = tier == Tier.Primary ? "plate.crest" : tier == Tier.Icon ? "square.dark" : "plate.dark";
            string lit = tier == Tier.Primary ? "plate.crest" : tier == Tier.Icon ? "square.crest" : "plate.darkAlt";
            var s0 = tier == Tier.Quiet ? null : UiSkin.Sprite(idle);
            if (tier == Tier.Quiet) img.color = new Color(0, 0, 0, 0);       // still catches the pointer
            else if (s0 != null)
            {
                img.sprite = s0; img.type = Image.Type.Sliced;
                // Sprite swap, not tint: a multiply tint muddies the gold leaf.
                b.transition = Selectable.Transition.SpriteSwap;
                var ss = b.spriteState;
                ss.highlightedSprite = ss.selectedSprite = UiSkin.Sprite(lit);
                ss.pressedSprite = s0; ss.disabledSprite = s0;
                b.spriteState = ss;
            }
            else
            {
                img.color = tier == Tier.Primary ? new Color(0.36f, 0.24f, 0.12f) : UiPalette.Panel;
                var o = img.gameObject.AddComponent<Outline>();
                o.effectColor = tier == Tier.Primary ? UiPalette.Honey : UiPalette.Camel;
            }
            var role = UiFonts.Role.Button;
            var label = Text("Label", img.transform, text, role, TextAnchor.MiddleCenter);
            label.color = tier == Tier.Primary ? UiPalette.Honey : tier == Tier.Quiet ? UiPalette.Camel : UiPalette.Ivory;
            Ui.Stretch(label.rectTransform);
            if (tier == Tier.Quiet) img.gameObject.AddComponent<QuietUnderline>().Bind(b, label);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public static Image Divider(string name, Transform parent)
        {
            var img = Ui.Image(name, parent, UiPalette.Camel);
            var s = UiSkin.Sprite("divider.a");
            if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; img.color = Color.white; }
            return Ui.Sized(img, s != null ? 12 : 2);
        }

        /// <summary>A labelled settings-style row: label left, control right, value text in between.</summary>
        public sealed class Row
        {
            public RectTransform Rect; public Text Label, Value; public Selectable Control;
            internal Func<string> Read;
            public void Refresh() { if (Read != null) Value.text = Read(); }
        }

        static Row NewRow(Transform parent, string label)
        {
            var rt = Ui.Sized(Ui.Rect("Row_" + label, parent), RowHeight);
            var l = Text("Label", rt, label, UiFonts.Role.Body);
            l.rectTransform.anchorMin = new Vector2(0, 0); l.rectTransform.anchorMax = new Vector2(0.5f, 1);
            l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
            var v = Text("Value", rt, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            Ui.Place(v.rectTransform, new Vector2(1, 0.5f), new Vector2(-72, 0), new Vector2(240, RowHeight));
            return new Row { Rect = rt, Label = l, Value = v };
        }

        public static Row ToggleRow(Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            var row = NewRow(parent, label);
            row.Read = () => get() ? "On" : "Off";
            Button box = null;
            box = Button("Toggle", row.Rect, "", () => { set(!get()); row.Refresh(); SetGem(box, get()); }, Tier.Icon);
            Ui.Place((RectTransform)box.transform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(56, 54));
            row.Control = box;
            row.Refresh(); SetGem(box, get());
            return row;
        }

        // The checkbox mark: a lit gem when on, nothing when off. Plus the On/Off text, so the
        // state never relies on colour or a tiny sprite alone.
        static void SetGem(Button box, bool on)
        {
            var gem = box.transform.Find("Gem") as RectTransform;
            if (gem == null)
            {
                var g = Ui.Image("Gem", box.transform, UiPalette.Honey);
                var s = UiSkin.Sprite("gem.1");
                if (s != null) { g.sprite = s; g.color = Color.white; }
                g.raycastTarget = false;
                gem = Ui.Place(g.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 26));
            }
            gem.gameObject.SetActive(on);
        }

        public static Row StepperRow(Transform parent, string label, Func<string> value, Action<int> step)
        {
            var row = NewRow(parent, label);
            row.Read = value;
            var less = Arrow("Less", row.Rect, "arrow.left", "<", () => { step(-1); row.Refresh(); });
            Ui.Place((RectTransform)less.transform, new Vector2(1, 0.5f), new Vector2(-312, 0), new Vector2(38, 36));
            var more = Arrow("More", row.Rect, "arrow.right", ">", () => { step(+1); row.Refresh(); });
            Ui.Place((RectTransform)more.transform, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(38, 36));
            row.Control = more;
            row.Refresh();
            return row;
        }

        static Button Arrow(string name, Transform parent, string spriteId, string fallback, Action onClick)
        {
            var frames = UiSkin.Frames(spriteId);
            var b = Button(name, parent, frames.Length > 0 ? "" : fallback, onClick, Tier.Icon);
            if (frames.Length == 4)
            {
                var img = b.GetComponent<Image>();
                img.sprite = frames[0]; img.type = Image.Type.Simple;
                b.transition = Selectable.Transition.SpriteSwap;
                // Frame order: idle, hover, pressed, disabled (verified on the proof sheet, Task 2 Step 6).
                b.spriteState = new SpriteState { highlightedSprite = frames[1], selectedSprite = frames[1], pressedSprite = frames[2], disabledSprite = frames[3] };
            }
            return b;
        }

        public sealed class TabStrip
        {
            public RectTransform Rect; public int Selected { get; private set; } = -1;
            internal Button[] Buttons; internal Action<int> OnSelect;
            public void Select(int i)
            {
                if (i == Selected) return;
                Selected = i;
                var frames = UiSkin.Frames("tab");
                for (int k = 0; k < Buttons.Length; k++)
                {
                    var img = Buttons[k].GetComponent<Image>();
                    if (frames.Length == 4) img.sprite = frames[k == i ? 1 : 0];
                    var t = Buttons[k].GetComponentInChildren<Text>();
                    t.color = k == i ? UiPalette.Honey : UiPalette.Muted;
                    // Selected also reads without colour: the active tab carries a pointer gem.
                    t.text = (k == i ? "♦ " : "") + t.text.TrimStart('♦', ' ');
                }
                OnSelect?.Invoke(i);
            }
        }

        public static TabStrip Tabs(Transform parent, string[] labels, Action<int> onSelect)
        {
            var rt = Ui.Rect("Tabs", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
            var strip = new TabStrip { Rect = rt, Buttons = new Button[labels.Length] };
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                var b = Button("Tab" + i, rt, labels[i], () => strip.Select(k), Tier.Secondary);
                var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 280; le.preferredHeight = 64;
                var frames = UiSkin.Frames("tab");
                if (frames.Length == 4) { var img = b.GetComponent<Image>(); img.sprite = frames[0]; img.type = Image.Type.Sliced; b.transition = Selectable.Transition.None; }
                strip.Buttons[i] = b;
            }
            strip.OnSelect = onSelect;
            return strip;
        }

        /// <summary>
        /// Bounded vertical scrolling for long content (Records, Details). The viewport clips with
        /// RectMask2D (no stencil, cheap on WebGL); the caller sizes the returned content.
        /// </summary>
        public static RectTransform ScrollView(string name, Transform parent, out ScrollRect scroll)
        {
            var root = Ui.Rect(name, parent);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;   // one body line per wheel notch
            var viewport = Ui.Rect("Viewport", root);
            Ui.Stretch(viewport); viewport.offsetMax = new Vector2(-24, 0);   // room for the track
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var col = Ui.Column(content, 8);
            col.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;

            var track = Ui.Image("Track", root, UiPalette.PanelDeep);
            var ts = UiSkin.Sprite("scroll.track");
            if (ts != null) { track.sprite = ts; track.type = Image.Type.Sliced; track.color = Color.white; }
            track.rectTransform.anchorMin = new Vector2(1, 0); track.rectTransform.anchorMax = new Vector2(1, 1);
            track.rectTransform.pivot = new Vector2(1, 0.5f); track.rectTransform.sizeDelta = new Vector2(14, 0);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Ui.Image("Handle", track.transform, UiPalette.Honey);
            Ui.Stretch(handle.rectTransform);
            bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            root.gameObject.AddComponent<ScrollToFocus>().Bind(scroll);
            return content;
        }
    }

    /// <summary>Quiet buttons have no plate; a 2-px underline shows focus/hover instead.</summary>
    public sealed class QuietUnderline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Button button; Image line; bool hover;
        public void Bind(Button b, Text label)
        {
            button = b;
            line = Ui.Image("Underline", transform, UiPalette.Camel);
            line.raycastTarget = false;
            line.rectTransform.anchorMin = new Vector2(0.2f, 0); line.rectTransform.anchorMax = new Vector2(0.8f, 0);
            line.rectTransform.sizeDelta = new Vector2(0, 2); line.rectTransform.anchoredPosition = new Vector2(0, 8);
        }
        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;
        void LateUpdate()
        {
            var es = EventSystem.current;
            line.enabled = hover || (es != null && es.currentSelectedGameObject == gameObject);
        }
    }

    /// <summary>Keyboard focus inside a scroll view scrolls the focused row into view.</summary>
    public sealed class ScrollToFocus : MonoBehaviour
    {
        ScrollRect scroll; GameObject last;
        public void Bind(ScrollRect s) => scroll = s;
        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null || sel == last || !sel.transform.IsChildOf(scroll.content)) { last = sel; return; }
            last = sel;
            Canvas.ForceUpdateCanvases();
            var view = scroll.viewport.rect.height;
            var content = scroll.content.rect.height;
            if (content <= view) return;
            var item = (RectTransform)sel.transform;
            float top = -scroll.content.InverseTransformPoint(item.TransformPoint(new Vector3(0, item.rect.yMax))).y;
            float bottom = top + item.rect.height;
            float y = scroll.content.anchoredPosition.y;
            if (top < y) y = top; else if (bottom > y + view) y = bottom - view;
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(y, 0, content - view));
        }
    }
}
```

- [ ] **Step 4: Implement `UiTooltip` and `FocusPointer`**

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Parchment tooltip on hover AND keyboard focus (plan 0.8), after 0.35 s so sweeping the
    /// mouse across a row of icons does not strobe. Tick(now) is public so tests drive time.
    /// </summary>
    public sealed class UiTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public const float Delay = 0.35f;
        Func<string> text; Image strip; float since = -1f; bool hover, focus;
        public Text Label { get; private set; }
        public bool Showing => strip != null && strip.gameObject.activeSelf;

        public static UiTooltip Attach(Selectable s, Func<string> text)
        {
            var t = s.gameObject.AddComponent<UiTooltip>();
            t.text = text;
            t.strip = UiKit.Frame("Tooltip", s.transform, UiKit.FrameKind.Tooltip);
            t.strip.raycastTarget = false;
            Ui.Place(t.strip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -12), new Vector2(360, 56));
            t.strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.Label = UiKit.Text("Text", t.strip.transform, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            t.Label.color = UiPalette.Ink;     // dark ink on parchment
            Ui.Stretch(t.Label.rectTransform);
            // Overrides the parent's sorting so a tooltip is never under the next row.
            var c = t.strip.gameObject.AddComponent<Canvas>(); c.overrideSorting = true; c.sortingOrder = 500;
            t.strip.gameObject.SetActive(false);
            return t;
        }

        public void Hover(bool on, float now) { hover = on; Restart(now); }
        public void Focus(bool on, float now) { focus = on; Restart(now); }
        void Restart(float now) { since = hover || focus ? now : -1f; if (since < 0f) strip.gameObject.SetActive(false); }

        public void Tick(float now)
        {
            bool show = since >= 0f && now - since >= Delay - 1e-4f;
            if (show) Label.text = text();
            if (strip.gameObject.activeSelf != show) strip.gameObject.SetActive(show);
        }

        void Update() => Tick(Time.unscaledTime);
        public void OnPointerEnter(PointerEventData e) => Hover(true, Time.unscaledTime);
        public void OnPointerExit(PointerEventData e) => Hover(false, Time.unscaledTime);
        public void OnSelect(BaseEventData e) => Focus(true, Time.unscaledTime);
        public void OnDeselect(BaseEventData e) => Focus(false, Time.unscaledTime);
    }

    /// <summary>
    /// The gold pointer orb beside whatever has keyboard focus (plan 0.7). One per canvas; it
    /// follows EventSystem.currentSelectedGameObject and animates its five frames at 8 fps, or
    /// holds frame 0 under Reduce flashes. Without art it is a small honey diamond.
    /// </summary>
    public sealed class FocusPointer : MonoBehaviour
    {
        Image img; Sprite[] frames; RectTransform rt;

        public static FocusPointer Ensure(Canvas canvas)
        {
            var found = canvas.GetComponentInChildren<FocusPointer>(true);
            if (found != null) return found;
            var img = Ui.Image("FocusPointer", canvas.transform, UiPalette.Honey);
            img.raycastTarget = false;
            var p = img.gameObject.AddComponent<FocusPointer>();
            p.img = img; p.rt = img.rectTransform; p.frames = UiSkin.Frames("pointer");
            if (p.frames.Length > 0) { img.sprite = p.frames[0]; img.color = Color.white; }
            p.rt.sizeDelta = p.frames.Length > 0 ? new Vector2(64, 38) : new Vector2(16, 16);
            p.rt.pivot = new Vector2(1f, 0.5f);
            return p;
        }

        void LateUpdate()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            bool on = sel != null && sel.activeInHierarchy && sel.transform is RectTransform;
            img.enabled = on;
            if (!on) return;
            transform.SetAsLastSibling();   // drawn over the screen that owns the focus
            var target = (RectTransform)sel.transform;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            rt.position = new Vector3(corners[0].x - 6f, (corners[0].y + corners[1].y) * 0.5f, 0);
            if (frames.Length > 0)
                img.sprite = DisplayOptions.ReduceFlashes ? frames[0] : frames[(int)(Time.unscaledTime * 8f) % frames.Length];
        }
    }
}
```

Note: if the pointer's art faces right (the proof sheet shows it pointing left), set `rt.localScale = new Vector3(-1, 1, 1)`. A mirror is still pixel-exact.

- [ ] **Step 5: Re-skin `Ui` so legacy screens inherit the look**

In `Ui.cs`:

```csharp
        // Aliases kept for screens not yet rebuilt; new code reads UiPalette directly.
        public static Color Ink => UiPalette.Ivory;
        public static Color Panel => UiPalette.Panel;
        public static Color Accent => UiPalette.Honey;
        public static Color ButtonFill => UiPalette.Panel;
```

These replace the four `static readonly` fields. Keep `Font` returning the built-in font, because `UiFonts` falls back to it.

In `Label`, after `t.font = Font;`:

```csharp
            // Pixel body face where imported. Sizes from older screens (22-30) snap to the
            // body role so legacy text is crisp until its screen is rebuilt.
            t.font = UiFonts.Body;
            if (UiFonts.HasPixelFonts) t.fontSize = Mathf.Max(UiFonts.NativeBody, Mathf.RoundToInt(size / (float)UiFonts.NativeBody) * UiFonts.NativeBody);
```

Replace the body of `Button` with `return UiKit.Button(name, parent, text, onClick, UiKit.Tier.Secondary);`, and drop `fontSize` (the parameter stays for source compatibility).

`UiFonts` must not recurse into `Ui.Font`: it calls `Ui.Font` only as the fallback value, and `Ui.Font` never calls `UiFonts`. Check this in review.

In `CreateCanvas`, after the raycaster:

```csharp
            // Pixel art: no sub-pixel placement, or a 1-art-pixel line lands across two screen pixels.
            canvas.pixelPerfect = true;
            FocusPointer.Ensure(canvas);
```

- [ ] **Step 6: Run the tests**

Run: `bash .superpowers/rt.sh editor UiKitTests`. Expected: 7 passed.
Then run: `bash .superpowers/strict.sh both t3`. Expected: green.

`LayoutTests` must still pass, because the legacy screens changed font. A spill means a box sized for the built-in font is now too small. If so, fix the box in that screen, not the font size: that is the spec's "fix overflow through layout, not smaller text".

- [ ] **Step 7: Commit (when asked).** Message: `Add the themed UI kit and re-skin the legacy helpers`

---

### Task 4: ScreenStack (one foreground screen owns input, focus and Esc)

**Files:**
- Create: `Assets/Game/Scripts/UI/Kit/ScreenStack.cs`
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs`: `Update()` routes Esc through the stack, and `SyncGameplayInput` uses `Screens.Count`.
- Modify: `GameRoot.Menus.cs`, `.Progression.cs`, `.Styles.cs`, `.Records.cs` and `.Cheats.cs`: open and close go through `Screens.Push` and `Screens.Pop`.
- Test: `Assets/Game/Tests/EditMode/ScreenStackTests.cs`

**Interfaces:**
- Produces:
  - `class ScreenStack` with:
    - `void Push(GameObject root, Func<GameObject> defaultFocus, Action onEscape)`.
    - `bool Pop()`, which returns whether anything was popped.
    - `bool Escape()`, which runs the top's `onEscape`, or pops if that is null, and returns whether the stack handled the press.
    - `int Count`, `GameObject Top`, `bool Contains(GameObject)`, `void Clear()` and `void Tick(float unscaledNow)`.
  - `GameRoot.Screens`, typed `ScreenStack`.
- The **main menu** and **pause menu** *are* on the stack, as its bottom entry. That is what hides them while Character or Settings is up, which the spec requires.
  - The main menu is pushed with an `onEscape` that does nothing.
  - The pause menu is pushed with an `onEscape` of `() => SetMenuOpen(false)`.
  - `ShowMainMenu()` becomes `Screens.Clear(); Screens.Push(Main.gameObject, () => Main.DefaultFocus, () => { });`.
  - `SetMenuOpen(true)` pushes `Menu`, and `SetMenuOpen(false)` pops it.
- The HUD and the run-flow panels (offers, results, banner) are **not** on the stack. They are driven by `sim.State`, and the existing results-screen rule ("Esc there does nothing") stays in `SetMenuOpen`.
- Existing tests assert `Main.IsOpen == false` while Tree, Styles or Records is open. The stack satisfies that by deactivating the main menu.

Why a stack and not today's `if (Settings.IsOpen) ... else if (CheatPanel.IsOpen)` chains: there are five such chains across the partials. Each new screen (Character, Training, confirm dialogs, Details) would add a branch to every one of them. Focus restore is currently missing because no single place knows what was focused before.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowedHex.Tests
{
    public class ScreenStackTests
    {
        GameObject es, a, b, opener;
        ScreenStack stack;

        [SetUp]
        public void SetUp()
        {
            es = new GameObject("ES", typeof(EventSystem));
            EventSystem.current = es.GetComponent<EventSystem>();
            a = new GameObject("A", typeof(RectTransform), typeof(CanvasGroup));
            b = new GameObject("B", typeof(RectTransform), typeof(CanvasGroup));
            opener = new GameObject("Opener");
            stack = new ScreenStack();
        }
        [TearDown] public void TearDown() { foreach (var g in new[] { es, a, b, opener }) Object.DestroyImmediate(g); }

        [Test]
        public void PushHidesTheScreenBelowAndPopRestoresItAndItsFocus()
        {
            EventSystem.current.SetSelectedGameObject(opener);
            stack.Push(a, () => a, null);
            stack.Push(b, () => b, null);
            Assert.IsFalse(a.activeSelf, "only the top screen shows (spec: hide parent controls)");
            Assert.AreSame(b, EventSystem.current.currentSelectedGameObject);
            stack.Pop();
            Assert.IsTrue(a.activeSelf);
            Assert.AreSame(a, EventSystem.current.currentSelectedGameObject, "focus returns to what opened B");
            stack.Pop();
            Assert.AreSame(opener, EventSystem.current.currentSelectedGameObject);
        }

        // Review Focus 3.
        [Test]
        public void EscDuringFadeClosesOnlyTheTop()
        {
            stack.Push(a, null, null);
            stack.Push(b, null, null);
            stack.Tick(0f);                 // b mid-fade
            Assert.IsTrue(stack.Escape());
            Assert.AreEqual(1, stack.Count);
            Assert.AreSame(a, stack.Top);
            Assert.IsTrue(a.activeSelf);
        }

        [Test]
        public void EscapeHandlerOverridesThePop()
        {
            int asked = 0;
            stack.Push(a, null, () => asked++);   // e.g. a dirty form asking "discard?"
            Assert.IsTrue(stack.Escape());
            Assert.AreEqual(1, asked); Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void EmptyStackLeavesEscToTheGame() => Assert.IsFalse(stack.Escape());

        [Test]
        public void FadeRunsOnUnscaledTimeAndEndsOpaque()
        {
            stack.Push(a, null, null);
            stack.Tick(100f); stack.Tick(100f + ScreenStack.FadeSeconds * 0.5f);
            Assert.AreEqual(0.5f, a.GetComponent<CanvasGroup>().alpha, 0.05f);
            stack.Tick(100f + ScreenStack.FadeSeconds + 0.01f);
            Assert.AreEqual(1f, a.GetComponent<CanvasGroup>().alpha, 1e-4);
        }

        [Test]
        public void PushingTheSameScreenTwiceIsANoOp()
        {
            stack.Push(a, null, null); stack.Push(a, null, null);
            Assert.AreEqual(1, stack.Count);
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The foreground screen router (spec section 4.2). Only the top screen is active; pushing
    /// remembers what was focused and popping gives it back. Esc asks the top screen first.
    /// A plain class ticked by GameRoot, so tests drive it without frames.
    /// </summary>
    public sealed class ScreenStack
    {
        public const float FadeSeconds = 0.12f;

        sealed class Entry
        {
            public GameObject Root; public Func<GameObject> DefaultFocus; public Action OnEscape;
            public GameObject FocusBefore; public float FadeFrom = -1f;
        }

        readonly List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;
        public GameObject Top => entries.Count > 0 ? entries[entries.Count - 1].Root : null;
        public bool Contains(GameObject root) => entries.Exists(e => e.Root == root);

        public void Push(GameObject root, Func<GameObject> defaultFocus, Action onEscape)
        {
            if (root == null || Contains(root)) return;
            var es = EventSystem.current;
            var e = new Entry { Root = root, DefaultFocus = defaultFocus, OnEscape = onEscape,
                                FocusBefore = es != null ? es.currentSelectedGameObject : null };
            if (entries.Count > 0) entries[entries.Count - 1].Root.SetActive(false);
            entries.Add(e);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            var cg = root.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;       // Tick starts the fade on its first call
            Focus(defaultFocus?.Invoke());
        }

        public bool Pop()
        {
            if (entries.Count == 0) return false;
            var top = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            if (top.Root != null) top.Root.SetActive(false);
            if (entries.Count > 0)
            {
                var below = entries[entries.Count - 1];
                below.Root.SetActive(true);
                var cg = below.Root.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;     // returning is instant: no fade-in on the way back
            }
            Focus(top.FocusBefore);
            return true;
        }

        /// <summary>Esc: the top screen's handler, or a pop. False when nothing is open (the game decides).</summary>
        public bool Escape()
        {
            if (entries.Count == 0) return false;
            var top = entries[entries.Count - 1];
            if (top.OnEscape != null) top.OnEscape(); else Pop();
            return true;
        }

        public void Clear() { while (entries.Count > 0) Pop(); }

        /// <summary>Advance the top screen's fade on unscaled time (menus fade while the run is paused).</summary>
        public void Tick(float now)
        {
            if (entries.Count == 0) return;
            var top = entries[entries.Count - 1];
            var cg = top.Root != null ? top.Root.GetComponent<CanvasGroup>() : null;
            if (cg == null) return;
            if (top.FadeFrom < 0f) top.FadeFrom = now;
            cg.alpha = Mathf.Clamp01((now - top.FadeFrom) / FadeSeconds);
        }

        static void Focus(GameObject go)
        {
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(go != null && go.activeInHierarchy ? go : null);
        }
    }
}
```

Step 1's restore test pops B to reach A. A was hidden while B was up, so A's `FocusBefore` is the opener, and B's is A's default focus. `Focus(top.FocusBefore)` restores exactly that.

- [ ] **Step 4: Wire `GameRoot`**

In `GameRoot.cs`:
1. Add `public ScreenStack Screens { get; } = new ScreenStack();`.
2. In `Update()`, replace the Esc branch chain with:

```csharp
            if (pausePressed)
            {
                // One rule (plan Task 4): the top screen handles Esc. Only with nothing stacked
                // does Esc reach the game, and then only to OPEN pause: the pause menu is on the
                // stack, so closing it is its own onEscape (resume).
                if (!Screens.Escape() && !InMainMenu) SetMenuOpen(true);
                SyncGameplayInput();
            }
            Screens.Tick(Time.unscaledTime);
```

3. In `SyncGameplayInput`, replace `!Settings.IsOpen` (and any other per-panel checks there) with `Screens.Count == 0`.

In every `Open*`/`Close*` method across the partials, replace the hand-written show/hide and focus code:
- Open becomes `X.Show(...)` plus `Screens.Push(X.gameObject, () => X.DefaultFocus, null)`.
- Close becomes `Screens.Pop()`.
- The `CloseSubMenus` chain becomes `Screens.Clear()`. Keep the partial-method declarations as empty hooks, so this change does not have to touch the owner's files.

Each panel's `Show` stops calling `SetSelectedGameObject` itself. It exposes `public GameObject DefaultFocus` instead, which is the button that `Show` used to select.

Every panel root gets a `CanvasGroup` (add one in each `Create`), so the fade works.

- [ ] **Step 5: Run the tests**

Run: `bash .superpowers/rt.sh editor ScreenStackTests` (6 passed), then `bash .superpowers/strict.sh both t4`. The existing `GameRootPlayModeTests` Esc tests must stay green unchanged: they are the regression net for this rewiring.

- [ ] **Step 6: Commit (when asked).** Message: `Route every sub-screen through one stack that owns focus and Esc`

---

### Task 5: Atmosphere (vignette, ash and embers, the title)

**Files:**
- Create: `Assets/Game/Scripts/UI/Kit/Atmosphere.cs`
- Create: `Assets/Game/Scripts/UI/Kit/PixelGeometry.cs` (the vignette texture here; rings and paths are added in Task 10)
- Test: `Assets/Game/Tests/EditMode/AtmosphereTests.cs`

**Interfaces:**
- Produces:
  - `Atmosphere.Create(Transform parent)` returns `Atmosphere`, which has:
    - `void SetActive(bool)`
    - `void Tick(float unscaledNow)`
    - `int LiveMotes`
    - `float TitleGlow(float now)` (static: the pulse curve, 0..1)
  - `PixelGeometry.Vignette(int w, int h)` returns `Texture2D` (point-filtered, alpha 0 at the centre, rising to 0.85 at the corners, in 8 hard bands for a dithered pixel look).
- Consumes: `WorldArt/Ash Fall`, `WorldArt/Embers Ambient` and `WorldArt/Dust Motes` (already imported by WorldArtImporter, git-ignored, 32-px cells) and `DisplayOptions.ReduceFlashes`.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class AtmosphereTests
    {
        [Test]
        public void VignetteIsClearInTheMiddleAndDarkAtTheCorners()
        {
            var t = PixelGeometry.Vignette(64, 36);
            Assert.AreEqual(FilterMode.Point, t.filterMode);
            Assert.Less(t.GetPixel(32, 18).a, 0.05f);
            Assert.Greater(t.GetPixel(0, 0).a, 0.7f);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void VignetteUsesHardBandsNotASmoothGradient()
        {
            var t = PixelGeometry.Vignette(64, 36);
            var seen = new System.Collections.Generic.HashSet<float>();
            for (int x = 0; x < 64; x++) seen.Add(Mathf.Round(t.GetPixel(x, 18).a * 100f));
            Assert.LessOrEqual(seen.Count, PixelGeometry.VignetteBands + 1);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void TitleGlowBreathesSlowlyAndIsSteadyUnderReduceFlashes()
        {
            bool was = DisplayOptions.ReduceFlashes;
            try
            {
                DisplayOptions.ReduceFlashes = false;
                Assert.AreNotEqual(Atmosphere.TitleGlow(0f), Atmosphere.TitleGlow(1.5f), 1e-3);
                // A 4 s breath: never faster than 0.25 Hz, so it is ambience, not a flash.
                Assert.AreEqual(Atmosphere.TitleGlow(0f), Atmosphere.TitleGlow(Atmosphere.BreathSeconds), 1e-4);
                DisplayOptions.ReduceFlashes = true;
                Assert.AreEqual(Atmosphere.TitleGlow(0f), Atmosphere.TitleGlow(1.5f), 1e-6);
            }
            finally { DisplayOptions.ReduceFlashes = was; }
        }

        [Test]
        public void WithoutTheWorldArtTheLayerIsJustTheVignette()
        {
            var go = new GameObject("P", typeof(RectTransform));
            var a = Atmosphere.Create(go.transform);
            a.Tick(0f); a.Tick(1f);
            Assert.GreaterOrEqual(a.LiveMotes, 0);   // no throw either way
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement**

```csharp
// PixelGeometry.cs
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Runtime textures for UI geometry drawn on the pixel grid (plan 0.6): no anti-aliasing,
    /// point filtering, hard steps. Generated, not shipped, so they need no licence and no import.
    /// </summary>
    public static class PixelGeometry
    {
        public const int VignetteBands = 8;

        static Texture2D New(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>Elliptical darkening, quantised to bands so it reads as pixel art (low-res, scaled up).</summary>
        public static Texture2D Vignette(int w, int h)
        {
            var t = New(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.9f);
                    float a = Mathf.Floor(d * VignetteBands) / VignetteBands * 0.85f / ((VignetteBands - 1f) / VignetteBands);
                    px[y * w + x] = new Color32(7, 5, 11, (byte)(Mathf.Clamp01(a) * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }
    }
}
```

```csharp
// Atmosphere.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>
    /// The nightmare layer behind every front-end screen (plan 0.4): a banded vignette over the
    /// idle arena, plus slow ash and embers drifting up from the bottom edge. Motes reuse the
    /// licensed WorldArt effect sheets; without them the layer is just the vignette. Nothing here
    /// flashes: motes fade in and out over seconds, and Reduce flashes freezes the title breath.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        public const float BreathSeconds = 4f;
        const int MaxMotes = 18;
        const float MoteLife = 7f;

        sealed class Mote { public Image Img; public Sprite[] Frames; public float Born, X, Speed, Sway; }
        readonly List<Mote> motes = new List<Mote>();
        readonly List<Sprite[]> sheets = new List<Sprite[]>();
        RectTransform rt;
        float nextSpawn;
        System.Random rng = new System.Random(7);

        public int LiveMotes => motes.Count;

        /// <summary>0..1 brightness of the title's violet aura. Constant under Reduce flashes.</summary>
        public static float TitleGlow(float now) => DisplayOptions.ReduceFlashes ? 0.6f
            : 0.6f + 0.4f * Mathf.Sin(now / BreathSeconds * Mathf.PI * 2f);

        public static Atmosphere Create(Transform parent)
        {
            var rt = Ui.Rect("Atmosphere", parent);
            Ui.Stretch(rt);
            var a = rt.gameObject.AddComponent<Atmosphere>();
            a.rt = rt;
            // Low-res texture, scaled up: the bands become chunky pixel steps on screen.
            var v = rt.gameObject.AddComponent<RawImage>();
            v.texture = PixelGeometry.Vignette(96, 54);
            v.raycastTarget = false;
            foreach (var name in new[] { "Ash Fall", "Embers Ambient", "Dust Motes" })
            {
                var tex = Resources.Load<Texture2D>("WorldArt/" + name);
                if (tex == null) continue;
                int n = tex.width / 32;
                var f = new Sprite[n];
                for (int i = 0; i < n; i++) f[i] = Sprite.Create(tex, new Rect(i * 32, 0, 32, 32), new Vector2(0.5f, 0.5f), UiSkin.PixelsPerUnit);
                sheets.Add(f);
            }
            return a;
        }

        public void Tick(float now)
        {
            if (sheets.Count > 0 && motes.Count < MaxMotes && now >= nextSpawn)
            {
                nextSpawn = now + 0.45f;
                var frames = sheets[rng.Next(sheets.Count)];
                var img = Ui.Image("Mote", rt, new Color(1, 1, 1, 0));
                img.raycastTarget = false; img.sprite = frames[0];
                img.rectTransform.sizeDelta = new Vector2(64, 64);   // 32-px cell at 2x
                motes.Add(new Mote { Img = img, Frames = frames, Born = now, X = (float)rng.NextDouble(),
                                     Speed = 40f + (float)rng.NextDouble() * 50f, Sway = (float)rng.NextDouble() * 6.28f });
            }
            var size = rt.rect.size;
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                float age = now - m.Born;
                if (age > MoteLife) { Destroy(m.Img.gameObject); motes.RemoveAt(i); continue; }
                // Rise from below the bottom edge with a slow sideways sway; fade in, then out.
                float x = (m.X - 0.5f) * size.x + Mathf.Sin(age * 0.6f + m.Sway) * 30f;
                float y = -size.y * 0.5f - 32f + age * m.Speed;
                // Whole reference pixels only, in steps of 2 (one art pixel), so motes never shimmer.
                m.Img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x / 2f) * 2f, Mathf.Round(y / 2f) * 2f);
                float a = Mathf.Min(1f, age / 1.5f) * Mathf.Min(1f, (MoteLife - age) / 2f) * 0.55f;
                m.Img.color = new Color(1, 1, 1, a);
                m.Img.sprite = m.Frames[(int)(age * 8f) % m.Frames.Length];
            }
        }

        void Update() => Tick(Time.unscaledTime);
    }
}
```

- [ ] **Step 4: Run.** Run: `bash .superpowers/rt.sh editor AtmosphereTests` (4 passed).

- [ ] **Step 5: Commit (when asked).** Message: `Add the menu atmosphere: banded vignette and drifting ash`

---

## Phase B: Front-end screens

### Task 6: Main menu (three destinations, mode selector, Training, routing that keeps the old keys)

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/MenuRouting.cs`
- Create: `Assets/Game/Scripts/UI/Screens/TrainingMenu.cs`
- Rewrite: `Assets/Game/Scripts/UI/MainMenu.cs`. Public API kept: `AddEntry`, `SetEntry`, `EnableEntry`, `Press`, `IsEntryEnabled`, `SetProfileLine`, `SetWarning`, `Show` and `IsOpen`. New: `DefaultFocus`, `SetStatus(MenuStatus)` and `SetCheatNotice(bool)`.
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.Menus.cs` (entry labels, `RefreshMainMenu` feeds `SetStatus`)
- Test: `Assets/Game/Tests/EditMode/MenuRoutingTests.cs`, plus additions to `Assets/Game/Tests/PlayMode/LayoutTests.cs`

**Interfaces:**
- Consumes: `UiKit`, `ScreenStack`, `Atmosphere` and `UiGlyphs` (Task 9; until then the badge uses a `gem.1` sprite).
- Produces:
  - `enum MenuSlot { Play, Mode, Character, Records, Training, Settings, Cheats, Quit, Hidden }`.
  - `MenuRouting.SlotFor(string key)` returns `MenuSlot`.
  - `MenuRouting.ModeKeys`: `{ "play_short", "endless" }`, in selector order.
  - `MenuRouting.TrainingKeys`: `{ "tutorial", "practice" }`.
  - `struct MenuStatus { int Mastery; string StyleName; int Points; float XpFraction; }`.
  - `MainMenu.SelectedMode`, a session-static `string`, default `"play_short"`.

How the old keys land:

| Key | Where it lives now |
|---|---|
| `play_short`, `endless` | the mode selector, launched by **Play** |
| `mastery` | **Character**, opened on the Skills tab |
| `style` | no button of its own: Character, opened on the Capture Style tab. `Press("style")` still works. |
| `records` | **Records** |
| `tutorial`, `practice` | the **Training** menu (top-right) |
| `settings` | the gear icon (top-right) |
| `cheats` | quiet footer, left |
| `quit` | quiet footer, right; absent on WebGL |

Layout at the 1920×1080 reference. Everything is anchored, so it holds at any aspect ratio:
- **Left column:** 640 wide, starting 128 from the left edge.
- **Top of the column:** the title "Borrowed Hex" (alagard 96, Honey, with a 2-px Violet outline breathing via `Atmosphere.TitleGlow`), then a `filigree.a` flourish under it.
- **Main controls:**
  - At y −320, the mode tabs (Short run | Endless).
  - At y −400, **Play** (Primary, 560×80). Its cheat notice ("Cheats active, progression disabled", Warning colour, Body) sits 16 px to its right, inside the column's 640 px when wrapped, or under Play below 1600 px wide.
  - At y −504, **Character** (Secondary, 560×64), with the badge "◆ 2" in Honey at its right end when points are greater than 0.
  - At y −584, **Records** (Secondary).
- **Status block,** at y −680: "Mastery 3 · Snatcher" (Body, Muted) over a `bar.tray` XP bar (320×26) with a `fill.green` fill.
- **Top right:** **Training** (Quiet) and the **Settings** icon button (`btn.options`, with tooltip "Settings").
- **Footer, 48 from the bottom:** **Cheats** (Quiet, left), **Quit** (Quiet, right), and the save warning centred on a parchment strip, shown only when non-empty. Save failures are a separate line from the cheat notice, as the spec requires.
- **Background:** no full-screen dim. A left-to-right `Scrim` gradient (an `Image` with a 64×1 generated texture, opaque at the left fading to 0 by 55%) keeps the column readable while the arena and the rogue stay visible on the right, with `Atmosphere` over everything.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class MenuRoutingTests
    {
        [TestCase("play_short", MenuSlot.Mode)]
        [TestCase("endless", MenuSlot.Mode)]
        [TestCase("mastery", MenuSlot.Character)]
        [TestCase("style", MenuSlot.Hidden)]
        [TestCase("records", MenuSlot.Records)]
        [TestCase("tutorial", MenuSlot.Training)]
        [TestCase("practice", MenuSlot.Training)]
        [TestCase("settings", MenuSlot.Settings)]
        [TestCase("cheats", MenuSlot.Cheats)]
        [TestCase("quit", MenuSlot.Quit)]
        public void EveryLegacyKeyHasAHome(string key, MenuSlot slot) => Assert.AreEqual(slot, MenuRouting.SlotFor(key));

        [Test]
        public void UnknownKeysAreHiddenNotLost() => Assert.AreEqual(MenuSlot.Hidden, MenuRouting.SlotFor("future_mode"));

        [Test]
        public void ShortRunIsTheFirstMode() => Assert.AreEqual("play_short", MenuRouting.ModeKeys[0]);
    }
}
```

Add to `LayoutTests.cs`. It runs on the main menu, so open it in the test:

```csharp
        [UnityTest]
        public IEnumerator MainMenu_NothingOverlapsAndEveryControlIsOnScreen()
        {
            root.ShowMainMenu();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var boxes = new List<(string, Rect)>();
            foreach (var b in root.Main.GetComponentsInChildren<Button>(false)) boxes.Add((b.name, ScreenBox((RectTransform)b.transform)));
            foreach (var n in new[] { "Title", "Status", "Warning", "CheatNotice" })
            {
                var t = root.Main.transform.Find(n);
                if (t != null && t.gameObject.activeInHierarchy && t.GetComponent<Text>()?.text != "") boxes.Add((n, ScreenBox((RectTransform)t)));
            }
            AssertNoOverlaps(boxes, "main menu");
            AssertOnScreen(boxes, "main menu");
            AssertTextFits(root.Main.transform, "main menu");
        }
```

- [ ] **Step 2: Run, expect failures.** Run: `bash .superpowers/uc.sh` (compile errors for `MenuRouting` and `MenuSlot`).

- [ ] **Step 3: Implement `MenuRouting`**

```csharp
namespace BorrowedHex.UI
{
    public enum MenuSlot { Play, Mode, Character, Records, Training, Settings, Cheats, Quit, Hidden }

    /// <summary>
    /// Where each legacy main-menu key lives in the three-destination layout (spec 2). Keys stay
    /// the programmatic contract: GameRoot partials and tests keep calling AddEntry/Press with them.
    /// </summary>
    public static class MenuRouting
    {
        public static readonly string[] ModeKeys = { "play_short", "endless" };
        public static readonly string[] TrainingKeys = { "tutorial", "practice" };

        public static MenuSlot SlotFor(string key) => key switch
        {
            "play_short" or "endless" => MenuSlot.Mode,
            "mastery" => MenuSlot.Character,
            "records" => MenuSlot.Records,
            "tutorial" or "practice" => MenuSlot.Training,
            "settings" => MenuSlot.Settings,
            "cheats" => MenuSlot.Cheats,
            "quit" => MenuSlot.Quit,
            _ => MenuSlot.Hidden,   // "style" and any future key: pressable, no button of its own
        };
    }
}
```

- [ ] **Step 4: Rewrite `MainMenu`**

The entry dictionary stays the source of truth: `actions[key]`, `enabled[key]` and `labels[key]`. `AddEntry` no longer creates a button per key. It records the action and refreshes the slot that shows the key. `Press(key)` is unchanged in meaning: it invokes the action when enabled.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    public struct MenuStatus { public int Mastery; public string StyleName; public int Points; public float XpFraction; }

    /// <summary>
    /// The launch screen (spec 2 / plan Task 6): three destinations on the left so the arena and
    /// the rogue stay visible. Entries are still keyed (Phase 8 contract); MenuRouting decides
    /// which visible control each key drives. A disabled key greys its control and, where it has
    /// one, shows the reason in the control's tooltip instead of lengthening the label.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        // The selected mode survives returning to the menu, not a restart of the game (spec).
        public static string SelectedMode = "play_short";

        readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();
        readonly Dictionary<string, bool> enabled = new Dictionary<string, bool>();
        readonly Dictionary<string, string> reasons = new Dictionary<string, string>();
        Button play, character, records, training, settings, cheats, quit;
        UiKit.TabStrip modes;
        Text statusLine, warning, cheatNotice, badge, title;
        Image xpFill;
        Outline titleGlow;
        TrainingMenu trainingMenu;
        Atmosphere atmosphere;

        public bool IsOpen => gameObject.activeSelf;
        public GameObject DefaultFocus => play.gameObject;
        /// <summary>The Training sub-menu, so GameRoot can push it on the screen stack.</summary>
        public TrainingMenu Training => trainingMenu;
        public event Action OpenTraining;

        public static MainMenu Create(Canvas canvas)
        {
            var root = Ui.Rect("MainMenu", canvas.transform);
            Ui.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var menu = root.gameObject.AddComponent<MainMenu>();
            menu.Build(root, canvas);
            return menu;
        }

        void Build(RectTransform root, Canvas canvas)
        {
            // Readability scrim on the left only: the right half stays the living arena.
            var scrim = root.gameObject.AddComponent<RawImage>();
            scrim.texture = LeftScrim(); scrim.raycastTarget = true;   // still blocks clicks into the arena
            atmosphere = Atmosphere.Create(root);

            var col = Ui.Rect("Column", root);
            col.anchorMin = new Vector2(0, 0); col.anchorMax = new Vector2(0, 1); col.pivot = new Vector2(0, 1);
            col.sizeDelta = new Vector2(640, 0); col.anchoredPosition = new Vector2(128, 0);

            title = UiKit.Text("Title", col, "Borrowed Hex", UiFonts.Role.Title);
            Ui.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, -96), new Vector2(640, 112));
            titleGlow = title.gameObject.AddComponent<Outline>();
            titleGlow.effectDistance = new Vector2(2, -2);
            var flourish = Ui.Image("Flourish", col, UiPalette.Camel);
            var fs = UiSkin.Sprite("divider.b");
            if (fs != null) { flourish.sprite = fs; flourish.type = Image.Type.Sliced; flourish.color = Color.white; }
            Ui.Place(flourish.rectTransform, new Vector2(0, 1), new Vector2(0, -216), new Vector2(420, fs != null ? 12 : 2));

            modes = UiKit.Tabs(col, new[] { "Short run", "Endless" }, i => { SelectedMode = MenuRouting.ModeKeys[i]; });
            Ui.Place(modes.Rect, new Vector2(0, 1), new Vector2(0, -288), new Vector2(568, 64));

            play = UiKit.Button("Play", col, "Play", () => Press(SelectedMode), UiKit.Tier.Primary);
            Ui.Place((RectTransform)play.transform, new Vector2(0, 1), new Vector2(0, -376), new Vector2(560, 80));
            cheatNotice = UiKit.Text("CheatNotice", col, "", UiFonts.Role.Body);
            cheatNotice.color = UiPalette.Warning;
            Ui.Place(cheatNotice.rectTransform, new Vector2(0, 1), new Vector2(0, -464), new Vector2(560, 32));

            character = UiKit.Button("Character", col, "Character", () => Press("mastery"));
            Ui.Place((RectTransform)character.transform, new Vector2(0, 1), new Vector2(0, -512), new Vector2(560, 64));
            badge = UiKit.Text("Badge", character.transform, "", UiFonts.Role.Body, TextAnchor.MiddleRight);
            badge.color = UiPalette.Honey;
            Ui.Place(badge.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(120, 48));
            records = UiKit.Button("Records", col, "Records", () => Press("records"));
            Ui.Place((RectTransform)records.transform, new Vector2(0, 1), new Vector2(0, -592), new Vector2(560, 64));

            statusLine = UiKit.Text("Status", col, "", UiFonts.Role.Small);
            Ui.Place(statusLine.rectTransform, new Vector2(0, 1), new Vector2(0, -688), new Vector2(560, 32));
            var tray = Ui.Image("Xp", col, UiPalette.PanelDeep);
            var trayS = UiSkin.Sprite("bar.tray");
            if (trayS != null) { tray.sprite = trayS; tray.type = Image.Type.Sliced; tray.color = Color.white; }
            Ui.Place(tray.rectTransform, new Vector2(0, 1), new Vector2(0, -728), new Vector2(320, 26));
            xpFill = Ui.Image("Fill", tray.transform, UiPalette.Violet);
            var fillS = UiSkin.Sprite("fill.green");
            if (fillS != null) { xpFill.sprite = fillS; xpFill.type = Image.Type.Sliced; xpFill.color = Color.white; }
            xpFill.rectTransform.anchorMin = new Vector2(0, 0); xpFill.rectTransform.anchorMax = new Vector2(0, 1);
            xpFill.rectTransform.offsetMin = new Vector2(16, 9); xpFill.rectTransform.offsetMax = new Vector2(-16, -9);

            training = UiKit.Button("Training", root, "Training", () => OpenTraining?.Invoke(), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)training.transform, new Vector2(1, 1), new Vector2(-136, -40), new Vector2(240, 64));
            settings = UiKit.Button("Settings", root, "", () => Press("settings"), UiKit.Tier.Icon);
            var gear = UiSkin.Frames("btn.options");
            if (gear.Length == 4)
            {
                var img = settings.GetComponent<Image>(); img.sprite = gear[0]; img.type = Image.Type.Simple;
                settings.transition = Selectable.Transition.SpriteSwap;
                settings.spriteState = new SpriteState { highlightedSprite = gear[1], selectedSprite = gear[1], pressedSprite = gear[2], disabledSprite = gear[3] };
            }
            else settings.GetComponentInChildren<Text>().text = "*";
            Ui.Place((RectTransform)settings.transform, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(64, 64));
            UiTooltip.Attach(settings, () => "Settings");

            cheats = UiKit.Button("Cheats", root, "Cheats", () => Press("cheats"), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)cheats.transform, new Vector2(0, 0), new Vector2(128, 48), new Vector2(200, 56));
            quit = UiKit.Button("Quit", root, "Quit", () => Press("quit"), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)quit.transform, new Vector2(1, 0), new Vector2(-128, 48), new Vector2(200, 56));
            quit.gameObject.SetActive(false);   // shown once AddEntry("quit") arrives (never on WebGL)
            warning = UiKit.Text("Warning", root, "", UiFonts.Role.Body, TextAnchor.MiddleCenter);
            warning.color = UiPalette.Warning;
            Ui.Place(warning.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(1000, 64));

            trainingMenu = TrainingMenu.Create(canvas, k => Press(k));
            Refresh();
        }

        static Texture2D LeftScrim()
        {
            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
            {
                // Opaque ink to 30%, stepping down to clear by 55%: hard steps, pixel style.
                float u = x / 63f, a = u < 0.3f ? 0.86f : u > 0.55f ? 0f : Mathf.Floor((0.55f - u) / 0.25f * 4f) / 4f * 0.86f;
                t.SetPixel(x, 0, new Color(UiPalette.Ink.r, UiPalette.Ink.g, UiPalette.Ink.b, a));
            }
            t.Apply(false);
            return t;
        }

        public Button AddEntry(string key, string label, Action onClick, string disabledReason = null)
        {
            actions[key] = onClick;
            if (key == "quit") quit.gameObject.SetActive(true);
            SetEntry(key, onClick != null, disabledReason);
            return ControlFor(key);
        }

        public void SetEntry(string key, bool on, string disabledReason = null)
        {
            enabled[key] = on;
            reasons[key] = disabledReason;
            Refresh();
        }

        public void EnableEntry(string key, Action onClick) { actions[key] = onClick; SetEntry(key, onClick != null); }

        public void Press(string key)
        {
            if (IsEntryEnabled(key) && actions.TryGetValue(key, out var a)) a?.Invoke();
        }

        public bool IsEntryEnabled(string key) => enabled.TryGetValue(key, out var on) && on;

        Button ControlFor(string key) => MenuRouting.SlotFor(key) switch
        {
            MenuSlot.Mode => play, MenuSlot.Character => character, MenuSlot.Records => records,
            MenuSlot.Training => training, MenuSlot.Settings => settings, MenuSlot.Cheats => cheats,
            MenuSlot.Quit => quit, _ => null,
        };

        void Refresh()
        {
            if (play == null) return;
            // The Endless tab greys out (it stays visible) while its key is disabled.
            for (int i = 0; i < MenuRouting.ModeKeys.Length; i++)
            {
                var tab = modes.Rect.GetChild(i).GetComponent<Button>();
                tab.interactable = IsEntryEnabled(MenuRouting.ModeKeys[i]);
            }
            if (!IsEntryEnabled(SelectedMode)) SelectedMode = MenuRouting.ModeKeys[0];
            modes.Select(System.Array.IndexOf(MenuRouting.ModeKeys, SelectedMode));
            play.interactable = IsEntryEnabled(SelectedMode);
            character.interactable = IsEntryEnabled("mastery");
            records.interactable = IsEntryEnabled("records");
            training.interactable = IsEntryEnabled("tutorial") || IsEntryEnabled("practice");
            settings.interactable = IsEntryEnabled("settings");
            cheats.interactable = IsEntryEnabled("cheats");
            trainingMenu.SetEnabled(IsEntryEnabled("tutorial"), IsEntryEnabled("practice"));
        }

        public void SetStatus(MenuStatus s)
        {
            statusLine.text = $"Mastery {s.Mastery}  ·  {s.StyleName}";
            badge.text = s.Points > 0 ? $"◆ {s.Points}" : "";
            xpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.XpFraction), 1);
        }

        /// <summary>Kept for GameRoot's existing call; the status block replaces the old free-text line.</summary>
        public void SetProfileLine(string s) { }
        public void SetWarning(string s) => warning.text = s ?? "";
        public void SetCheatNotice(bool on) => cheatNotice.text = on ? "Cheats active — progression disabled" : "";

        public void Show(bool on) => gameObject.SetActive(on);   // focus: ScreenStack (Task 4)

        void Update()
        {
            var c = UiPalette.Violet; c.a = Atmosphere.TitleGlow(Time.unscaledTime);
            titleGlow.effectColor = c;
        }
    }
}
```

Glyph check: confirm in a test capture that alagard and m5x7 contain `·`, `◆` and `—`. If a glyph is missing, Unity falls back per character to the OS font, which would look wrong. In that case substitute `-`, `*` and `-`. Step 6 covers this.

```csharp
// TrainingMenu.cs
using System;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedHex.UI
{
    /// <summary>A small framed menu: Tutorial and Practice (spec "Training opens a small menu").</summary>
    public sealed class TrainingMenu : MonoBehaviour
    {
        Button tutorial, practice, back;
        public GameObject DefaultFocus => tutorial.interactable ? tutorial.gameObject : practice.gameObject;
        public event Action Back;

        public static TrainingMenu Create(Canvas canvas, Action<string> press)
        {
            var scrim = Ui.Image("TrainingMenu", canvas.transform, UiPalette.Scrim);
            Ui.Stretch(scrim.rectTransform);
            scrim.gameObject.AddComponent<CanvasGroup>();
            var m = scrim.gameObject.AddComponent<TrainingMenu>();
            var frame = UiKit.Frame("Panel", scrim.transform, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 400));
            var head = UiKit.Text("Heading", frame.transform, "Training", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(560, 64));
            m.tutorial = UiKit.Button("Tutorial", frame.transform, "Tutorial", () => press("tutorial"));
            Ui.Place((RectTransform)m.tutorial.transform, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(480, 64));
            m.practice = UiKit.Button("Practice", frame.transform, "Practice", () => press("practice"));
            Ui.Place((RectTransform)m.practice.transform, new Vector2(0.5f, 1), new Vector2(0, -208), new Vector2(480, 64));
            m.back = UiKit.Button("Back", frame.transform, "Back", () => m.Back?.Invoke(), UiKit.Tier.Quiet);
            Ui.Place((RectTransform)m.back.transform, new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(240, 56));
            scrim.gameObject.SetActive(false);
            return m;
        }

        public void SetEnabled(bool tut, bool prac) { tutorial.interactable = tut; practice.interactable = prac; }
    }
}
```

- [ ] **Step 5: Wire `GameRoot.Menus.cs`**

In `BuildMenus`, keep every `AddEntry` call and its key. Make two changes:
- `endless` is no longer "later build" here, because `BuildEndlessMenus` enables it.
- Add the Training wiring:

```csharp
            Main.OpenTraining += () => Screens.Push(Main.Training.gameObject, () => Main.Training.DefaultFocus, null);
            Main.Training.Back += () => Screens.Pop();
```

In `RefreshMainMenu`, replace the profile-line and warning composition with:

```csharp
            var m = Profile.Profile.mastery;
            Main.SetStatus(new MenuStatus
            {
                Mastery = m.level,
                StyleName = CaptureStyles.Resolve(Profile.Profile.styleId).Name,
                Points = m.points,
                XpFraction = m.level >= Mastery.MaxLevel ? 1f : m.xp / (float)Mastery.CostToAdvance(m.level),
            });
            Main.SetWarning(Profile.Warning);        // save failures only: readable, on their own line
            Main.SetCheatNotice(Cheats.AnyActive);
```

Starting a run from the Training menu must clear it: `StartRun` already calls `Screens.Clear()` through `CloseSubMenus` (Task 4).

- [ ] **Step 6: Run and capture**

1. Run `bash .superpowers/rt.sh editor MenuRoutingTests` and `bash .superpowers/rtp.sh LayoutTests`.
2. Run `bash .superpowers/rtp.sh GameRootPlayModeTests`. All existing `Main.Press` tests must pass unchanged. They are the proof that the routing kept the keys.
3. Capture the main menu with `bash .superpowers/cap.sh menu-main`, once with the art and once with `UiSkin.ForceFlat = true` set through `ev.sh`.
4. Check in the captures that `·`, `◆` and `—` render in the pixel faces, and apply the Step 4 fallback for any that do not.

- [ ] **Step 7: Gate and commit (when asked).** Message: `Rebuild the main menu around Play, Character and Records`

---

### Task 7: Settings and Cheats as labelled rows

**Files:**
- Rewrite: `Assets/Game/Scripts/UI/SettingsPanel.cs` (`DisplayOptions` stays in this file, unchanged)
- Rewrite: `Assets/Game/Scripts/UI/CheatsPanel.cs`
- Test: `Assets/Game/Tests/EditMode/SettingsRowsTests.cs`

**Interfaces:**
- Consumes: `UiKit.ToggleRow`, `UiKit.StepperRow` and `ScreenStack`.
- Produces:
  - `SettingsPanel.Show(ProfileSettings, Action changed, Action back)`, the same signature.
  - `SettingsPanel.DefaultFocus`.
  - Static helpers for the tests: `SettingsPanel.StepScale(float current, int dir)` returning `float`, which clamps at the ends rather than wrapping, and `SettingsPanel.StepDisplay(int mode, int dir, bool allowWindowed)` returning `int`.
  - `CheatsPanel` keeps its public API; it gains `DefaultFocus`.

The stepper clamps where the old cycle wrapped: from 130%, the old cycle's next step was 80%. With a stepper, "›" at 130% wrapping to 80% is a surprise, so the ends clamp. Display mode cycles through a closed list, so it wraps.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class SettingsRowsTests
    {
        [Test] public void ScaleStepsUpAndDown() { Assert.AreEqual(1.15f, SettingsPanel.StepScale(1f, +1), 1e-4); Assert.AreEqual(0.9f, SettingsPanel.StepScale(1f, -1), 1e-4); }
        [Test] public void ScaleClampsAtTheEnds() { Assert.AreEqual(1.3f, SettingsPanel.StepScale(1.3f, +1), 1e-4); Assert.AreEqual(0.8f, SettingsPanel.StepScale(0.8f, -1), 1e-4); }
        [Test] public void AnOffListScaleSnapsToTheNearestStep() => Assert.AreEqual(1.15f, SettingsPanel.StepScale(1.07f, +1), 1e-4);

        // Platform rule kept: windowed only where allowed (desktop). Order: as launched, fullscreen, windowed.
        [Test] public void DisplayCyclesThroughWhatThePlatformAllows()
        {
            Assert.AreEqual(1, SettingsPanel.StepDisplay(-1, +1, true));
            Assert.AreEqual(0, SettingsPanel.StepDisplay(1, +1, true));
            Assert.AreEqual(-1, SettingsPanel.StepDisplay(0, +1, true));
            Assert.AreEqual(-1, SettingsPanel.StepDisplay(1, +1, false), "no windowed on web");
            Assert.AreEqual(0, SettingsPanel.StepDisplay(-1, -1, true));
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement**

The `SettingsPanel` builder (it replaces `Create` and `Refresh`; `Show` keeps its signature):

```csharp
        static readonly float[] Scales = { 0.8f, 0.9f, 1f, 1.15f, 1.3f };

        public static float StepScale(float current, int dir)
        {
            int nearest = 0;
            for (int i = 1; i < Scales.Length; i++)
                if (Mathf.Abs(Scales[i] - current) < Mathf.Abs(Scales[nearest] - current)) nearest = i;
            // An off-list value (hand-edited save) counts as already past its nearest step in the
            // direction of travel only if it lies beyond it; otherwise it snaps to that step.
            bool beyond = dir > 0 ? current > Scales[nearest] + 1e-3f : current < Scales[nearest] - 1e-3f;
            bool on = Mathf.Abs(current - Scales[nearest]) < 1e-3f;
            int target = on || beyond ? nearest + dir : nearest;
            return Scales[Mathf.Clamp(target, 0, Scales.Length - 1)];
        }

        public static int StepDisplay(int mode, int dir, bool allowWindowed)
        {
            var order = allowWindowed ? new[] { -1, 1, 0 } : new[] { -1, 1 };
            int i = System.Array.IndexOf(order, mode); if (i < 0) i = 0;
            return order[(i + dir + order.Length) % order.Length];
        }

        void Build(RectTransform root)
        {
            var frame = UiKit.Frame("Panel", root, UiKit.FrameKind.Ornate);
            Ui.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 560));
            var head = UiKit.Text("Heading", frame.transform, "Settings", UiFonts.Role.Heading, TextAnchor.MiddleCenter);
            Ui.Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 64));
            var body = Ui.Rect("Rows", frame.transform);
            Ui.Place(body, new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(752, 4 * UiKit.RowHeight + 3 * 16));
            Ui.Column(body, 16);
            rows.Add(UiKit.StepperRow(body, "Display", () => settings.displayMode switch { 1 => "Fullscreen", 0 => "Windowed", _ => "As launched" },
                d => { settings.displayMode = StepDisplay(settings.displayMode, d, allowWindowed); onChanged?.Invoke(); }));
            rows.Add(UiKit.StepperRow(body, "Interface scale", () => $"{Mathf.RoundToInt(settings.uiScale * 100)}%",
                d => { settings.uiScale = StepScale(settings.uiScale, d); onChanged?.Invoke(); }));
            rows.Add(UiKit.ToggleRow(body, "Reduce flashes", () => settings.reduceFlashes, v => { settings.reduceFlashes = v; onChanged?.Invoke(); }));
            rows.Add(UiKit.ToggleRow(body, "Control hints", () => settings.showHints, v => { settings.showHints = v; onChanged?.Invoke(); }));
            back = UiKit.Button("Back", frame.transform, "Back", () => onBack?.Invoke(), UiKit.Tier.Secondary);
            Ui.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 64));
        }

        public GameObject DefaultFocus => rows[0].Control.gameObject;
        void Refresh() { foreach (var r in rows) r.Refresh(); }
```

There are three fields: `readonly List<UiKit.Row> rows`, `Button back` and `bool allowWindowed`. `Create` makes a `Scrim` root with a `CanvasGroup` and calls `Build`. The `Show` body becomes: set the fields, then `Refresh()`. Do not call `SetSelectedGameObject` there, because `ScreenStack` handles focus.

`CheatsPanel` has the same frame: heading "Cheats", two `ToggleRow`s, the note and Back.
- The first row is "Invincibility" (get `Cheats.Invincible`, set by the existing `onInvincible` flip). The second is "All skills unlocked" (get `Cheats.UnlockAllNodes`).
- The note is one Body line in Muted, verbatim from the spec: "Session only. Active cheats disable XP, records, and achievements."
- The existing `Flip(onX)` callbacks are kept, so `GameRoot.Cheats.cs` is unchanged.

- [ ] **Step 4: Run.** Run: `bash .superpowers/rt.sh editor SettingsRowsTests` (5 passed) and `bash .superpowers/strict.sh both t7`.

- [ ] **Step 5: Commit (when asked).** Message: `Turn Settings and Cheats into labelled toggle and stepper rows`

---

### Task 8: Character screen (header and tabs) and the Capture Style tab

**Files:**
- Create: `Assets/Game/Scripts/UI/Screens/CharacterScreen.cs`
- Rewrite: `Assets/Game/Scripts/UI/StylePanel.cs`. It becomes a view hosted inside `CharacterScreen` and is no longer full-screen. The API is kept: `Show`, `Hide`, `Click(id)`, `IsOpen`, and `static CardBody`.
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.Styles.cs` and `GameRoot.Progression.cs` (both open through `CharacterScreen`)
- Modify: `Assets/Game/Tests/EditMode/StyleTests.cs` (CardBody expectations)
- Test: `Assets/Game/Tests/EditMode/StyleCardTests.cs`

**Interfaces:**
- Consumes: `UiKit.Tabs`, `ScreenStack`, `UiGlyphs` (Task 9 provides the style emblems; until then the emblem slot shows the style's initial in alagard).
- Produces:
  - `CharacterScreen.Create(Canvas)`, `void Open(int tab)` with `CharacterScreen.SkillsTab = 0` and `StyleTab = 1`, `void SetHeader(MasteryState m)`, `RectTransform Body`, `GameObject DefaultFocus`, and `event Action Back`.
  - `int CharacterScreen.LastTab`, static, so the tab is remembered for the session.
  - `StylePanel.CardSummary(CaptureStyle, PlayerProfile, GameConfig)` returns `StyleSummary { string Name; string TradeOff; (string label, string value)[] Compare; }`.
  - `StylePanel.CardBody(...)`, unchanged in signature. It is now the **Details** text: the full stats breakdown, plus "Selected" when selected. The existing test's `Contains` checks still hold, except "SELECTED", which becomes "Selected"; update that one assertion.

Header layout:
- A `frame.ornate` 1600×960 centred, with a heading band 120 tall.
- On the left of the band, "Mastery 4" (alagard 48, Honey) with an XP bar (`bar.tray` 480×26) under it, and "120 / 250 XP" (Small).
- In the middle, the tabs "Skills" and "Capture style".
- On the right, "◆ 2 skill points" (Body, Honey) and the `btn.close` Back icon with tooltip "Back".

`Body` is the rect below the band. `SkillTreePanel` (Task 10) and `StylePanel` parent their content to it, and only the active tab's view is active. That keeps `Tree.IsOpen` and `Styles.IsOpen`, which read `gameObject.activeInHierarchy`, truthful for the existing tests.

Style cards, three across, each 480×560:
- A `portrait` frame (66×72 art, so 132×144 reference) holding the style's emblem glyph.
- The name (alagard Sub, Honey).
- The trade-off sentence (Body).
- A divider.
- Three aligned comparison rows (label left, value right): **Catch cone** "140°", **Catch window** "1.00 s", **Dash** "x1.0". The values come from `Loadout.Resolve(config, ActiveNodes(p), style.Id)` via its `PlayerStats` fields. These are the same fields `CaptureStyles.Describe` prints today: read that method and use the same three fields and formats.
- The selected card uses `frame.cardAlt` with a ✓ gem badge in the top-right corner and the word "Selected" under the name. It is not filled green (spec).
- A Quiet "Details" button under the cards opens a parchment panel (scrollable) with `CardBody` for all three styles.
- The paragraph repeating shared mechanics, and "Click to select", are removed (spec).

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.Data;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class StyleCardTests
    {
        static GameConfig Cfg => Resources.Load<GameConfig>("GameConfig") ?? ScriptableObject.CreateInstance<GameConfig>();

        [Test]
        public void EveryCardComparesTheSameThreeThingsInTheSameOrder()
        {
            var p = new PlayerProfile();
            string[] first = null;
            foreach (var s in CaptureStyles.All)
            {
                var sum = StylePanel.CardSummary(s, p, Cfg);
                Assert.AreEqual(3, sum.Compare.Length, s.Id);
                var labels = System.Array.ConvertAll(sum.Compare, c => c.label);
                if (first == null) first = labels; else CollectionAssert.AreEqual(first, labels, "aligned rows");
                Assert.IsFalse(string.IsNullOrEmpty(sum.TradeOff));
            }
        }

        [Test]
        public void TheCardNoLongerSaysClickToSelect()
        {
            foreach (var s in CaptureStyles.All)
                StringAssert.DoesNotContain("Click to select", StylePanel.CardBody(s, new PlayerProfile(), Cfg, false));
        }

        [Test]
        public void CardValuesFollowOwnedSkills()
        {
            var p = new PlayerProfile();
            var before = StylePanel.CardSummary(CaptureStyles.Resolve(CaptureStyles.Collector), p, Cfg).Compare[0].value;
            p.ownedNodes.Add(SkillTree.PrecisionAngle);
            var after = StylePanel.CardSummary(CaptureStyles.Resolve(CaptureStyles.Collector), p, Cfg).Compare[0].value;
            Assert.AreNotEqual(before, after, "Wide Grasp widens the cone on the card");
        }
    }
}
```

The `GameConfig` load path must match how `StyleTests.Cfg` gets its config. Copy that property verbatim from `StyleTests.cs`.

- [ ] **Step 2: Run, expect a compile failure (`CardSummary`).**

- [ ] **Step 3: Implement `CardSummary` and the new `CardBody`**

```csharp
        public struct StyleSummary { public string Name, TradeOff; public (string label, string value)[] Compare; }

        /// <summary>The card's short face: name, one trade-off sentence, three aligned numbers.</summary>
        public static StyleSummary CardSummary(CaptureStyle style, PlayerProfile p, GameConfig c)
        {
            var st = Loadout.Resolve(c, SkillTree.ActiveNodes(p), style.Id);
            return new StyleSummary
            {
                Name = style.Name, TradeOff = style.TradeOff,
                // Same fields and formats as CaptureStyles.Describe so the card and Details agree.
                Compare = new[]
                {
                    ("Catch cone", $"{st.CaptureConeAngle:0}°"),
                    ("Catch window", $"{st.CaptureWindow:0.00} s"),
                    ("Dash", $"x{st.DashDistanceScale:0.0}"),
                },
            };
        }

        /// <summary>The Details text for one style: everything Describe knows. Static for EditMode tests.</summary>
        public static string CardBody(CaptureStyle style, PlayerProfile p, GameConfig c, bool selected)
        {
            var stats = Loadout.Resolve(c, SkillTree.ActiveNodes(p), style.Id);
            return $"{style.Name}{(selected ? "  — Selected" : "")}\n{style.TradeOff}\n\n{style.Summary}\n\n{CaptureStyles.Describe(stats)}";
        }
```

**Before writing this, read the real field names.** `CaptureWindow` and `DashDistanceScale` are placeholders for whatever `PlayerStats` calls the catch window and the dash distance, so read `CaptureStyles.Describe` and use its exact names. The test pins only labels, count and reactivity, not these names.

Then:
- Build the cards with `UiKit.Frame(...Card)`, `UiKit.Text` and two-column rows.
- Make each card a `Button` whose `onClick` calls `Click(style.Id)`. The behaviour is unchanged: it saves immediately, and re-selecting saves nothing.
- `Refresh()` swaps the card frame sprite (`frame.card` or `frame.cardAlt`) and toggles the ✓ gem and the "Selected" word.

`CharacterScreen` is a scrim root with a `CanvasGroup`, the ornate frame, the header band and `Body`. `Open(tab)` calls `tabs.Select(tab)`, which raises `onSelect`. GameRoot uses that to show the matching view and hide the other (`Tree.Show(...)`/`Styles.Hide()` or the reverse), then stores `LastTab`.

In GameRoot:
- Add `public CharacterScreen Character { get; private set; }`, created in `BuildMenus` before `Tree` and `Styles`, so they can parent their content to `Character.Body`.
- `OpenTree` becomes `Character.Open(CharacterScreen.SkillsTab); Screens.Push(Character.gameObject, () => Character.DefaultFocus, null);`.
- `OpenStyles` is the same with `StyleTab`.
- `Press("mastery")` therefore lands on Skills, and `Press("style")` on Capture Style.

- [ ] **Step 4: Update `StyleTests`.** Change `StringAssert.Contains("SELECTED", dare)` to `StringAssert.Contains("Selected", dare)`. That is the only change.

- [ ] **Step 5: Run.** Run `bash .superpowers/rt.sh editor "StyleCardTests|StyleTests"` and `bash .superpowers/rtp.sh GameRootPlayModeTests` (`TheStylePanel_SavesTheChoice...` must pass unchanged).

- [ ] **Step 6: Commit (when asked).** Message: `Add the Character screen and compact capture-style cards`

---

### Task 9: UiGlyphs (the code-drawn icon set)

**Files:**
- Create: `Assets/Game/Scripts/UI/Kit/UiGlyphs.cs`
- Test: `Assets/Game/Tests/EditMode/UiGlyphsTests.cs`

**Interfaces:**
- Produces:
  - `UiGlyphs.Get(string id)` returns a `Sprite`: 12×12, PPU 25, so 48×48 reference pixels at 4× (one art pixel = 4 reference px, which suits small icons and keeps them on the grid). It returns null for an unknown id.
  - `UiGlyphs.Ids`.
  - The ids:
    - skills: `skill.<SkillTree id>`
    - branches: `branch.precision`, `branch.mobility`, `branch.resilience`, `branch.blood`
    - upgrades: `upgrade.<UpgradeId>`
    - payloads: `payload.bolt`, `payload.riposte`, `payload.rocket`
    - slot states: `state.frozen`, `state.unstable`, `state.fused`, `state.locked`, `state.overcharge`, `state.check`
    - style emblems: `style.<CaptureStyle id>`

The palette for every glyph is a 4-key ramp:
- `.` clear
- `d` dark outline `#15121C`
- `m` mid Camel `#C19149`
- `h` highlight Honey `#DCC47C`
- `w` ivory `#F2E8C9`

Glyphs are **monochrome gold**, so they sit on any frame. Colour meaning (Life red, overcharge gold, frozen blue) comes from the `Image.color` the caller sets. The ramp multiplies cleanly toward a tint because its light end is near white.

This is a deliberate exception to the "full code in the plan" rule: the 40 bitmaps are art, so they are drawn during implementation. The motifs below are the brief. The tests enforce the contract (size, palette, every id present, no two identical, readable silhouettes). Three are given in full to set the style.

| Id | Motif |
|---|---|
| `branch.precision` | an open hand with fingers splayed (grasp) |
| `branch.mobility` | a winged boot |
| `branch.resilience` | a shield with a vertical rune |
| `branch.blood` | a drop over a coin |
| `skill.precision_angle` (Wide Grasp) | two arcs spreading from a point |
| `skill.precision_capacity` (Deep Pockets) | a pouch with three stars |
| `skill.quick_draw` | a hand with a motion streak |
| `skill.mobility_speed` (Light Feet) | a feather |
| `skill.mobility_dash_recovery` (Quick Recovery) | a circular arrow |
| `skill.mobility_dash_distance` (Long Stride) | footprints at a distance |
| `skill.resilience_grace` (Steady Nerves) | a candle flame, upright |
| `skill.resilience_time` (Borrowed Hours) | an hourglass |
| `skill.resilience_dash_grace` (Slippery) | a droplet sliding off a shield |
| `skill.blood_leech` | a fang |
| `skill.blood_siphon` | a chalice |
| `skill.blood_debt` | a quill over a ledger line |
| `upgrade.PiercingReturn` | an arrow through a ring |
| `upgrade.EchoVolley` | three stacked chevrons |
| `upgrade.HeavyOrbit` | a ball on an orbit ellipse |
| `upgrade.PartingGift` | an opened box with a spark |
| `upgrade.FinalSecond` | an hourglass with one grain |
| `upgrade.Overflow` | a cup spilling |
| `upgrade.Fusion` | two circles merging |
| `payload.bolt` | a diagonal bolt |
| `payload.riposte` | crossed blades |
| `payload.rocket` | a flame-tailed shell |
| `state.frozen` | a snowflake |
| `state.unstable` | a jagged crack |
| `state.fused` | a chain link |
| `state.locked` | a padlock |
| `state.overcharge` | a crown or star burst |
| `state.check` | a check mark |
| `style.<id>` (3) | `snatcher`: a grabbing hand; `collector`: an open ledger; `daredevil`: a blade over a wing |

The `style.<id>` keys must match `CaptureStyles.All[i].Id`; read the ids from code.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using BorrowedHex.Progression;
using BorrowedHex.Runs;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class UiGlyphsTests
    {
        static IEnumerable<string> Required()
        {
            foreach (var n in SkillTree.Nodes) yield return "skill." + n.Id;
            foreach (var b in new[] { "precision", "mobility", "resilience", "blood" }) yield return "branch." + b;
            foreach (UpgradeId u in System.Enum.GetValues(typeof(UpgradeId))) yield return "upgrade." + u;
            foreach (var p in new[] { "bolt", "riposte", "rocket" }) yield return "payload." + p;
            foreach (var s in new[] { "frozen", "unstable", "fused", "locked", "overcharge", "check" }) yield return "state." + s;
            foreach (var st in CaptureStyles.All) yield return "style." + st.Id;
        }

        [Test]
        public void EveryThingThatNeedsAnIconHasOne()
        {
            foreach (var id in Required()) Assert.NotNull(UiGlyphs.Get(id), id);
        }

        [Test]
        public void GlyphsAre12By12OnThePixelGrid()
        {
            foreach (var id in UiGlyphs.Ids)
            {
                var s = UiGlyphs.Get(id);
                Assert.AreEqual(12, (int)s.rect.width, id); Assert.AreEqual(12, (int)s.rect.height, id);
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, id);
            }
        }

        [Test]
        public void BitmapsUseOnlyTheRampAndAreSquare()
        {
            foreach (var pair in UiGlyphs.Bitmaps)
            {
                Assert.AreEqual(12, pair.Value.Length, pair.Key);
                foreach (var row in pair.Value)
                {
                    Assert.AreEqual(12, row.Length, pair.Key);
                    foreach (char c in row) StringAssert.Contains(c.ToString(), ".dmhw", pair.Key);
                }
            }
        }

        // A glyph must read at a glance: enough ink, an outline, and not a copy of another one.
        [Test]
        public void GlyphsAreDistinctAndHaveSubstance()
        {
            var seen = new HashSet<string>();
            foreach (var pair in UiGlyphs.Bitmaps)
            {
                string flat = string.Concat(pair.Value);
                Assert.IsTrue(seen.Add(flat), $"{pair.Key} duplicates another glyph");
                int ink = 0, outline = 0;
                foreach (char c in flat) { if (c != '.') ink++; if (c == 'd') outline++; }
                Assert.GreaterOrEqual(ink, 20, $"{pair.Key} too faint to read");
                Assert.Greater(outline, 0, $"{pair.Key} needs a dark outline to read on gold frames");
            }
        }

        [Test] public void UnknownIdsAreNull() => Assert.IsNull(UiGlyphs.Get("nope"));
    }
}
```

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement the class and the three reference glyphs, then draw the rest to the brief**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Code-drawn 12x12 icons (plan 0.5): skills, branches, upgrades, payloads, slot states.
    /// Same string-bitmap idea as PixelSprites, one shared gold ramp so they read as one set,
    /// tinted by the caller for meaning. A future licensed icon pack can replace any id here
    /// without touching a screen.
    /// </summary>
    public static class UiGlyphs
    {
        static readonly Dictionary<char, Color32> Ramp = new Dictionary<char, Color32>
        {
            ['.'] = new Color32(0, 0, 0, 0), ['d'] = new Color32(0x15, 0x12, 0x1C, 255),
            ['m'] = new Color32(0xC1, 0x91, 0x49, 255), ['h'] = new Color32(0xDC, 0xC4, 0x7C, 255),
            ['w'] = new Color32(0xF2, 0xE8, 0xC9, 255),
        };

        internal static readonly Dictionary<string, string[]> Bitmaps = new Dictionary<string, string[]>
        {
            ["state.check"] = new[]
            {
                "............",
                "..........dd",
                ".........dwd",
                "........dwhd",
                ".dd....dwhd.",
                "dwwd..dwhd..",
                "dhwwddwhd...",
                ".dhwwwhd....",
                "..dhwhd.....",
                "...dhd......",
                "....d.......",
                "............",
            },
            ["state.locked"] = new[]
            {
                "....dddd....",
                "...dmhhmd...",
                "..dmd..dmd..",
                "..dmd..dmd..",
                ".dddddddddd.",
                ".dhwwwwwwhd.",
                ".dhhhddhhhd.",
                ".dmhhddhhmd.",
                ".dmmhddhmmd.",
                ".dmmmmmmmmd.",
                ".dddddddddd.",
                "............",
            },
            ["skill.resilience_time"] = new[]   // Borrowed Hours: an hourglass
            {
                ".dddddddddd.",
                ".dmhhhhhhmd.",
                "..dwwwwwwd..",
                "...dhwwhd...",
                "....dhhd....",
                ".....dd.....",
                "....dmmd....",
                "...dm..md...",
                "..dm.hh.md..",
                ".dmhhwwhhmd.",
                ".dddddddddd.",
                "............",
            },
            // ...the remaining ids from the brief table, drawn in the same style.
        };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static IEnumerable<string> Ids => Bitmaps.Keys;

        public static Sprite Get(string id)
        {
            if (!Bitmaps.TryGetValue(id, out var rows) || rows == null) return null;
            if (cache.TryGetValue(id, out var s) && s != null) return s;
            var t = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    t.SetPixel(x, 11 - y, Ramp[rows[y][x]]);   // row 0 is the top of the drawing
            t.Apply(false);
            return cache[id] = Sprite.Create(t, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 25f);
        }
    }
}
```

Draw every brief motif:
- Outline in `d`, mid-tone body in `m`, light edge in `h`, and at most a few `w` sparkle pixels.
- Light comes from the top left, as in the three examples.
- Run the tests after each batch of five.

- [ ] **Step 4: Capture a contact sheet and review it with the owner.** Lay out every glyph at 48 px on a `frame.card` with its id under it (an `ev.sh` snippet, as in Task 2 Step 6). Run `bash .superpowers/cap.sh glyphs`. Silhouettes that read poorly at 48 px get redrawn here. This is the one visual sign-off in the plan where taste decides.

- [ ] **Step 5: Run and commit (when asked).** Run `bash .superpowers/rt.sh editor UiGlyphsTests`. Message: `Draw a pixel icon set for skills, upgrades, payloads and slot states`

---

### Task 10: The Broken Accord (ritual skill seal and inspector)

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/AccordLayout.cs`
- Modify: `Assets/Game/Scripts/UI/Kit/PixelGeometry.cs` (add `Ring`, `Disc` and `Diamond`)
- Rewrite: `Assets/Game/Scripts/UI/SkillTreePanel.cs`. It is hosted in `CharacterScreen.Body`. API: `Show`, `Hide` and `Respec` are kept. `Click(id)` now *selects*. New: `UnlockSelected()`, `Selected`, and `DefaultFocus`.
- Create: `Assets/Game/Scripts/UI/Screens/ConfirmDialog.cs` (also used by Task 12)
- Modify: `Assets/Game/Scripts/UI/Kit/ScreenStack.cs`. Add `PushOverlay`: a dialog must not hide the screen it is asking about. Task 4's `Push` deactivates the screen below.
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs`. Add `public ConfirmDialog Confirm { get; private set; }`, created in `Awake` after the menus, so it draws above them.
- Modify: `Assets/Game/Tests/PlayMode/GameRootPlayModeTests.cs` (the tree test calls `Click` then `UnlockSelected`)
- Modify: `Assets/Game/Tests/PlayMode/LayoutTests.cs` (rewrite `SkillTree_FourBranches_...` for the map)
- Test: `Assets/Game/Tests/EditMode/AccordLayoutTests.cs`, `Assets/Game/Tests/PlayMode/SkillTreePanelTests.cs`

**Interfaces:**
- Consumes: `SkillTree.Nodes`, `Prerequisite`, `Describe`, `IsOwned`, `WhyCannotBuy`, `TryBuy`, `Respec` and `LevelForTier`; `Cheats.UnlockAllNodes`; `UiGlyphs`; `ScreenStack`.
- Produces:
  - `enum NodeState { Owned, CheatActive, Available, Locked }`.
  - From `AccordLayout`:
    - `Vector2 NodePosition(SkillNode n)`
    - `float SealSize(int tier)`
    - `Vector2 Direction(SkillBranch b)`
    - `List<Vector2> PathDots(SkillNode n)`
    - `NodeState State(PlayerProfile p, SkillNode n)`
    - `const float CoreRadius`, and `float Radius(int tier)`
    - `Vector2 MapSize` = (1040, 800)
  - `ConfirmDialog.Create(Canvas)` and `void Ask(string title, string body, string confirm, Action onConfirm, ScreenStack stack)`. The default focus is **Cancel**. Esc means cancel.

The geometry is in map-local reference pixels, with the origin at the centre of the map. The four branches leave on the diagonals:

| Branch | Quadrant | Direction |
|---|---|---|
| Precision | upper-left | 135° |
| Mobility | upper-right | 45° |
| Resilience | lower-left | 225° |
| Blood Price | lower-right | 315° |

Tier radii are 168, 264 and 360. The core (the torn Ledger scrap) has radius 88. Seal sizes are 80 for tiers 1 and 2 (circles) and 104 for tier 3 (diamond).
- Extent check: the furthest node is 360 · cos 45° = 255 from the centre on each axis, plus 52 (half the outer seal), which is 307. That is inside the half-height of 400.
- Each path is a quadratic curve from the previous ring (or the core) to the node. Its control point is pushed 18% of the segment length sideways, clockwise, so each branch reads as a drawn working rather than a straight spoke.
- Dots sit every 16 reference px (8 art px), snapped to the 2-px grid, and are omitted inside any seal.
- The two concentric rings (radii 216 and 312, between the tiers) are **broken**: each is drawn as arcs that stop 24° short of each branch axis. A ring never touches a path, so it can never read as a connection between branches. The spec requires exactly this.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class AccordLayoutTests
    {
        [Test]
        public void BranchesSitInTheirQuadrants()
        {
            foreach (var n in SkillTree.Nodes)
            {
                var p = AccordLayout.NodePosition(n);
                bool left = n.Branch == SkillBranch.Precision || n.Branch == SkillBranch.Resilience;
                bool up = n.Branch == SkillBranch.Precision || n.Branch == SkillBranch.Mobility;
                Assert.AreEqual(left, p.x < 0, n.Id); Assert.AreEqual(up, p.y > 0, n.Id);
            }
        }

        [Test]
        public void OuterTiersAreFurtherOut()
        {
            foreach (var n in SkillTree.Nodes)
                Assert.AreEqual(AccordLayout.Radius(n.Tier), AccordLayout.NodePosition(n).magnitude, 0.5f, n.Id);
            Assert.Less(AccordLayout.Radius(1), AccordLayout.Radius(2));
            Assert.Less(AccordLayout.Radius(2), AccordLayout.Radius(3));
        }

        [Test]
        public void SealsNeverTouchEachOtherAndStayOnTheMap()
        {
            var nodes = SkillTree.Nodes;
            foreach (var a in nodes)
            {
                var pa = AccordLayout.NodePosition(a); float ra = AccordLayout.SealSize(a.Tier) / 2f;
                Assert.LessOrEqual(Mathf.Abs(pa.x) + ra, AccordLayout.MapSize.x / 2f, a.Id);
                Assert.LessOrEqual(Mathf.Abs(pa.y) + ra + 40f, AccordLayout.MapSize.y / 2f, a.Id + " (+40 for the name under it)");
                foreach (var b in nodes)
                {
                    if (a == b) continue;
                    float rb = AccordLayout.SealSize(b.Tier) / 2f;
                    Assert.Greater(Vector2.Distance(pa, AccordLayout.NodePosition(b)), ra + rb + 16f, $"{a.Id}/{b.Id}");
                }
            }
        }

        // Spec: "decorative rings never resemble connections between unrelated branches"; the same
        // goes for paths: no dot of one branch's path comes near another branch's seal.
        [Test]
        public void PathsOnlyTouchTheirOwnChain()
        {
            foreach (var n in SkillTree.Nodes)
                foreach (var dot in AccordLayout.PathDots(n))
                    foreach (var other in SkillTree.Nodes.Where(o => o.Branch != n.Branch))
                        Assert.Greater(Vector2.Distance(dot, AccordLayout.NodePosition(other)), AccordLayout.SealSize(other.Tier) / 2f + 24f, $"{n.Id} path near {other.Id}");
        }

        [Test]
        public void DotsAreOnThePixelGridAndOutsideSeals()
        {
            foreach (var n in SkillTree.Nodes)
            {
                var dots = AccordLayout.PathDots(n);
                Assert.Greater(dots.Count, 3, n.Id);
                foreach (var d in dots)
                {
                    Assert.AreEqual(0f, d.x % 2f, 1e-4, n.Id); Assert.AreEqual(0f, d.y % 2f, 1e-4, n.Id);
                    Assert.Greater(Vector2.Distance(d, AccordLayout.NodePosition(n)), AccordLayout.SealSize(n.Tier) / 2f, n.Id);
                }
            }
        }

        [Test]
        public void StatesFollowOwnershipCheatsAndGates()
        {
            var p = new PlayerProfile(); p.mastery.level = 2; p.mastery.points = 1;
            var wide = SkillTree.Find(SkillTree.PrecisionAngle);
            var deep = SkillTree.Find(SkillTree.PrecisionCapacity);
            Assert.AreEqual(NodeState.Available, AccordLayout.State(p, wide));
            Assert.AreEqual(NodeState.Locked, AccordLayout.State(p, deep), "needs Wide Grasp and mastery 4");
            p.ownedNodes.Add(wide.Id);
            Assert.AreEqual(NodeState.Owned, AccordLayout.State(p, wide));
            try
            {
                Cheats.SetUnlockAllNodes(true);
                Assert.AreEqual(NodeState.CheatActive, AccordLayout.State(p, deep), "distinct from earned ownership");
                Assert.AreEqual(NodeState.Owned, AccordLayout.State(p, wide));
            }
            finally { Cheats.SetUnlockAllNodes(false); }
        }
    }
}
```

`Cheats` lives in whichever namespace `GameRoot.Cheats.cs` uses. Copy the `using` from `LayoutTests.cs`, which already calls `Cheats.SetUnlockAllNodes`.

```csharp
// SkillTreePanelTests.cs (PlayMode): Review Focus 5
using System.Collections;
using BorrowedHex.Presentation;
using BorrowedHex.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    public class SkillTreePanelTests
    {
        GameObject camGo, rootGo; GameRoot root;

        [UnitySetUp] public IEnumerator SetUp()
        {
            camGo = new GameObject("Cam") { tag = "MainCamera" }; camGo.AddComponent<Camera>();
            GameRoot.StorageOverride = new MemoryProfileStorage();
            rootGo = new GameObject("GameRoot"); root = rootGo.AddComponent<GameRoot>();
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(rootGo); Object.Destroy(camGo); GameRoot.StorageOverride = null; yield return null; }

        [UnityTest]
        public IEnumerator SelectingNeverBuysAndADoubleUnlockSpendsOnePoint()
        {
            var m = root.Profile.Profile.mastery; m.level = 2; m.points = 2;
            root.Main.Press("mastery");
            yield return null;
            root.Tree.Click(SkillTree.PrecisionAngle);
            Assert.AreEqual(2, m.points, "selection is free");
            root.Tree.UnlockSelected();
            root.Tree.UnlockSelected();
            Assert.AreEqual(1, m.points);
            Assert.AreEqual(1, root.Profile.Profile.ownedNodes.Count);
        }

        [UnityTest]
        public IEnumerator WithNoPointsTheUnlockButtonSaysWhy()
        {
            var m = root.Profile.Profile.mastery; m.level = 2; m.points = 0;
            root.Main.Press("mastery");
            yield return null;
            root.Tree.Click(SkillTree.PrecisionAngle);
            yield return null;
            Assert.IsFalse(root.Tree.UnlockButton.interactable);
            StringAssert.Contains(SkillTree.WhyCannotBuy(root.Profile.Profile, SkillTree.PrecisionAngle), root.Tree.InspectorText);
        }
    }
}
```

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement `AccordLayout`**

```csharp
using System.Collections.Generic;
using BorrowedHex.Progression;
using UnityEngine;

namespace BorrowedHex.UI
{
    public enum NodeState { Owned, CheatActive, Available, Locked }

    /// <summary>
    /// Geometry and state of the Broken Accord (spec 3). Pure: no Unity objects, so every rule
    /// about where things sit and what they mean is tested without a canvas. Visual positions are
    /// deliberately separate from progression data (spec 4.3).
    /// </summary>
    public static class AccordLayout
    {
        public static readonly Vector2 MapSize = new Vector2(1040, 800);
        public const float CoreRadius = 88f;
        public const float Spacing = 16f;        // dot pitch: 8 art pixels
        const float Bend = 0.18f;                // sideways push of each path's control point

        public static float Radius(int tier) => tier == 1 ? 168f : tier == 2 ? 264f : 360f;
        public static float SealSize(int tier) => tier == 3 ? 104f : 80f;

        public static Vector2 Direction(SkillBranch b)
        {
            float deg = b switch { SkillBranch.Precision => 135f, SkillBranch.Mobility => 45f, SkillBranch.Resilience => 225f, _ => 315f };
            return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        }

        public static Vector2 NodePosition(SkillNode n) => Direction(n.Branch) * Radius(n.Tier);

        /// <summary>Dotted connection INTO <paramref name="n"/>: from its prerequisite's seal, or from the core.</summary>
        public static List<Vector2> PathDots(SkillNode n)
        {
            var pre = SkillTree.Prerequisite(n);
            var dir = Direction(n.Branch);
            Vector2 a = pre != null ? NodePosition(pre) : dir * CoreRadius;
            float skipA = pre != null ? SealSize(pre.Tier) / 2f + 6f : 6f;
            Vector2 b = NodePosition(n);
            float skipB = SealSize(n.Tier) / 2f + 6f;
            // Control point: midpoint pushed clockwise (right of travel), so curves swirl one way.
            var mid = (a + b) * 0.5f;
            var side = new Vector2(dir.y, -dir.x);
            var c = mid + side * Vector2.Distance(a, b) * Bend;

            var dots = new List<Vector2>();
            // Walk the curve finely and drop a dot each time the arc length passes Spacing.
            Vector2 prev = a; float run = 0f;
            for (int i = 1; i <= 200; i++)
            {
                float t = i / 200f;
                var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
                run += Vector2.Distance(prev, p); prev = p;
                if (run < Spacing) continue;
                run = 0f;
                if (Vector2.Distance(p, a) < skipA || Vector2.Distance(p, b) < skipB) continue;
                dots.Add(new Vector2(Mathf.Round(p.x / 2f) * 2f, Mathf.Round(p.y / 2f) * 2f));
            }
            return dots;
        }

        public static NodeState State(PlayerProfile p, SkillNode n)
        {
            if (p.ownedNodes.Contains(n.Id)) return NodeState.Owned;
            if (SkillTree.IsOwned(p, n.Id)) return NodeState.CheatActive;   // owned only through the cheat
            return SkillTree.WhyCannotBuy(p, n.Id) == null ? NodeState.Available : NodeState.Locked;
        }
    }
}
```

Assumption to verify before Step 4: `SkillTree.IsOwned` returns true for every node while `Cheats.UnlockAllNodes` is on. The summary of the domain says a cheat-only node is "owned but not in ownedNodes". Read `SkillTree.IsOwned` (line 111) to confirm. If it does not consult the cheat, test `Cheats.UnlockAllNodes` directly in `State` instead.

- [ ] **Step 4: Implement the `PixelGeometry` additions**

```csharp
        /// <summary>A 1-art-pixel ring of radius <paramref name="r"/> art px, with gaps around the given axes (degrees).</summary>
        public static Texture2D Ring(int r, float[] gapAxes, float gapHalfWidth, Color32 ink)
        {
            int size = r * 2 + 3; var t = New(size, size);
            var px = new Color32[size * size];
            // Midpoint circle: exact pixel ring, no anti-aliasing.
            int x = r, y = 0, err = 1 - r, c = size / 2;
            while (x >= y)
            {
                foreach (var (dx, dy) in new[] { (x, y), (y, x), (-y, x), (-x, y), (-x, -y), (-y, -x), (y, -x), (x, -y) })
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                    bool inGap = false;
                    foreach (var g in gapAxes) if (Mathf.Abs(Mathf.DeltaAngle(ang, g)) < gapHalfWidth) inGap = true;
                    if (!inGap) px[(c + dy) * size + (c + dx)] = ink;
                }
                y++;
                if (err < 0) err += 2 * y + 1; else { x--; err += 2 * (y - x) + 1; }
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        /// <summary>Filled seal: rim colour on the outer pixel, fill inside. Diameter in art px.</summary>
        public static Texture2D Disc(int d, Color32 rim, Color32 fill)
        {
            var t = New(d, d); var px = new Color32[d * d];
            float c = (d - 1) / 2f, r = d / 2f;
            for (int y = 0; y < d; y++) for (int x = 0; x < d; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (dist <= r - 0.5f) px[y * d + x] = dist > r - 2f ? rim : fill;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        public static Texture2D Diamond(int d, Color32 rim, Color32 fill)
        {
            var t = New(d, d); var px = new Color32[d * d];
            float c = (d - 1) / 2f;
            for (int y = 0; y < d; y++) for (int x = 0; x < d; x++)
            {
                float m = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                if (m <= c) px[y * d + x] = m > c - 1.5f ? rim : fill;
            }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }
```

- [ ] **Step 5: Build the panel**

The structure under `Body` (from `CharacterScreen`) is:

```
SkillTreePanel (RectTransform stretched over Body)
  Map        1040x800, left, centred vertically      <- holds Decor + Node_* children
    Decor      (rings, core scrap, filigree corners, every path dot)  raycastTarget=false
    Node_<id>  x12, Button, seal Image + glyph Image + Name text under the seal
  Inspector  480 wide, right, `frame.parchment`
    Name (Sub), StateLine (gem + word), Effect (Body), Requirement (Small), Prerequisite (Small),
    Unlock (Primary "Unlock · 1 point"), Note "Skills apply next run" (Small)
  Footer     "Reset skills" (Quiet), bottom-left of the map
```

How each node state looks. **State is never conveyed by colour alone:** every state has a word in the inspector and a distinct mark on the map.

| State | Seal | Glyph | Mark |
|---|---|---|---|
| Owned | `Disc` / `Diamond` with a Honey rim and a lit fill (`#3A2A14`) | full colour (white tint) | none; the path into it is drawn in Honey |
| CheatActive | as Owned, but with a Violet rim | white | a small "C" badge (Small font) at bottom-right |
| Available | Camel rim, dark fill | Camel tint 70% | a **pulsing gold outline** (static under Reduce flashes) |
| Locked | Muted rim, dark fill | Muted tint 40% | the `state.locked` glyph badge at bottom-right |
| Selected (any state) | plus a 2-art-pixel Ivory ring 6 px outside the seal | | |

The path dots into a node are Honey when the node is Owned or CheatActive, and Muted at 50% otherwise.

The unlock animation (spec): on a successful `UnlockSelected`, the path dots into the node light in order over 0.4 s, on unscaled time. Under Reduce flashes they switch on at once.

`Click(id)` sets `Selected`, refreshes the inspector, and moves the EventSystem focus to that node.

`UnlockSelected()`:

```csharp
        public void UnlockSelected()
        {
            if (selected == null) return;
            // TryBuy is the single authority: a second press finds the node owned and spends nothing.
            if (SkillTree.TryBuy(profile, selected.Id, out _)) { BeginLightUp(selected); changed?.Invoke(); }
            Refresh();
        }
```

The inspector text, built in `InspectorText`, which is exposed for tests:

```csharp
        public string InspectorText
        {
            get
            {
                if (selected == null) return "";
                var st = AccordLayout.State(profile, selected);
                string state = st switch { NodeState.Owned => "Active", NodeState.CheatActive => "Active through cheats",
                                           NodeState.Available => "Available", _ => "Locked" };
                var pre = SkillTree.Prerequisite(selected);
                string why = st == NodeState.Locked || st == NodeState.Available && profile.mastery.points <= 0
                    ? SkillTree.WhyCannotBuy(profile, selected.Id) : null;
                return $"{selected.Name}\n{state}\n\n{SkillTree.Describe(selected, tuning)}\n\n" +
                       $"Requires mastery {SkillTree.LevelForTier(selected.Tier)}" +
                       (pre != null ? $"\nAfter {pre.Name}" : "") + (why != null ? $"\n{why}" : "");
            }
        }
```

The unlock button is `interactable` only when the state is `Available` and `WhyCannotBuy == null`. Its caption is always "Unlock · 1 point". The disabled reason is shown in the inspector.

"Reset skills" calls `ConfirmDialog.Ask("Reset skills?", $"All {n} skill points come back to you. Skills change from your next run.", "Reset", () => { Respec(); }, screens)`. Here `n = profile.ownedNodes.Count`, which is the exact refund, because each node costs one point (spec: "state the exact refund"). The button is hidden when `n == 0`.

`ScreenStack.PushOverlay(root, focus, onEscape)` works like `Push`, except that the screen below stays **active** and gets `CanvasGroup.interactable = false` and `blocksRaycasts = false`. `Pop` restores both flags on the entry below. Add the test to `ScreenStackTests`:

```csharp
        [Test]
        public void AnOverlayKeepsTheScreenBelowVisibleButInert()
        {
            stack.Push(a, () => aButton, null);
            stack.PushOverlay(b, () => bButton, null);
            Assert.IsTrue(a.activeSelf, "the dialog is about this screen, so it stays in view");
            Assert.IsFalse(a.GetComponent<CanvasGroup>().interactable);
            stack.Pop();
            Assert.IsTrue(a.GetComponent<CanvasGroup>().interactable);
            Assert.AreEqual(aButton, EventSystem.current.currentSelectedGameObject);
        }
```

Use the fixture names from `ScreenStackTests.SetUp` (Task 4); rename `a`, `b`, `aButton` and `bButton` to match it. Store an `Overlay` flag on `Entry` so `Pop` knows which flags to restore.

`ConfirmDialog`:
- A Scrim root with a `CanvasGroup`, pushed with `PushOverlay`.
- An ornate frame 720×360, with the title (Heading), the body (Body), and two buttons: **Cancel** (Secondary, left) and the confirm label (Primary, right).
- `Ask` pushes the dialog on the stack with default focus on **Cancel** and `onEscape` = pop.
- Confirming pops, then runs the action.

The tooltip on each node shows its name and state. That makes the name readable when the map is focused by keyboard and the label is small.

Keyboard: the nodes use `Navigation.Mode.Explicit`.
- Up and down move between tiers in a branch.
- Left and right move to the same tier in the mirrored branch: Precision ↔ Mobility, Resilience ↔ Blood Price.
- Right from a right-side branch goes to the inspector's Unlock button.

Build this navigation once in `Build()` from `SkillTree.Nodes`.

- [ ] **Step 6: Update the two existing tests**

In `GameRootPlayModeTests.TheTree_FromTheMainMenu_Buys_AndTheNextRunUsesIt`, change `root.Tree.Click(SkillTree.PrecisionAngle);   // buy...` to:

```csharp
            root.Tree.Click(SkillTree.PrecisionAngle);   // select (the spec splits selection from purchase)
            root.Tree.UnlockSelected();                  // buy: owning it makes it active (D101)
```

In `LayoutTests.SkillTree_FourBranches_NothingOverlapsAndAllTextFits`, replace the body after `Show`:

```csharp
                    var panel = (RectTransform)root.Tree.transform.Find("Panel");   // Panel is kept as the name
                    var map = (RectTransform)panel.Find("Map");
                    var nodes = new List<(string, Rect)>();
                    foreach (RectTransform child in map)
                        if (child.name.StartsWith("Node_") && child.gameObject.activeInHierarchy) nodes.Add((child.name, ScreenBox(child)));
                    Assert.AreEqual(12, nodes.Count, "all twelve nodes are on the map");
                    AssertNoOverlaps(nodes, where);
                    var blocks = new List<(string, Rect)> { ("Map", ScreenBox(map)), ("Inspector", ScreenBox((RectTransform)panel.Find("Inspector"))) };
                    AssertNoOverlaps(blocks, where);
                    AssertOnScreen(nodes.Concat(blocks), where);
                    AssertTextFits(panel, where);
```

Name the panel root's child "Panel" (the skill view's container) so `root.Tree.transform.Find("Panel")` still resolves. `Tree.Show(profile, config.progression, null, null)` must still work with null callbacks.

- [ ] **Step 7: Run.** Run:
- `bash .superpowers/rt.sh editor AccordLayoutTests`
- `bash .superpowers/rtp.sh "SkillTreePanelTests|LayoutTests|GameRootPlayModeTests"`
- `bash .superpowers/strict.sh both t10`

Then capture the map in three states: fresh, mid-progress, and cheat. Use `bash .superpowers/cap.sh accord-fresh`, and so on.

- [ ] **Step 8: Commit (when asked).** Message: `Replace the skill columns with the Broken Accord seal and an inspector`

---

### Task 11: Records and Achievements tabs

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/RecordRows.cs`
- Rewrite: `Assets/Game/Scripts/UI/RecordsPanel.cs`. The static `AchievementsBody` and `RecordsBody` stay as thin wrappers, because nothing else may call them. Grep first, and delete them if unused.
- Test: `Assets/Game/Tests/EditMode/RecordRowsTests.cs`

**Interfaces:**
- Produces:
  - `RecordRows.Row(RunRecord r)` returns `RecordRow { string Mode, Style, Value, Duration, Details; }`.
  - `RecordRows.Summary(PlayerStats st)` returns `string`, the one compact totals line.
  - `enum AchievementFilter { All, Earned, Locked }`.
  - `RecordRows.Achievements(PlayerProfile p, AchievementFilter f)` returns `List<(AchievementDef def, bool earned)>`.
  - `RecordRows.Completion(PlayerProfile p)` returns `string`, such as "3 / 9".
  - `RecordRows.FormatTime(float)`, moved here from the panel.

Layout inside a full-screen ornate frame (1600×960), with the tabs **Records** and **Achievements**:
- **Records:**
  - A header row (Mode · Style · Best · Time), in Small Muted.
  - Then a scroll view of rows, each a `frame.card` 64 tall: mode, style, the value in Honey, and the duration.
  - Pressing a row expands it with a parchment block showing `Details`: the reason, kills, mastery, skills by *name*, seed and build, in Small.
  - The totals summary sits on a fixed strip above the footer.
  - With no records: "No finished runs yet. Your first run will be written here."
- **Achievements:**
  - The filter tabs (All / Earned / Locked) and the completion "3 / 9" are shown once, in the header.
  - Then a scroll view of entries: an emblem (gem.1 when earned, gem.4 dim when locked), the name in Sub, and the condition in Body, plus the `state.check` glyph and the word "Earned" when earned.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class RecordRowsTests
    {
        static RunRecord Rec() => new RunRecord { mode = "Short", styleId = "collector", kind = Records.BestScore, score = 4210,
            duration = 312f, kills = 57, reason = "Victory", seed = 99, buildVersion = "0.9", masteryLevel = 3,
            passives = { SkillTree.PrecisionAngle, "unknown_future_node" } };

        [Test]
        public void TheRowShowsModeStyleValueAndTimeOnly()
        {
            var r = RecordRows.Row(Rec());
            Assert.AreEqual("Short", r.Mode); Assert.AreEqual("Collector", r.Style);
            StringAssert.Contains("4210", r.Value); Assert.AreEqual("5:12", r.Duration);
            foreach (var s in new[] { r.Mode, r.Style, r.Value, r.Duration })
            { StringAssert.DoesNotContain("seed", s); StringAssert.DoesNotContain("0.9", s); }
        }

        [Test]
        public void DetailsUseSkillNamesAndKeepUnknownIds()
        {
            var d = RecordRows.Row(Rec()).Details;
            StringAssert.Contains("Wide Grasp", d);
            StringAssert.DoesNotContain(SkillTree.PrecisionAngle, d);
            StringAssert.Contains("unknown_future_node", d, "an id with no name is still shown, not dropped");
            StringAssert.Contains("99", d); StringAssert.Contains("0.9", d);
        }

        [Test]
        public void SurvivalRecordsShowTheTime()
        {
            var rec = Rec(); rec.kind = Records.LongestRun;
            StringAssert.Contains("5:12", RecordRows.Row(rec).Value);
        }

        [Test]
        public void FiltersPartitionTheAchievements()
        {
            var p = new PlayerProfile();
            p.achievements.Add(new AchievementEntry { id = Achievements.All[0].Id });
            int all = RecordRows.Achievements(p, AchievementFilter.All).Count;
            int got = RecordRows.Achievements(p, AchievementFilter.Earned).Count;
            int left = RecordRows.Achievements(p, AchievementFilter.Locked).Count;
            Assert.AreEqual(Achievements.All.Count, all);
            Assert.AreEqual(1, got); Assert.AreEqual(all - 1, left);
            Assert.AreEqual($"1 / {all}", RecordRows.Completion(p));
        }

        [Test] public void HoursFormatWithAnHourField() => Assert.AreEqual("1:02:03", RecordRows.FormatTime(3723f));
    }
}
```

The `AchievementEntry` field name must match `PlayerProfile.cs` (read it; the summary did not record it). `Achievements.Has` is the authority, so the filter must call it rather than reading the list.

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement**

```csharp
using System.Collections.Generic;
using System.Linq;
using BorrowedHex.Progression;
using UnityEngine;

namespace BorrowedHex.UI
{
    public struct RecordRow { public string Mode, Style, Value, Duration, Details; }
    public enum AchievementFilter { All, Earned, Locked }

    /// <summary>What the Records and Achievements tabs show (spec 2): aligned essentials up front,
    /// metadata in Details, readable skill names instead of internal ids.</summary>
    public static class RecordRows
    {
        public static RecordRow Row(RunRecord r)
        {
            string skills = r.passives == null || r.passives.Count == 0 ? "none"
                : string.Join(", ", r.passives.Select(id => SkillTree.Find(id)?.Name ?? id));
            return new RecordRow
            {
                Mode = r.mode,
                Style = CaptureStyles.Resolve(r.styleId).Name,
                Value = r.kind == Records.LongestRun ? $"Survived {FormatTime(r.duration)}" : $"Score {r.score}",
                Duration = FormatTime(r.duration),
                Details = $"{r.reason} · {r.kills} kills · mastery {r.masteryLevel}\nSkills: {skills}\nSeed {r.seed} · build {r.buildVersion}",
            };
        }

        public static string Summary(PlayerStats st) =>
            $"{st.runs} runs · {st.victories} wins · {st.kills} kills · {st.bossesDefeated} bosses · " +
            $"{st.perfectShots} perfect · {st.backfires} backfires · {FormatTime(st.secondsPlayed)} played";

        public static List<(AchievementDef def, bool earned)> Achievements(PlayerProfile p, AchievementFilter f) =>
            Progression.Achievements.All.Select(a => (a, Progression.Achievements.Has(p, a.Id)))
                .Where(x => f == AchievementFilter.All || (f == AchievementFilter.Earned) == x.Item2).ToList();

        public static string Completion(PlayerProfile p) =>
            $"{Progression.Achievements.All.Count(a => Progression.Achievements.Has(p, a.Id))} / {Progression.Achievements.All.Count}";

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }
    }
}
```

`PlayerStats` here is the type of `PlayerProfile.stats`. Use its real type name, read from `PlayerProfile.cs`, because the run-time `PlayerStats` in Combat is a different class. This uses the existing `Style()` behaviour: `CaptureStyles.Resolve` maps a null or empty id to Snatcher, so verify that it does.

The panel builds rows from these values with `UiKit` and keeps them under `UiKit.ScrollView`. The PlayMode test helper `RecordsPanelText()` concatenates the texts in the panel, so "Short" in the Mode column satisfies it unchanged.

- [ ] **Step 4: Run.** Run `bash .superpowers/rt.sh editor RecordRowsTests`, then `bash .superpowers/rtp.sh GameRootPlayModeTests` (the records test must pass), then the gate.

- [ ] **Step 5: Commit (when asked).** Message: `Split Records and Achievements into tabs with aligned rows and details`

---

### Task 12: Pause with confirmation, and the Practice tools drawer

**Files:**
- Rewrite: `Assets/Game/Scripts/UI/PauseMenu.cs`. API kept: `Create(canvas, resume, restart, quit)`, `AddButton(Func<string>, Action)`, `Show`, `IsOpen`. New: `DefaultFocus`.
- Create: `Assets/Game/Scripts/UI/Screens/PracticeDrawer.cs`
- Modify: `Assets/Game/Scripts/UI/GameplayHud.cs`. `AddDevButton` and `ResetButton` route into the drawer, and `SetResetVisible` becomes `SetPracticeVisible`; keep the old name as an alias.
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs` (counted-run confirmation)
- Test: `Assets/Game/Tests/PlayMode/PauseConfirmTests.cs`, `Assets/Game/Tests/EditMode/PracticeDrawerTests.cs`

**Interfaces:**
- Consumes: `ConfirmDialog` (Task 10) and `ScreenStack`.
- Produces:
  - `GameRoot.IsCountedRunActive`, a `bool`: true when `kind` is Short or Endless, the setup is not Debug, and the state is not Results.
  - `PracticeDrawer.Group(string label)` returns `string`, one of `"Spawn"`, `"Arena"` or `"Run tools"`.
  - `PracticeDrawer.Add(string label, Action a)` returns `Button`; `bool Open`; `void Toggle()`.

Pause layout: an ornate frame 640×560, centred, over the `Scrim`.
- **Resume** (Primary), with "Esc / P" in Small Muted to its right, inside the frame.
- Then Settings, Restart run and Main menu (Secondary). On WebGL, "Main menu" reads "Quit to menu", as today.
- **Quit** (Quiet), on the frame's bottom edge, only when quit is not null.
- The HUD and the Practice drawer are hidden while paused: `Hud.SetPauseCovered(true)` hides the root group except the life bar. The life bar stays visible as context, and it sits above the frame, so the layout test still holds. Set the frame position so the life bar box does not overlap; Task 13's HUD test checks this.

Restart and Main menu during a counted run go through:

```csharp
        void ConfirmLeave(string title, System.Action go)
        {
            if (!IsCountedRunActive) { go(); return; }
            Confirm.Ask(title, "Unfinished progress from this run is lost.", title.StartsWith("Restart") ? "Restart" : "Leave", go, Screens);
        }
```

The calls are `ConfirmLeave("Restart run?", Restart)` and `ConfirmLeave("Abandon this run?", ShowMainMenu)`.

The **P** key must behave like Esc while a dialog or sub-screen is up. Today `pausePressed` toggles the menu directly, so P on the confirm would close the pause menu under it and resume the run. In `GameRoot.Update`, route it as:

```csharp
            if (pausePressed)
            {
                // Above the pause menu, P backs out one level like Esc; only on pause itself does it resume.
                if (Screens.Count > 1 || (Screens.Count == 1 && Screens.Top != Menu.gameObject)) Screens.Escape();
                else SetMenuOpen(!Menu.IsOpen);
            }
```

`HandleRestartKey` (R) must do nothing while `Confirm` is open: add `if (Screens.Top == Confirm.gameObject) return false;` at its top. Add `PIsEscOnTheConfirmDialog` to `PauseConfirmTests`, with the same shape as the Esc test and the input simulated through the reader's `ConsumePause` test hook. Look in `GameRootPlayModeTests` for how P is pressed there.

Practice groups, decided by label so `GameRoot`'s existing `AddDevButton` calls need no change:
- `"+ "` prefix: **Spawn**
- `"Clear arena"`, or an `"Auto-spawn"` prefix: **Arena**
- Everything else (Upgrade cycling, Endless (debug), Skip wave, and Reset): **Run tools**

The drawer is a Quiet toggle button "Practice tools ▾" at top-right, under the pause button. It is closed at the start of every run (spec). Opening it shows a `frame.card` column with group headings (Sub) and the buttons (Secondary, 288×48). The layout test then verifies the HUD with it closed. Add one more test case with it open, against the HUD elements it may cover (none: it drops below the pause button, on the right edge where the old wall was, and the upgrade chips moved to the top bar in Task 13).

- [ ] **Step 1: Write the failing tests**

```csharp
// PracticeDrawerTests.cs
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class PracticeDrawerTests
    {
        [TestCase("+ Formation", "Spawn")] [TestCase("+ Collector", "Spawn")]
        [TestCase("Clear arena", "Arena")] [TestCase("Auto-spawn: off", "Arena")]
        [TestCase("Upgrade: none", "Run tools")] [TestCase("Endless (debug)", "Run tools")]
        [TestCase("Skip wave", "Run tools")] [TestCase("Reset", "Run tools")]
        public void ToolsLandInTheirGroup(string label, string group) => Assert.AreEqual(group, PracticeDrawer.Group(label));
    }
}
```

Read `GameRoot.AutoSpawnLabel` for the exact auto-spawn caption, and match its prefix.

```csharp
// PauseConfirmTests.cs (PlayMode): Review Focus 3
using System.Collections;
using BorrowedHex.Core;
using BorrowedHex.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    public class PauseConfirmTests
    {
        GameObject camGo, rootGo; GameRoot root;
        [UnitySetUp] public IEnumerator SetUp()
        {
            camGo = new GameObject("Cam") { tag = "MainCamera" }; camGo.AddComponent<Camera>();
            GameRoot.StorageOverride = new MemoryProfileStorage();
            rootGo = new GameObject("GameRoot"); root = rootGo.AddComponent<GameRoot>();
            yield return null;
            root.SetFocus(true); root.PlayShort(); root.SetMenuOpen(false);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(rootGo); Object.Destroy(camGo); GameRoot.StorageOverride = null; yield return null; }

        Button Find(string name) => GameObject.Find(name).GetComponent<Button>();

        [UnityTest]
        public IEnumerator RestartInACountedRunAsksFirstAndDefaultsToCancel()
        {
            root.SetMenuOpen(true);
            yield return null;
            int seed = root.Sim.Setup.Seed;
            Find("RestartRun").onClick.Invoke();
            yield return null;
            Assert.AreEqual("Cancel", UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name);
            Assert.AreEqual(seed, root.Sim.Setup.Seed, "nothing restarted yet");
        }

        [UnityTest]
        public IEnumerator EscOnConfirmReturnsToPauseNotToTheRun()
        {
            root.SetMenuOpen(true);
            yield return null;
            Find("RestartRun").onClick.Invoke();
            yield return null;
            Assert.IsTrue(root.Screens.Escape());
            yield return null;
            Assert.IsTrue(root.Menu.IsOpen, "back on the pause menu");
            Assert.IsTrue(root.Sim.Clock.HasPauseReason(PauseReason.Menu), "the run never resumed");
        }

        [UnityTest]
        public IEnumerator PracticeRunsRestartWithoutAsking()
        {
            root.PlaySandbox(); root.SetMenuOpen(false);
            yield return null;
            Assert.IsFalse(root.IsCountedRunActive);
        }
    }
}
```

Check `root.Sim.Setup.Seed` against the real `RunSetup` field name. If the run id is the better marker, use that, but restart must be observable. The pause buttons are named `RestartRun`, `MainMenuButton`, `Resume`, `Settings` and `Quit`; set these names in the rewrite.

- [ ] **Step 2: Run, expect a failure.**

- [ ] **Step 3: Implement**

Write `PauseMenu` with the kit, using the names above. In `PracticeDrawer`:

```csharp
        public static string Group(string label) =>
            label.StartsWith("+ ") ? "Spawn"
            : label == "Clear arena" || label.StartsWith("Auto-spawn") ? "Arena"
            : "Run tools";
```

`Add` puts the button under its group's column, creating the group heading on first use. `GameplayHud.AddDevButton(label, a)` becomes `return drawer.Add(label, a);`. `ResetButton` is created through `drawer.Add("Reset", reset)`. The tests' `Hud.ResetButton.gameObject.activeSelf` stays false in the tutorial, because `SetPracticeVisible(false)` hides the drawer, toggle included, and `SetActive(false)`s the Reset button itself.

- [ ] **Step 4: Run.** Run `bash .superpowers/rt.sh editor PracticeDrawerTests`, `bash .superpowers/rtp.sh "PauseConfirmTests|GameRootPlayModeTests|LayoutTests"`, then the gate.

- [ ] **Step 5: Commit (when asked).** Message: `Confirm before abandoning a counted run and fold the dev buttons into a drawer`

---

## Phase C: Run screens

### Task 13: Combat HUD (dominant Life, short objective, upgrade chips)

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/HudText.cs`
- Rewrite: `Assets/Game/Scripts/UI/GameplayHud.cs`. Kept: `Create`, `Bind`, `PauseButton`, `ResetButton`, `Packets`, `AddDevButton` (into the drawer, Task 12) and `SetResetVisible` (as an alias). New: `SetCovered(bool)`.
- Modify: `Assets/Game/Scripts/UI/PauseMenu.cs` (the held-upgrades inspection list)
- Modify: `Assets/Game/Tests/PlayMode/GameRootPlayModeTests.cs` (the endless objective assertion)
- Modify: `Assets/Game/Tests/PlayMode/LayoutTests.cs` (only if `HudBoxes` needs the new root names; it walks top-level Graphics, so it should not)
- Test: `Assets/Game/Tests/EditMode/HudTextTests.cs`

**Interfaces:**
- Consumes: `UiGlyphs` (`upgrade.<UpgradeId>`, `state.locked`), `UiTooltip`, `UiSkin` (`bar.tray`, `fill.red`, `square.dark`) and `UiFonts`.
- Produces:
  - `enum ObjectiveKind { Tutorial, Sandbox, Encounter, ShortBoss, EndlessWave, EndlessBoss }`.
  - `struct ObjectiveInfo { ObjectiveKind Kind; int Encounter, EncounterCount, EnemiesLeft, Wave, WavesPerCycle, Cycle; float WaveSecondsLeft; string BossName; }`.
  - `HudText.Objective(ObjectiveInfo)` returns `string`.
  - `HudText.Score(int score, float multiplier)` returns `string`.
  - `HudText.Chain(int length, float nextBonus)` returns `string`, empty below 2.
  - `GameplayHud.SetCovered(bool)` hides everything except the Life block (used by Pause, Tutorial completion and Results).

The layout at 1920×1080 reference, all anchored top-relative:

| Element | Anchor and position | Size | Notes |
|---|---|---|---|
| Life heart | top-centre, (−316, −24) | 64×64 | existing `PixelSprites.Heart()`; beats below 1/6 except under Reduce flashes (unchanged rule) |
| Life bar | top-centre, (24, −40) | 600×32 | `bar.tray` sliced, inner fill `fill.red` tinted `UiPalette.Blood`, not the old green-to-red HSV ramp |
| Objective | top-centre, (0, −88) | 900×32 | Small font, Muted; empty in the tutorial |
| Boss bar | top-centre, (0, −128) | 600×20 | `bar.trayDark` with a `fill.red` fill; shown only while a boss lives |
| Score | top-left, (40, −32) | 480×48 | Number font, Honey while a multiplier is above 1, otherwise Ivory |
| Dash | top-left, (40, −92) | 220×18 | `bar.trayDark` with a `fill.blue` fill, plus a "Dash" label (Small) |
| Upgrade chips | top-left, (40, −128) | 4 × (56×56), gap 8 | `square.dark` frame, glyph 48, rank digit (Small, Honey) bottom-right; tooltip shows name, rank and `UpgradeInfo.Describe` |
| Chain | top-centre, (380, −40) | 220×32 | Honey, fading with the window as today; shown only while active |
| Pause | top-right, (−40, −40) | 64×64 | Icon tier, sprite `btn.options`'s sibling frames are not a pause icon, so it draws "II" in alagard |
| Practice toggle | top-right, (−40, −112) | 288×48 | Task 12 |

Life loses its traffic-light hue on purpose. Red for a full bar reads as "blood is life", which is the game's premise. Urgency now comes from the heart beat and the damage flash, both unchanged, rather than from colour.

The upgrade chips live in the `Combat` group, so the existing rule ("combat-only readouts hide during the choice and the results") hides them during the upgrade choice. That is what spec "hide the external HUD" asks for. A locked set (no new cards) shows the `state.locked` glyph as a fifth, smaller badge after the chips, with tooltip "No new upgrades: rank-ups only".

Pause inspection (spec: names and effects through "pause inspection"): `PauseMenu.SetHeld(IReadOnlyList<UpgradeOffer> held, UpgradeTuning t)` fills a "Held upgrades" block under the buttons, one row each: glyph, then "Echo Volley 2", then the effect (Small). It is hidden when nothing is held. GameRoot calls it from `SetMenuOpen(true)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class HudTextTests
    {
        static ObjectiveInfo Info(ObjectiveKind k) => new ObjectiveInfo
        { Kind = k, Encounter = 1, EncounterCount = 3, EnemiesLeft = 6, Wave = 1, WavesPerCycle = 6, Cycle = 1, WaveSecondsLeft = 44.2f, BossName = "The Collector" };

        [Test] public void EncounterLineIsShort() => Assert.AreEqual("Encounter 2/3  ·  6 remaining", HudText.Objective(Info(ObjectiveKind.Encounter)));
        [Test] public void OneRemainingIsSingular() { var i = Info(ObjectiveKind.Encounter); i.EnemiesLeft = 1; StringAssert.EndsWith("1 remaining", HudText.Objective(i)); }
        [Test] public void EndlessShowsWaveCycleAndCountdown() => Assert.AreEqual("Wave 1/6  ·  Cycle 1  ·  0:45", HudText.Objective(Info(ObjectiveKind.EndlessWave)));
        [Test] public void BossLinesNameTheBossOnce() { StringAssert.AreEqualIgnoringCase("Defeat The Collector", HudText.Objective(Info(ObjectiveKind.ShortBoss))); }
        [Test] public void TheTutorialLineIsEmptyBecauseThePromptOwnsTheLessonNumber() => Assert.AreEqual("", HudText.Objective(Info(ObjectiveKind.Tutorial)));
        [Test] public void SandboxSaysPractice() => Assert.AreEqual("Practice", HudText.Objective(Info(ObjectiveKind.Sandbox)));
        [Test] public void ScoreShowsTheMultiplierOnlyAboveOne() { Assert.AreEqual("4210", HudText.Score(4210, 1f)); Assert.AreEqual("4210  x1.25", HudText.Score(4210, 1.25f)); }
        [Test] public void AChainOfOneIsNotAChain() { Assert.AreEqual("", HudText.Chain(1, 2f)); Assert.AreEqual("Chain x3  +2 next", HudText.Chain(3, 2f)); }
    }
}
```

The countdown rounds up, as `EndlessObjective` does today (`CeilToInt`): 44.2 s shows "0:45".

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement `HudText`**

```csharp
using UnityEngine;

namespace BorrowedHex.UI
{
    public enum ObjectiveKind { Tutorial, Sandbox, Encounter, ShortBoss, EndlessWave, EndlessBoss }

    public struct ObjectiveInfo
    {
        public ObjectiveKind Kind;
        public int Encounter, EncounterCount, EnemiesLeft, Wave, WavesPerCycle, Cycle;
        public float WaveSecondsLeft;
        public string BossName;
    }

    /// <summary>
    /// The HUD's words (spec 2, Combat HUD): one short objective line, sentence case, no shouting.
    /// Pure, so the copy is tested without a sim; GameplayHud only fills an ObjectiveInfo.
    /// </summary>
    public static class HudText
    {
        const string Sep = "  ·  ";   // the same separator the menus use (Task 6 checks the glyph)

        public static string Objective(ObjectiveInfo i)
        {
            switch (i.Kind)
            {
                case ObjectiveKind.Tutorial: return "";   // the prompt panel shows "Lesson n/6" once
                case ObjectiveKind.Sandbox: return "Practice";
                case ObjectiveKind.Encounter: return $"Encounter {i.Encounter + 1}/{i.EncounterCount}{Sep}{i.EnemiesLeft} remaining";
                case ObjectiveKind.ShortBoss: return $"Defeat {i.BossName}";
                case ObjectiveKind.EndlessBoss: return $"Defeat {i.BossName}{Sep}Cycle {i.Cycle}";
                default:
                    int secs = Mathf.CeilToInt(i.WaveSecondsLeft);
                    return $"Wave {i.Wave}/{i.WavesPerCycle}{Sep}Cycle {i.Cycle}{Sep}{secs / 60}:{secs % 60:00}";
            }
        }

        public static string Score(int score, float multiplier) => multiplier > 1f ? $"{score}  x{multiplier:0.00}" : score.ToString();

        public static string Chain(int length, float nextBonus) => length < 2 ? "" : $"Chain x{length}  +{nextBonus:0.#} next";
    }
}
```

The boss name is the configured `collector.displayName`, used as written: no `ToUpperInvariant`. The pixel faces carry the emphasis now, and capitals in alagard read as shouting.

- [ ] **Step 4: Rebuild `GameplayHud`**

Keep the polling structure: `LateUpdate` reads the sim each frame. Replace the objective composition with:

```csharp
            var info = new ObjectiveInfo
            {
                Kind = sim.Tutorial != null ? ObjectiveKind.Tutorial
                    : sim.IsEndlessRun ? (sim.State == RunState.BossIntro || sim.State == RunState.BossCombat ? ObjectiveKind.EndlessBoss : ObjectiveKind.EndlessWave)
                    : !sim.IsShortRun ? ObjectiveKind.Sandbox
                    : sim.Encounter < sim.Config.shortMode.encounterCount ? ObjectiveKind.Encounter : ObjectiveKind.ShortBoss,
                Encounter = sim.Encounter, EncounterCount = sim.Config.shortMode.encounterCount,
                // Only asked for when it is shown: EnemiesLeftInEncounter walks the enemy list.
                EnemiesLeft = sim.IsShortRun && sim.Tutorial == null ? sim.EnemiesLeftInEncounter() : 0,
                Wave = sim.Wave, WavesPerCycle = sim.Config.endless.wavesPerCycle, Cycle = sim.Cycle,
                WaveSecondsLeft = sim.WaveSecondsLeft, BossName = sim.Config.collector.displayName,
            };
            objectiveLabel.text = HudText.Objective(info);
```

`EnemiesLeftInEncounter`, `WaveSecondsLeft`, `Wave` and `Cycle` are the members the current HUD already calls. Read `GameplayHud.cs:195-202` before deleting it and copy any guard it has.

During a tutorial, the score and the dash label are hidden, along with the packet hint (`Packets.SetHintVisible(false)`, Task 14). That follows spec "Hide irrelevant score and general hints during lessons". The dash bar stays, because the dash lesson needs it.

Chips: build four in `Build()` and fill them in `LateUpdate` from `sim.HeldUpgrades`, setting the tooltip text only when the held list changes. Comparing count plus ids is enough, so the tooltip is not rebuilt per frame.

`SetCovered(bool on)`: `combat.gameObject` and the score/chip group go inactive while covered; the Life block stays. It is OR-ed with the existing state rule, so covering never shows something the state hides.

- [ ] **Step 5: Update the endless test.** In `GameRootPlayModeTests`, change `StringAssert.StartsWith("WAVE 1/6", ...)` to `StringAssert.StartsWith("Wave 1/6", ...)`. Leave the rest of that test as it is.

- [ ] **Step 6: Run.** Run:
- `bash .superpowers/rt.sh editor HudTextTests`
- `bash .superpowers/rtp.sh "LayoutTests|GameRootPlayModeTests"`; the HUD no-overlap test runs over the new layout.
- `bash .superpowers/strict.sh both t13`

Then capture a short-run fight with three upgrades held and a chain active (`cap.sh hud-fight`).

- [ ] **Step 7: Commit (when asked).** Message: `Rebuild the combat HUD around a dominant Life bar and upgrade chips`

---

### Task 14: Hex slots with an explicit overcharge border

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/SlotCue.cs`
- Rewrite: `Assets/Game/Scripts/UI/PacketIndicator.cs`. Kept: `Create` and `Bind`. New: `SetHintVisible(bool)`. Removed: the `OverchargeGold` field (spec: "remove the duplicate local overcharge color").
- Test: `Assets/Game/Tests/EditMode/SlotCueTests.cs`

**Interfaces:**
- Consumes: `FeedbackColors.Overcharge`, `CapturedPacket.IsOvercharged(power)`, `sim.IsPrimed(pk)`, `store.SelectedSlot`, `store.InSlot(i)`, `store.IsLocked(i)`, `sim.Clock.Now`, `UiGlyphs` (`state.*` and `payload.*`).
- Produces:
  - `struct SlotInput { bool HasPacket, Locked, Selected, Primed, Overcharged, Fused, HandFull, ReduceFlashes; double Now, OverchargeSince; }`.
  - `struct SlotCue { bool Marker; Color MarkerColor; bool Border; Color BorderColor; bool CountdownGold; string[] States; string Label; }`.
  - `SlotCue.Evaluate(in SlotInput)` returns `SlotCue`.
  - `SlotCue.Track(ref PulseClock clock, object packet, bool pulsing, double now)` returns `double` (the time the current pulse started).
  - `struct PulseClock { object Packet; double Since; bool Was; }`.
  - Constants: `SlotCue.PulseHz = 2f`, `SlotCue.PulseMin = 0.45f`.

The rules, copied from spec §2's overcharge table and turned into one pure function. In this game an *unselected* packet is a frozen packet (`PacketIndicator` today labels every unselected packet FROZEN), so "frozen" means `HasPacket && !Selected`.

| Input | Marker | Border | States (glyph + word) |
|---|---|---|---|
| selected, empty or ordinary | Ivory | off | none, or "Decaying" |
| selected, overcharged | Ivory | `FeedbackColors.Overcharge`, alpha `0.725 + 0.275·cos(2π·2·(now − since))`: 1 at entry, 0.45 at its lowest | "Overcharge" |
| unselected (frozen), overcharged | off | the same colour, alpha 1 (steady) | "Frozen", "Overcharge" |
| Reduce flashes, any overcharged | as above | alpha 1 | as above |
| hand full (selected) | `RejectRed` | **unchanged**: the border is independent | "Hand full", then the others |
| unprimed | as above | as above | "Unstable" is always kept; the label never says "Fire" |
| fused (`PowerScale > 1`) | as above | as above | adds "Fused" |
| locked slot (no packet) | Ivory if selected | off | "Locked" |
| empty | Ivory if selected | off | none |

Paused: `Now` is `sim.Clock.Now`, which does not advance while paused, so the pulse freezes and resumes where it left off. Nothing replays on resume, because `Since` only resets when the pulse condition goes from false to true or the packet changes.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.Presentation;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class SlotCueTests
    {
        static SlotInput Packet(bool selected, bool over, double now = 10, double since = 10) => new SlotInput
        { HasPacket = true, Selected = selected, Primed = true, Overcharged = over, Now = now, OverchargeSince = since };

        [Test] public void OrdinarySelectionIsIvoryWithNoGold()
        {
            var c = SlotCue.Evaluate(Packet(true, false));
            Assert.IsTrue(c.Marker); Assert.AreEqual(UiPalette.Ivory, c.MarkerColor); Assert.IsFalse(c.Border);
        }

        [Test] public void AnEmptySelectedSlotStillShowsTheIvoryMarker()
        {
            var c = SlotCue.Evaluate(new SlotInput { Selected = true });
            Assert.IsTrue(c.Marker); Assert.AreEqual(UiPalette.Ivory, c.MarkerColor); Assert.IsFalse(c.Border);
        }

        [Test] public void SelectedOverchargeUsesTheSharedGoldExactlyAndStartsBright()
        {
            var c = SlotCue.Evaluate(Packet(true, true));
            Assert.IsTrue(c.Border);
            var g = FeedbackColors.Overcharge;
            Assert.AreEqual(g.r, c.BorderColor.r); Assert.AreEqual(g.g, c.BorderColor.g); Assert.AreEqual(g.b, c.BorderColor.b);
            Assert.AreEqual(1f, c.BorderColor.a, 1e-4);
            Assert.IsTrue(c.CountdownGold);
        }

        [Test] public void ThePulseRunsBetween45And100PercentAt2Hz()
        {
            Assert.AreEqual(SlotCue.PulseMin, SlotCue.Evaluate(Packet(true, true, 10.25, 10)).BorderColor.a, 1e-4, "trough a quarter second in");
            Assert.AreEqual(1f, SlotCue.Evaluate(Packet(true, true, 10.5, 10)).BorderColor.a, 1e-4, "peak again at half a second");
        }

        [Test] public void FrozenOverchargeIsSteadyAndSaysFrozen()
        {
            var c = SlotCue.Evaluate(Packet(false, true, 10.25, 10));
            Assert.IsTrue(c.Border); Assert.AreEqual(1f, c.BorderColor.a, 1e-4);
            Assert.IsFalse(c.Marker);
            CollectionAssert.Contains(c.States, "state.frozen");
            CollectionAssert.Contains(c.States, "state.overcharge");
        }

        [Test] public void ReduceFlashesHoldsTheBorderSteady()
        {
            var i = Packet(true, true, 10.25, 10); i.ReduceFlashes = true;
            Assert.AreEqual(1f, SlotCue.Evaluate(i).BorderColor.a, 1e-4);
        }

        [Test] public void HandFullTurnsTheMarkerRedButKeepsTheGoldBorder()
        {
            var i = Packet(true, true); i.HandFull = true;
            var c = SlotCue.Evaluate(i);
            Assert.AreEqual(SlotCue.RejectRed, c.MarkerColor); Assert.IsTrue(c.Border);
        }

        [Test] public void AnUnstablePacketNeverSaysFire()
        {
            var i = Packet(true, true); i.Primed = false;
            var c = SlotCue.Evaluate(i);
            CollectionAssert.Contains(c.States, "state.unstable");
            StringAssert.DoesNotContain("Fire", c.Label);
        }

        [TestCase(false, true)] [TestCase(false, false)]
        public void EmptyAndLockedSlotsHaveNoBorder(bool selected, bool locked)
        {
            var c = SlotCue.Evaluate(new SlotInput { Selected = selected, Locked = locked, Overcharged = true });
            Assert.IsFalse(c.Border, "a stale overcharge never outlives its packet");
            if (locked) CollectionAssert.Contains(c.States, "state.locked");
        }

        [Test] public void TwoChargedPacketsBothShowGoldButOnlyTheSelectedOnePulses()
        {
            var sel = SlotCue.Evaluate(Packet(true, true, 10.25, 10));
            var frozen = SlotCue.Evaluate(Packet(false, true, 10.25, 10));
            Assert.IsTrue(sel.Border && frozen.Border);
            Assert.Less(sel.BorderColor.a, 1f); Assert.AreEqual(1f, frozen.BorderColor.a, 1e-4);
        }

        [Test] public void TrackStartsBrightOnEntryAndOnANewPacket()
        {
            var clock = new PulseClock(); var a = new object(); var b = new object();
            Assert.AreEqual(5.0, SlotCue.Track(ref clock, a, true, 5.0));
            Assert.AreEqual(5.0, SlotCue.Track(ref clock, a, true, 7.0), "same packet keeps pulsing from its start");
            Assert.AreEqual(8.0, SlotCue.Track(ref clock, b, true, 8.0), "a swapped-in packet starts bright");
            SlotCue.Track(ref clock, b, false, 9.0);
            Assert.AreEqual(9.5, SlotCue.Track(ref clock, b, true, 9.5), "re-selecting a frozen overcharge starts bright");
        }

        [Test] public void APausedClockFreezesThePulse()
        {
            var x = SlotCue.Evaluate(Packet(true, true, 10.1, 10)).BorderColor.a;
            var y = SlotCue.Evaluate(Packet(true, true, 10.1, 10)).BorderColor.a;
            Assert.AreEqual(x, y, "same gameplay time, same frame of the pulse");
        }
    }
}
```

`FeedbackColors` lives in `BorrowedHex.Presentation`; if it lives elsewhere, take the namespace from `AttackEmitter.cs`, which uses it.

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement `SlotCue`**

```csharp
using System.Collections.Generic;
using BorrowedHex.Presentation;
using UnityEngine;

namespace BorrowedHex.UI
{
    public struct SlotInput
    {
        public bool HasPacket, Locked, Selected, Primed, Overcharged, Fused, HandFull, ReduceFlashes;
        public double Now, OverchargeSince;
    }

    public struct PulseClock { public object Packet; public double Since; public bool Was; }

    /// <summary>
    /// Spec 2, "Explicit overcharge feedback for both hex slots", as one pure rule. Selection and
    /// overcharge are separate channels: an ivory marker says "this is the hand you fire from",
    /// a gold border says "this one is charged". Hand-full only touches the marker.
    /// </summary>
    public struct SlotCue
    {
        public const float PulseHz = 2f, PulseMin = 0.45f;
        public static readonly Color RejectRed = new Color(1f, 0.32f, 0.3f);

        public bool Marker; public Color MarkerColor;
        public bool Border; public Color BorderColor;
        public bool CountdownGold;
        public string[] States;
        public string Label;

        public static SlotCue Evaluate(in SlotInput i)
        {
            var c = new SlotCue { Marker = i.Selected, MarkerColor = i.HandFull ? RejectRed : UiPalette.Ivory };
            var states = new List<string>(4);
            var words = new List<string>(4);
            void Add(string glyph, string word) { states.Add(glyph); words.Add(word); }

            if (!i.HasPacket)
            {
                if (i.Locked) Add("state.locked", "Locked");
                c.States = states.ToArray(); c.Label = string.Join("  ", words);
                return c;   // no packet, no border: nothing stale survives a release or expiry
            }

            bool frozen = !i.Selected;
            if (i.HandFull) words.Add("Hand full");
            if (!i.Primed) Add("state.unstable", "Unstable");
            if (frozen) Add("state.frozen", "Frozen");
            if (i.Overcharged) Add("state.overcharge", "Overcharge");
            if (i.Fused) Add("state.fused", "Fused");
            if (words.Count == 0) words.Add("Decaying");

            if (i.Overcharged)
            {
                c.Border = true; c.CountdownGold = true;
                float a = 1f;
                if (!frozen && !i.ReduceFlashes)
                {
                    // cos starts at its peak, so entry is bright; mid 0.725 +- 0.275 spans 0.45..1.
                    double t = i.Now - i.OverchargeSince;
                    a = (1f + PulseMin) / 2f + (1f - PulseMin) / 2f * Mathf.Cos((float)(2.0 * Mathf.PI * PulseHz * t));
                }
                var g = FeedbackColors.Overcharge;
                c.BorderColor = new Color(g.r, g.g, g.b, a);
            }
            c.States = states.ToArray();
            c.Label = string.Join("  ", words);
            return c;
        }

        /// <summary>When the selected-overcharge pulse began. Resets on entry and on a new packet,
        /// so a swapped-in or re-selected charge always starts bright (spec).</summary>
        public static double Track(ref PulseClock clock, object packet, bool pulsing, double now)
        {
            if (pulsing && (!clock.Was || !ReferenceEquals(clock.Packet, packet))) clock.Since = now;
            clock.Was = pulsing; clock.Packet = packet;
            return clock.Since;
        }
    }
}
```

The float cosine at t = 0.25 s is `cos(π)`, which is −1 to within float error. The test tolerance of 1e-4 covers that.

- [ ] **Step 4: Rebuild `PacketIndicator` on `SlotCue`**

Each slot is a `frame.card` 300×96 at the existing positions. The bottom-centre anchor and the two-slot spacing stay; `PanelW` goes from 270 to 300 and `PanelH` from 70 to 96 to fit the icons. Its children:
- `Marker`: an Ivory 2-art-pixel chevron above the card (a `PixelGeometry` texture, 24×12). It is a separate Image, not an `Outline`, so it cannot be confused with the border.
- `Border`: a sliced Image over the card using `frame.cardAlt`'s outline only. If the art is missing, it is a 4-px coloured frame built from four Images. It is tinted per `SlotCue.BorderColor`.
- The left column: up to three `payload.*` glyphs with counts (Small), from `Contents(pk)`. Read how it groups payloads today and keep the grouping.
- The right column: `"{used}/{cap}"`, `"x{power:0.00}"` and `"{left:0.0} s"` in fixed rects, so the numbers never jump (spec "consistent positions").
- A state row of `States` glyphs (24 px each), followed by `Label` in Small.
- `Countdown`: the bottom bar, gold (`FeedbackColors.Overcharge`) while `CountdownGold`, the urgent colour under 0.5 s when selected and not overcharged, the fill colour otherwise. Unprimed halves its alpha, as today.

The per-frame loop builds a `SlotInput` per slot:

```csharp
                var pk = store.InSlot(i);
                bool selected = i == store.SelectedSlot;
                bool over = pk != null && pk.IsOvercharged(sim.Stats.Power);   // the slot's own packet only (spec)
                double since = SlotCue.Track(ref pulse[i], pk, pk != null && selected && over, now);
                var cue = SlotCue.Evaluate(new SlotInput
                {
                    HasPacket = pk != null, Locked = store.IsLocked(i), Selected = selected,
                    Primed = pk == null || sim.IsPrimed(pk), Overcharged = over, Fused = pk != null && pk.PowerScale > 1f,
                    HandFull = selected && Time.unscaledTime < handFullUntil,
                    ReduceFlashes = DisplayOptions.ReduceFlashes, Now = now, OverchargeSince = since,
                });
```

`pulse` is a `PulseClock[]` sized to the slot count. `Bind` resets it (`pulse = new PulseClock[2]`), so a rebind never carries a border or a pulse phase into the new run.

The hint becomes `"Catch [LMB]  ·  Fire [RMB]  ·  Swap [Q]"`. It is shown when `DisplayOptions.ShowHints && hintVisible`, where `hintVisible` is set by `SetHintVisible`.

The catch bar keeps its logic and moves onto `bar.trayDark` with a `fill.blue` fill.

- [ ] **Step 5: Add one PlayMode check for the wiring.** Add it to `GameRootPlayModeTests`: two overcharged packets in the two slots. Build them the way `ShortRunTests` builds captured packets; find the helper with `grep -n "InSlot\|Capture(" Assets/Game/Tests/EditMode/*.cs`. Assert:
- `PacketIndicator` slot 0 `Border` is active with alpha < 1 a quarter second (gameplay time) after selection.
- Slot 1 `Border` is active with alpha 1.
- After `Bind` to a fresh run, both borders are inactive.

Name the Border object `Border` so the test finds it with `transform.Find("Slot0/Border")`, and name the slots `Slot0` and `Slot1`.

- [ ] **Step 6: Run.** Run `bash .superpowers/rt.sh editor SlotCueTests`, `bash .superpowers/rtp.sh "GameRootPlayModeTests|LayoutTests"`, and the gate. Then capture with both slots charged (`cap.sh slots-charged`) and once with Reduce flashes on.

- [ ] **Step 7: Commit (when asked).** Message: `Give overcharged hex slots their own gold border and an ivory selection marker`

---

### Task 15: Upgrade offers and swap

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/UpgradeCopy.cs`
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs`, the upgrade section only. Every object name and every behaviour in the D96/R13 comments is kept: `UpgradeChoice`, `Panel`, `Card{i}` with `Text`, `Main` and `Swap`, `Replace{j}`, `Back`, `Continue` and `Retire`; `PaidClickGuard`; Continue as the default focus; Back as the default focus in the swap step.
- Test: `Assets/Game/Tests/EditMode/UpgradeCopyTests.cs`

**Interfaces:**
- Consumes: `LifeDisplay.Points`, `sim.TakeCost`, `sim.TakeCostFraction`, `sim.LifeSeconds`, `sim.Stats.StartingSeconds`, `UpgradeInfo.Name/Describe` and `UiGlyphs`.
- Produces:
  - `UpgradeCopy.LifePreview(float lifeSeconds, float capSeconds, float costSeconds)` returns `LifeCost { int Now, After, Cost, Percent; float FracNow, FracAfter; }`.
  - `UpgradeCopy.HeldLine(int held, int max)` returns `string`, for example "Held 2/4" or "Held 3/4  ·  last open slot".
  - `UpgradeCopy.Action(bool rankUp, int heldCount, int rank, int cost)` returns `string`, for example "Rank up to 2  ·  18 Life".

The layout changes, inside the same 900-wide panel:
- **Title:** `Encounter 1 cleared` / `Wave 3 complete` / `Boss defeated`, in alagard Heading and Honey. Sentence case.
- **Life summary**, where the flavour line was: a 600×24 `bar.tray` showing the current Life in Blood, with the part a purchase would take drawn in dark red (`UiPalette.Blood` at 40%). Beside it: "Life 142 → 124 (−18, 10%)". This is the "before/after life preview".
- **Held line:** one row with the held chips (the Task 13 chip prefab, 40 px) and `HeldLine` text. It is the only place held upgrades are listed, so the old "Holding:" text is removed. The final-slot warning appears here once, not on every card.
- **Cards:** `frame.card` rows, each with the glyph (48 px), the name plus rank (Sub, Honey), and the effect (Body), all on the left. The action column is on the right: **Main** (Primary, showing `UpgradeCopy.Action`) and **Swap** (Secondary, "Swap (free)").
- **Continue:** Secondary, label "Continue", default focus.
- **Retire:** Quiet, Endless only.

The swap step:
- **Heading:** "Replace an upgrade".
- The incoming upgrade once (a non-interactive card).
- One `Replace{j}` button per held upgrade, showing name, rank and effect.
- **Back** as the default focus.
- The title, Life summary, held line and other cards are hidden during this step (spec).

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class UpgradeCopyTests
    {
        [Test]
        public void ThePreviewSubtractsTheExactCost()
        {
            var c = UpgradeCopy.LifePreview(142f, 180f, 18f);
            Assert.AreEqual(UpgradeCopy.Points(142f), c.Now);
            Assert.AreEqual(UpgradeCopy.Points(124f), c.After);
            Assert.AreEqual(c.Now - c.After, c.Cost, "the bar and the button never disagree");
            Assert.AreEqual(10, c.Percent);
            Assert.Less(c.FracAfter, c.FracNow);
        }

        [Test] public void TheLastOpenSlotIsWarnedOnce() { Assert.AreEqual("Held 2/4", UpgradeCopy.HeldLine(2, 4)); StringAssert.Contains("last open slot", UpgradeCopy.HeldLine(3, 4)); }
        [Test] public void AFullSetSaysRankUpsOnly() => StringAssert.Contains("rank-ups only", UpgradeCopy.HeldLine(4, 4));
        [Test] public void ActionsKeepTheExactCostVisible()
        {
            Assert.AreEqual("Take  ·  18 Life", UpgradeCopy.Action(false, 0, 1, 18));
            Assert.AreEqual("Add  ·  18 Life", UpgradeCopy.Action(false, 2, 1, 18));
            Assert.AreEqual("Rank up to 2  ·  18 Life", UpgradeCopy.Action(true, 2, 2, 18));
        }
    }
}
```

The cost in display points is the existing `LifeDisplay.Points(sim.TakeCost)`. `UpgradeCopy.Points` forwards to it, so the test does not hard-code the seconds-to-points ratio. "The bar and the button never disagree" holds because both are computed from that one call. The percentage in the summary comes from `TakeCostFraction`, as `Price()` computes it today; pass it in if `LifePreview`'s own division rounds differently. Check that against the current `Price()` output in a capture.

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement `UpgradeCopy`**

```csharp
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.UI
{
    public struct LifeCost { public int Now, After, Cost, Percent; public float FracNow, FracAfter; }

    /// <summary>Upgrade-choice copy (spec 2, Upgrade offers): costs in Life, said once, exactly.</summary>
    public static class UpgradeCopy
    {
        const string Sep = "  ·  ";

        public static int Points(float seconds) => LifeDisplay.Points(seconds);

        public static LifeCost LifePreview(float lifeSeconds, float capSeconds, float costSeconds)
        {
            float cap = Mathf.Max(0.0001f, capSeconds);
            int now = Points(lifeSeconds), cost = Points(costSeconds);
            return new LifeCost
            {
                Now = now, Cost = cost, After = now - cost,
                Percent = Mathf.RoundToInt(costSeconds / cap * 100f),
                FracNow = Mathf.Clamp01(lifeSeconds / cap),
                FracAfter = Mathf.Clamp01((lifeSeconds - costSeconds) / cap),
            };
        }

        public static string HeldLine(int held, int max) =>
            held >= max ? $"Held {held}/{max}{Sep}rank-ups only"
            : held == max - 1 ? $"Held {held}/{max}{Sep}last open slot"
            : $"Held {held}/{max}";

        public static string Action(bool rankUp, int heldCount, int rank, int cost) =>
            (rankUp ? $"Rank up to {rank}" : heldCount == 0 ? "Take" : "Add") + $"{Sep}{cost} Life";
    }
}
```

The `Percent` in this function is computed against the cap, matching `TakeCostFraction` (`LifeDisplay` and the sim both measure against `StartingSeconds`). Read `ArenaSim.TakeCostFraction` and use its denominator if it differs.

`LifeDisplay.Points` may truncate or round. If `Points(a) - Points(b) != Points(a - b)` for the values in play, the test catches it. That is why `Cost` is defined as `Now - After` in the test rather than `Points(cost)`. Make the implementation match: set `After = Points(lifeSeconds - costSeconds)` and `Cost = Now - After`, and use `Cost` on the button too. The rule is that the button and the bar always show the same number.

- [ ] **Step 4: Restyle `RunFlowPanels`' upgrade section**

Replace the copy and the visuals as described above, and keep `FillCards`, `OnSwap` and `ShowSwapTargets` as the control flow:
- `upgradeNote` becomes the Life summary row.
- `upgradeHolding` becomes the held row.
- In the card body, the colour-tagged rich text is replaced by three Text children (Name, Rank, Effect). The `Text` child name stays on the effect text so `AssertCardsClean` still finds a `Text`.
- `cardMain[i]`'s label comes from `UpgradeCopy.Action(...)`.
- Locked-and-new cards keep their current rule (no Main button). They gain the `state.locked` glyph and the line "Set full: rank-ups only".

The `LayoutTests` upgrade tests already check card cleanliness and no overlap with the HUD; they cover the restyle. Add one assertion to `UpgradeChoice_...`: `root.Hud.transform.Find("Combat/Upgrades")` is inactive while the choice is open.

- [ ] **Step 5: Run.** Run `bash .superpowers/rt.sh editor UpgradeCopyTests`, `bash .superpowers/rtp.sh "LayoutTests|GameRootPlayModeTests"` (the paid-click-guard tests must pass untouched), then the gate. Capture the choice with 0, 3 and 4 held, plus the swap step (`cap.sh upgrade-*`).

- [ ] **Step 6: Commit (when asked).** Message: `Price upgrade offers in Life with a before-and-after preview`

---

### Task 16: Results

**Files:**
- Create: `Assets/Game/Scripts/UI/Logic/ResultsCopy.cs`
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs`, the results section. Kept: `Results` → `Panel` → `Body` (now inside Details), `Progress`, `PlayAgain` and `MainMenu`; `AgainLabel` containing "[R]".
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.Menus.cs`, `GameRoot.Progression.cs` and `GameRoot.Records.cs`. `FinalizeText`, `FinalizeTextExtra` and `FinalizeTextMore` are replaced by one `ResultsCopy.FromFinalize` call.
- Modify: `Assets/Game/Tests/PlayMode/LayoutTests.cs` (the results gap test measures the visible blocks)
- Test: `Assets/Game/Tests/EditMode/ResultsCopyTests.cs`

**Interfaces:**
- Consumes: `FinalizeResult` (`Applied`, `SkippedBecause`, `Saved`, `Xp`, `LevelBefore`, `LevelAfter`, `LevelsGained`, `NewAchievements`, `Records`), `MasteryState`, `Mastery.CostToAdvance`, `Achievements.Find`, `RunSummary`, and `Records.LongestRun`.
- Produces:
  - `struct ResultsOutcome { string XpLine; float XpFrac; string LevelUp; List<string> Rewards; List<string> Details; string Warning; string NotRecorded; }`.
  - `ResultsCopy.FromFinalize(FinalizeResult r, MasteryState m, string saveWarning)` returns `ResultsOutcome`.
  - `ResultsCopy.Title(RunSummary s)` returns `(string title, Color colour)`.
  - `ResultsCopy.Subtitle(RunSummary s)` returns `string`, or null when it would only repeat the title.
  - `RunFlowPanels.SetOutcome(ResultsOutcome)`, which replaces `SetProgress(string)`; keep `SetProgress` as an `[Obsolete]` wrapper only if something else calls it (grep first).

Layout: a `frame.ornate` panel, 960 wide, fit to content:
1. The title (alagard Title): "Victory" (Honey), "Defeated" (Blood), "Out of time" (Warning) or "Retired" (Honey). The subtitle is shown only when it adds information: Endless shows "Cycle 2 · 8 waves · 1 boss", and Short shows the time bonus if there is one.
2. `Headline`, three labelled stat blocks side by side: **Score**, **Duration** and **Kills**, each with the value in the Number font over the label in Small Muted.
3. `Progress`: the XP bar (`bar.tray` 480 with a `fill.green` fill) over "+46 XP"; the level-up line in Honey when there is one ("Mastery 3 → 4  ·  +1 skill point"); then the reward rows, one per new best and per new achievement, each with a gem and a name. When the run was not recorded: one Muted line instead, either "Practice run: not recorded" or "Cheats active: not recorded".
4. The save warning (Warning colour) when saving failed.
5. **Details** (Quiet). It toggles a parchment `ScrollView` 360 tall holding `Body`: the existing full statistics text (unchanged, built by the current `FillResults` string), plus the XP breakdown and the unchanged record comparisons.
6. **Play again [R]** (Primary, default focus) and **Main menu** (Secondary).

The HUD is covered with `Hud.SetCovered(true)` while the results show, which happens through the existing combat-group rule plus the Task 13 cover.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using BorrowedHex.Progression;
using BorrowedHex.UI;
using NUnit.Framework;

namespace BorrowedHex.Tests
{
    public class ResultsCopyTests
    {
        static FinalizeResult Applied() => new FinalizeResult
        {
            Applied = true, Saved = true, Xp = new XpBreakdown { NormalKills = 10, Total = 46 },
            LevelBefore = 3, LevelAfter = 3,
        };

        [Test] public void XpComesFirstAndTheBreakdownGoesToDetails()
        {
            var o = ResultsCopy.FromFinalize(Applied(), new MasteryState { level = 3, xp = 120 }, null);
            Assert.AreEqual("+46 XP", o.XpLine);
            Assert.AreEqual(120f / Mastery.CostToAdvance(3), o.XpFrac, 1e-4);
            Assert.IsTrue(o.Details.Exists(d => d.Contains("kills")));
            Assert.IsNull(o.LevelUp);
        }

        [Test] public void ALevelUpIsAReward()
        {
            var r = Applied(); r.LevelAfter = 4; r.LevelsGained = 1;
            StringAssert.Contains("Mastery 3 → 4", ResultsCopy.FromFinalize(r, new MasteryState { level = 4 }, null).LevelUp);
        }

        [Test] public void NewBestsAndAchievementsAreRewardsAndUnchangedRecordsAreDetails()
        {
            var r = Applied();
            r.NewAchievements.Add(Achievements.All[0].Id);
            r.Records.Add(new RecordOutcome { Kind = Records.BestScore, IsNewBest = true, ThisValue = 900, Previous = new RunRecord { score = 700 } });
            r.Records.Add(new RecordOutcome { Kind = Records.LongestRun, IsNewBest = false, ThisValue = 200, Previous = new RunRecord { duration = 300 } });
            var o = ResultsCopy.FromFinalize(r, new MasteryState { level = 3 }, null);
            Assert.IsTrue(o.Rewards.Exists(x => x.Contains(Achievements.All[0].Name)));
            Assert.IsTrue(o.Rewards.Exists(x => x.Contains("New best score")));
            Assert.IsFalse(o.Rewards.Exists(x => x.Contains("Longest")), "unchanged comparisons are not rewards");
            Assert.IsTrue(o.Details.Exists(x => x.Contains("Longest run")));
        }

        [Test] public void SkippedRunsSayWhyOnce()
        {
            var o = ResultsCopy.FromFinalize(new FinalizeResult { Applied = false, SkippedBecause = "sandbox" }, new MasteryState(), null);
            StringAssert.Contains("not recorded", o.NotRecorded);
            Assert.IsNull(o.XpLine);
        }

        [Test] public void ASaveFailureIsItsOwnLine()
        {
            var r = Applied(); r.Saved = false;
            Assert.AreEqual("Not saved: disk full", ResultsCopy.FromFinalize(r, new MasteryState { level = 3 }, "disk full").Warning);
        }

        [Test] public void TheSubtitleNeverRepeatsTheTitle()
        {
            // "Defeated" then "You fell" was the spec's example of redundancy.
            Assert.IsNull(ResultsCopy.Subtitle(TestRuns.Summary(BorrowedHex.Runs.RunEndReason.Death, GameMode.Short)));
        }
    }
}
```

`TestRuns.Summary(reason, mode)` is a test helper that builds a `RunSummary`. `RunSummary`'s fields are readonly, so find how the existing tests construct one: run `grep -rn "new RunSummary(" Assets/Game` and reuse that constructor. If no test builds one directly, give `ResultsCopy.Subtitle` the four values it needs as parameters (`reason`, `mode`, `cycle`, `wavesCompleted`, `bossesDefeated`, `victoryBonus`) instead of the summary, and drop the helper. **Prefer the parameter form**: it is simpler to test.

Check the namespaces of `MasteryState`, `RunRecord` and `GameMode` against `RecordRowsTests` and `StyleTests`, and copy their `using` lines.

- [ ] **Step 2: Run, expect a compile failure.**

- [ ] **Step 3: Implement `ResultsCopy`**

Move the strings out of `FinalizeTextExtra` (the XP parts list) and `FinalizeTextMore` (achievements and record lines) into `FromFinalize`, unchanged in content:
- XP: `XpLine = $"+{x.Total} XP"`. The non-zero parts go to Details as one line, "XP: kills 40, boss 25, …".
- Level up: `LevelUp = $"Mastery {r.LevelBefore} → {r.LevelAfter}  ·  +{r.LevelsGained} skill point{(r.LevelsGained == 1 ? "" : "s")}"`.
- `XpFrac = m.level >= Mastery.MaxLevel ? 1 : m.xp / (float)Mastery.CostToAdvance(m.level)`.
- New achievements: a reward each, `$"Achievement: {a?.Name ?? id}"`.
- Records: new bests (or first records) are rewards, `$"New best score: 900 (was 700)"`; anything else is a Details line, `$"Longest run: 5:00 (this run 3:20)"`. Reuse the existing `Fmt` logic.
- `NotRecorded`: `SkippedBecause` of "sandbox" gives "Practice run: not recorded"; "debug" gives "Cheats active: not recorded"; anything else gives null. "already finalized" must show nothing new, because it only happens on a replayed finalize.
- `Warning = r.Saved ? null : $"Not saved: {saveWarning}"`.

`Subtitle`:
- Endless: `$"Cycle {cycle}  ·  {waves} wave{s}  ·  {bosses} boss{es}"`.
- Short with a victory bonus: `$"Time bonus +{bonus}"`.
- Otherwise null.

`Title` maps the reasons as listed in the layout above, with `ToString()` for an unknown reason, as today.

- [ ] **Step 4: Wire it**

In GameRoot, replace `Flow.SetProgress(FinalizeText(LastFinalize))` with:

```csharp
            Flow.SetOutcome(ResultsCopy.FromFinalize(LastFinalize ?? new FinalizeResult(), Profile.Profile.mastery, Profile.Warning));
```

Then delete `FinalizeText`, `FinalizeTextExtra` and `FinalizeTextMore`. Before deleting, grep for `FinalizeText` under Tests; if a test asserts on the old strings, move that assertion to `ResultsCopyTests`. In `RunFlowPanels.FillResults`, keep the statistics string for `Body`, fill the headline blocks from the summary, and leave `Body` inside the collapsed Details.

`LayoutTests`: the results gap test checks the vertical gaps between the visible blocks (`Title`, `Headline`, `Progress` and the buttons) at ≤ 30 px. It also checks that `Progress` is sized to its content: its height equals `Ui.TextHeight` of its text plus the bar and rows it contains. Rewrite that assertion to name the blocks in the new order. Read the test before touching it; the gap rule is what it protects.

- [ ] **Step 5: Run.** Run `bash .superpowers/rt.sh editor ResultsCopyTests`, `bash .superpowers/rtp.sh "LayoutTests|GameRootPlayModeTests"` (`AgainLabel` must contain "[R]"), and the gate. Capture victory, death, endless retire, and a level-up with two achievements (`cap.sh results-*`).

- [ ] **Step 6: Commit (when asked).** Message: `Lead the results with outcome, XP and rewards, and fold statistics into Details`

---

### Task 17: Tutorial prompt, completion card and boss title

**Files:**
- Modify: `Assets/Game/Scripts/UI/TutorialPanel.cs`
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs` (the banner section)
- Modify: `Assets/Game/Scripts/Presentation/WorldArt/WorldIntroOverlay.cs`. This file is *not* in the owner's uncommitted set; `WorldPresentation.cs` is, so it is left untouched.
- Test: `Assets/Game/Tests/EditMode/TutorialCopyTests.cs`, plus one assertion in `WorldPresentationPlayModeTests`. That file has uncommitted changes from the owner, so **ask before editing it**; if the answer is no, put the assertion in a new `BossTitleTests.cs` instead.

**Interfaces:**
- Produces:
  - `TutorialPanel.PromptColour(float sincePromptChange, bool reduceFlashes)` returns `Color`. It is static and pure.
  - `TutorialPanel.CompletionReminder`, a `const string` that holds the spec sentence verbatim.
  - `WorldIntroOverlay.TitleShowing`, a `static bool`, true while the cinematic title is up.
  - `RunFlowPanels.BannerText(string bossName)` returns `string` (the name only).

Changes:
- **Prompt band:** a compact parchment plate (`frame.parchment`, 960×120, top-centre at y −180, below the objective line). It holds the header "Lesson 2 of 6" (Small, Camel; "Lesson complete" while celebrating), the instruction (Body, Ink #15121C on parchment), and the progress (Small). It keeps `raycastTarget = false` everywhere: the capture lesson's clicks must reach the arena (the existing comment explains why). The lesson number is shown *only* here (Task 13 removed it from the HUD).
- **Prompt flash:** `PromptColour` returns the steady ink colour when `reduceFlashes` is true. Otherwise it lerps Honey to ink over 0.5 s, as today.
- **Completion card:** an ornate frame, 760 wide. It holds the title "Tutorial complete" (alagard Heading, not 52-px bold Legacy), one reminder line (`CompletionReminder`: "Your life drains during runs. Defeat enemies to reclaim it."), **Play a run** (Primary) and **Main menu** (Secondary). The old three-line body is replaced, as the spec asks. `Hud.SetCovered(true)` and the hidden band are set while it shows.
- **Boss banner:** the "— BOSS —" label and the "Defeat it before the time runs out" sub-line are deleted. The name is shown once (alagard Title, Honey), between two `divider.a` ornaments, over a thinner 140-tall band. The banner's *timer* still runs when the cinematic owns the title, because `BannerDone` gates `CompleteBossIntro` and the spec says timing is unchanged. The band itself is hidden while `WorldIntroOverlay.TitleShowing` is true, so the name is presented once.
- **Cinematic title:** `WorldIntroOverlay.Render` sets the text to the name only (no "— BOSS —\n" prefix), with `UiFonts.Display` at 96. It sets `TitleShowing = title.gameObject.activeSelf` on every render; `Clear` and `Dispose` set it to false. It is a static flag because the overlay is owned by `WorldPresentation`, which this plan may not edit; the comment on the field must say so.

- [ ] **Step 1: Write the failing tests**

```csharp
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    public class TutorialCopyTests
    {
        [Test] public void ReduceFlashesKeepsThePromptSteady() =>
            Assert.AreEqual(TutorialPanel.PromptColour(0.5f, false), TutorialPanel.PromptColour(0f, true));

        [Test] public void APromptChangeFlashesWithoutTheSetting() =>
            Assert.AreNotEqual(TutorialPanel.PromptColour(0f, false), TutorialPanel.PromptColour(0.5f, false));

        [Test] public void TheReminderIsTheSpecSentence() =>
            Assert.AreEqual("Your life drains during runs. Defeat enemies to reclaim it.", TutorialPanel.CompletionReminder);

        [Test] public void TheBannerSaysTheNameOnly() =>
            Assert.AreEqual("The Collector", RunFlowPanels.BannerText("The Collector"));
    }
}
```

The PlayMode assertion: during a boss intro with the world art present, at most one of `WorldIntroOverlay.TitleShowing` and the Flow banner's band being active is true on any frame.

- [ ] **Step 2: Run, expect a failure. Step 3: Implement as described. Step 4: Run.**

Run `bash .superpowers/rt.sh editor TutorialCopyTests`, `bash .superpowers/rtp.sh "GameRootPlayModeTests|WorldPresentationPlayModeTests|LayoutTests"`, and the gate. Capture a tutorial lesson, the completion card and the boss intro, both with and without the world art (`cap.sh tutorial-*`, `cap.sh boss-intro`).

- [ ] **Step 5: Commit (when asked).** Message: `Compact the tutorial prompt and show the boss name once`

---

## Phase D: Proof

### Task 18: Layout sweep, captures, WebGL smoke test and docs

**Files:**
- Create: `Assets/Game/Tests/PlayMode/LayoutSweepTests.cs`
- Create: `Assets/Game/Tests/PlayMode/LayoutProbe.cs`. It holds the shared helpers, moved out of `LayoutTests.cs`, which then calls them.
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs`. Add `public Canvas Canvas => canvas;` so tests can reach the scaler; it is read-only.
- Modify: `Docs/WORLD_ART_HANDOFF.md` (the UI art rows, already added in Task 1; check that they match what shipped)
- Create: `Docs/UI_ART_CREDITS.md` (alagard, Dark Ages UI, Dark Dwellers UI and m5x7, each with source and licence as recorded under Q1 and Task 1)
- Modify: `Docs/TEST_EVIDENCE.md`. It holds the **owner's uncommitted edits**, so append only and stage the hunk with the `stage-mine.sh` pattern, never the whole file. Or ask first.
- Do not edit the spec. Report the pointer to the plan in the handoff message instead, because the spec is the owner's file.

**Interfaces:**
- Consumes everything above.
- Produces the acceptance evidence for spec §5.

- [ ] **Step 1: Write the sweep**

```csharp
using System.Collections;
using System.Collections.Generic;
using BorrowedHex.Presentation;
using BorrowedHex.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// Spec 5: "Every screen fits at 1280x720, 1600x900, 1920x1080, and ultrawide resolutions
    /// across existing 80-130% UI scales." The PlayMode game view cannot be resized, so each case
    /// sets the canvas scaler's reference resolution to what that screen and scale would produce
    /// (Unity's ScaleWithScreenSize, match 0.5) and lays out against the real screen. The boxes
    /// are compared in canvas space, which is what the scaler changes.
    /// </summary>
    public class LayoutSweepTests
    {
        static readonly Vector2[] Screens = { new Vector2(1280, 720), new Vector2(1600, 900), new Vector2(1920, 1080), new Vector2(3440, 1440) };
        static readonly float[] Scales = { 0.8f, 0.9f, 1f, 1.15f, 1.3f };

        // The screens to open in each case, by the same path a player takes.
        static readonly string[] Screens_ = { "main", "training", "settings", "cheats", "character.skills", "character.style", "records", "achievements", "pause", "pause.confirm", "hud", "upgrade", "upgrade.swap", "results", "tutorial.prompt", "tutorial.complete" };

        GameObject camGo, rootGo; GameRoot root;

        [UnitySetUp] public IEnumerator SetUp()
        {
            camGo = new GameObject("Cam") { tag = "MainCamera" }; camGo.AddComponent<Camera>();
            GameRoot.StorageOverride = new MemoryProfileStorage();
            rootGo = new GameObject("GameRoot"); root = rootGo.AddComponent<GameRoot>();
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { UiSkin.ForceFlat = false; Object.Destroy(rootGo); Object.Destroy(camGo); GameRoot.StorageOverride = null; yield return null; }

        [UnityTest]
        public IEnumerator EveryScreenFitsAtEverySizeAndScale([Values(false, true)] bool flat)
        {
            UiSkin.ForceFlat = flat;
            var failures = new List<string>();
            foreach (var screen in Screens)
                foreach (var scale in Scales)
                    foreach (var name in Screens_)
                    {
                        SetVirtualScreen(screen, scale);
                        yield return Open(name);
                        Canvas.ForceUpdateCanvases();
                        failures.AddRange(LayoutProbe.Check(root.Canvas.transform, VirtualRect(screen, scale), $"{name} @ {screen.x}x{screen.y} {scale:P0}{(flat ? " flat" : "")}"));
                        yield return Close(name);
                    }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
```

The test leans on three helpers:

1. **`LayoutProbe.Check(Transform root, Rect visible, string where)`**, which returns a `List<string>`. It moves the existing `LayoutTests` helpers (`ScreenBox`, `Overlap`, `AssertTextFits`, `Shows`) into a shared static test class, so both files use one implementation. Then it applies three rules to every *active, top-most* interactive or text box under the open screen:
   - nothing overlaps,
   - everything is inside `visible`,
   - all text fits.

   "Top-most" means the box is not nested in another Graphic, the same rule `HudBoxes` uses.

2. **`SetVirtualScreen(screen, scale)`** sets `root.Canvas.GetComponent<CanvasScaler>()` to the reference resolution `1920×1080 / scale`. It sets `RectTransform` of the canvas via `scaler.enabled = false` and assigns a fixed `scaleFactor` equal to what ScaleWithScreenSize would compute for `screen`:

   ```
   factor = 2^((log2(screen.x/ref.x) + log2(screen.y/ref.y)) / 2)
   ```

   This is Unity's formula for match 0.5, so the layout is the one a player on that screen would get, without resizing the Game view. Restore the scaler in TearDown.

3. **`Open(name)` and `Close(name)`** drive each screen through the public API used elsewhere in the tests: `Main.Press(...)`, `SetMenuOpen(true)`, `Flow` states via `Sim` debug calls, and so on. Copy how `LayoutTests` opens the upgrade choice and the results today. Close returns to the main menu with `ShowMainMenu()`.

The ultrawide case (3440×1440) has to show the arena on the right of the main menu, not a stretched column. The anchors in Task 6 give that. The probe's "inside visible" rule plus no overlap catches a column that drifts off-screen.

- [ ] **Step 2: Run the sweep.** Run: `bash .superpowers/rtp.sh LayoutSweepTests`. It runs 4 × 5 × 16 × 2 = 640 cases in one test. On failure, it prints every case, not just the first. Fix each failure **in the screen that owns it**, then rerun.

- [ ] **Step 3: Run the full gate.** Run `bash .superpowers/strict.sh both final`. Expected: all EditMode and PlayMode tests pass, with no new warnings.

- [ ] **Step 4: Capture the review set.** For each screen, take one capture at 1920×1080 at 100% and one at 1280×720 at 130%, with the art present (`cap.sh final-<screen>`). Put them in `.superpowers/shots/final/` for the owner's visual review.

- [ ] **Step 5: WebGL smoke test.** Build WebGL with the existing build script; find it with `grep -rn "BuildTarget.WebGL" Assets/Game/Editor`. Serve it locally and check five things by hand:
  1. The main menu has no Quit.
  2. Esc and P open and close pause, and Esc backs out of every sub-screen.
  3. The fonts render, with no fallback glyphs.
  4. The display stepper offers no Windowed option.
  5. Clicking a modal never fires in the arena.

  Record the result in `TEST_EVIDENCE.md` under a new "UI redesign" heading.

- [ ] **Step 6: Write the credits file.** One row per asset pack (name, author, source URL, licence as verified, date checked). For alagard, write the licence exactly as Q1 resolved it. If Q1 is still open, write "TBD: confirm commercial terms with the author (see Q1)". Do not write "free for commercial use" on aggregator evidence alone.

- [ ] **Step 7: Commit (when asked).** Message: `Sweep every screen across sizes and scales, and record the UI art credits`

---

