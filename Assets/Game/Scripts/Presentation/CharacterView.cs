using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Camera-facing 2D stand-in for a character, parented under a root that sits exactly at
    /// the logical (collision) position on the ground. Visual height, flip, flash and bob are
    /// presentation only; nothing here may move the root, so art can never shift hitboxes.
    /// Supplied artist sprites replace <see cref="SetSprite"/>'s input; a missing sprite falls
    /// back to the generated placeholder so the player is never invisible.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        SpriteRenderer body;
        SpriteRenderer shadow;
        Transform billboard;
        Camera cam;
        float flashUntil;
        bool blink;
        public float Scale { get; private set; } = 1f;

        // D6 hit recoil: a visual-only nudge along the hit plus a squash, settling in 0.1 s.
        // It moves the billboard child, never this root, which sits on the collision position.
        public const float RecoilSeconds = 0.1f, RecoilDistance = 0.1f;
        const float Squash = 0.2f;
        Vector2 recoilDir;
        float recoilAt = float.NegativeInfinity;
        // The scale the squash is applied around. Remembered separately because the squash
        // writes the body's localScale every frame and would otherwise compound on itself.
        float visualScale = 1f;

        // Impact frames (spec 4.3): a solid silhouette. The text shader draws the sprite's alpha
        // in the vertex colour, which is exactly a flat silhouette. One material is shared by
        // every view for the app's lifetime.
        static Material silhouetteMaterial;
        Material normalMaterial;
        // The tint callers asked for (ArenaView fades and telegraphs with alpha). Kept apart
        // from body.color because drawing white overrides the colour and must hand it back.
        Color tint = Color.white;
        public bool Silhouette { get; private set; }
        // True while the body is drawn through the silhouette material, for EITHER reason: an
        // impact frame or a hit flash. One flag for both so neither can switch off the other.
        internal bool DrawnWhite { get; private set; }
        public Sprite CurrentSprite => body.sprite;
        internal Vector3 BillboardOffset => billboard.localPosition;

        public static CharacterView Create(Transform parent, string name, PixelSprites.Kind kind, float scale, float shadowRadius)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CharacterView>();
            view.Build(kind, scale, shadowRadius);
            return view;
        }

        void Build(PixelSprites.Kind kind, float scale, float shadowRadius)
        {
            Scale = scale;
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(transform, false);
            sh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = Vector3.one * (shadowRadius * 2f);
            shadow = sh.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelSprites.Blob();
            shadow.color = new Color(0f, 0f, 0f, 0.55f);
            shadow.sortingOrder = -10;

            var bb = new GameObject("Billboard");
            bb.transform.SetParent(transform, false);
            billboard = bb.transform;
            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(billboard, false);
            sprite.transform.localScale = Vector3.one * scale;
            visualScale = scale;
            body = sprite.AddComponent<SpriteRenderer>();
            SetSprite(PixelSprites.Get(kind));
        }

        public void SetSprite(Sprite s) => body.sprite = s != null ? s : PixelSprites.Get(PixelSprites.Kind.Magician);

        // ---- Animated art (CharacterArt). Off until SetCharacter finds the character's clips;
        // without the asset (tests, an unbuilt library) the view keeps its static sprite. ----
        CharacterArt art;
        string character;      // "RogueMagician", "Acolyte", "Acolyte_Evolved", ...
        bool fourViews;        // the player has Left/Right art; enemies have one Side view, flipped for left
        string clipKey, state = "Idle";
        Sprite[] frames;
        float clipTime;

        /// <summary>True once animated art drives this body; static SetSprite/SetFacing then step aside.</summary>
        public bool Animated => frames != null;

        /// <summary>
        /// Switches this body to the named character's animations, keeping the current state and
        /// view. Returns false (and changes nothing) when the art has no clips for it.
        /// </summary>
        public bool SetCharacter(string name, bool hasFourViews)
        {
            art ??= CharacterArt.Load();
            if (art == null || art.Get($"{name}_Front_Idle") == null) return false;
            character = name;
            fourViews = hasFourViews;
            clipKey = null;
            Play(state, Vector2.down);
            return true;
        }

        /// <summary>
        /// Shows <paramref name="newState"/> facing <paramref name="dir"/> (sim plane: +y is away
        /// from the camera). Re-asking for the same clip continues it; a new clip starts at frame 0.
        /// One-shot states (Attack, Dash, Hurt, Death) hold their last frame until replaced.
        /// </summary>
        public void Play(string newState, Vector2 dir)
        {
            if (character == null) return;
            string view;
            bool flip = false;
            if (dir.sqrMagnitude < 1e-6f) view = clipKey != null ? CurrentView() : "Front";
            else if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x)) view = dir.y > 0f ? "Back" : "Front";
            else if (fourViews) view = dir.x < 0f ? "Left" : "Right";
            else { view = "Side"; flip = dir.x < 0f; }
            if (view == "Side" && dir.sqrMagnitude < 1e-6f) flip = body.flipX;
            string key = $"{character}_{view}_{newState}";
            var f = art.Get(key);
            if (f == null) return;   // a state this character lacks keeps the current clip
            body.flipX = flip;
            if (key == clipKey) return;
            clipKey = key; state = newState; frames = f; clipTime = 0f;
            body.sprite = frames[0];
        }

        /// <summary>The state of the clip playing now, and whether a one-shot has finished.</summary>
        public string State => state;
        public bool Finished => frames != null && !CharacterArt.Loops(state)
            && clipTime * CharacterArt.Fps(state) >= frames.Length;

        string CurrentView()
        {
            // "{character}_{View}_{State}": the view is the part between the two.
            var rest = clipKey.Substring(character.Length + 1);
            return rest.Substring(0, rest.IndexOf('_'));
        }

        void Animate(float dt)
        {
            if (frames == null) return;
            clipTime += dt;
            int i = Mathf.FloorToInt(clipTime * CharacterArt.Fps(state));
            i = CharacterArt.Loops(state) ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
            body.sprite = frames[i];
        }

        public void SetTint(Color c) { tint = c; body.color = Silhouette ? Color.white : tint; }
        public void SetVisualScale(float scale) { visualScale = scale; body.transform.localScale = Vector3.one * scale; }

        /// <summary>Face left/right toward the aim; purely cosmetic.</summary>
        public void SetFacing(float x)
        {
            // Animated art faces through Play's view choice instead.
            if (Animated) return;
            if (Mathf.Abs(x) > 0.05f) body.flipX = x < 0f;
        }

        /// <summary>Which way the sprite faces, so afterimages can copy it.</summary>
        public bool FlipX => body.flipX;

        /// <summary>
        /// The shared white-silhouette material, or null where the shader was stripped. Shared
        /// with the dash afterimages, which are silhouettes of the player too.
        /// </summary>
        internal static Material SilhouetteMaterial
        {
            get
            {
                if (silhouetteMaterial == null)
                {
                    var shader = Shader.Find("GUI/Text Shader");
                    if (shader != null) silhouetteMaterial = new Material(shader) { name = "ImpactSilhouette" };
                }
                return silhouetteMaterial;
            }
        }

        public void Flash(float seconds) => flashUntil = Time.unscaledTime + seconds;
        public void SetBlink(bool on) => blink = on;

        public void Recoil(Vector2 dirXZ)
        {
            recoilDir = dirXZ.sqrMagnitude > 1e-6f ? dirXZ.normalized : Vector2.zero;
            recoilAt = Time.unscaledTime;   // real time: the nudge plays through hit-stop
        }

        /// <summary>
        /// Poses the nudge and squash for time <paramref name="now"/>. A pure function of the
        /// last hit's time, so it is testable without frames and an old hit is simply k = 0.
        /// </summary>
        internal void ApplyRecoil(float now)
        {
            float k = 1f - Mathf.Clamp01((now - recoilAt) / RecoilSeconds);
            billboard.localPosition = new Vector3(recoilDir.x, 0f, recoilDir.y) * (RecoilDistance * k);
            body.transform.localScale = new Vector3(visualScale * (1f + Squash * k), visualScale * (1f - Squash * k), visualScale);
        }

        public void SetSilhouette(bool on)
        {
            if (on == Silhouette) return;
            Silhouette = on;
            ApplyFlash(Time.unscaledTime);   // swap now: an impact frame lasts only two frames
        }

        /// <summary>
        /// Draws the body white while an impact silhouette is on or a hit flash is running, and
        /// restores its own material otherwise. A pure function of state and <paramref name="now"/>
        /// so tests can step it without frames.
        /// </summary>
        /// <remarks>
        /// Playtest fix: the flash used to lerp body.color toward white. SpriteRenderer.color is a
        /// MULTIPLY tint, so it can only darken the texture, and over the usual white tint the
        /// lerp changed nothing at all. Only a different shader can draw the sprite brighter
        /// than itself, so the flash now uses the impact frame's silhouette material.
        /// </remarks>
        internal void ApplyFlash(float now)
        {
            bool want = (Silhouette || now < flashUntil) && SilhouetteMaterial != null;
            if (want != DrawnWhite)
            {
                if (want) { normalMaterial = body.sharedMaterial; body.sharedMaterial = SilhouetteMaterial; }
                else body.sharedMaterial = normalMaterial;
                DrawnWhite = want;
            }
            // The silhouette shader draws texture alpha x vertex colour, so the tint's alpha
            // still applies: a faded enemy flashes faded. An impact frame is always solid.
            body.color = Silhouette ? Color.white : tint;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) billboard.rotation = cam.transform.rotation;
            // Scaled time: animations freeze with the game in pause menus and hit-stop.
            Animate(Time.deltaTime);
            ApplyRecoil(Time.unscaledTime);
            ApplyFlash(Time.unscaledTime);
            bool hidden = blink && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.4f;
            // Stripped shader (no white to draw): the flash shows as a brief blink instead,
            // which still reads as "hit" where the old no-op tint read as nothing.
            if (SilhouetteMaterial == null && Time.unscaledTime < flashUntil) hidden = true;
            body.enabled = !hidden;
        }
    }
}
