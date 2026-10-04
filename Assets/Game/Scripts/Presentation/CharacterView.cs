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
        Color colorBeforeSilhouette;
        public bool Silhouette { get; private set; }
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

        public void SetTint(Color c) => body.color = c;
        public void SetVisualScale(float scale) { visualScale = scale; body.transform.localScale = Vector3.one * scale; }

        /// <summary>Face left/right toward the aim; purely cosmetic.</summary>
        public void SetFacing(float x)
        {
            if (Mathf.Abs(x) > 0.05f) body.flipX = x < 0f;
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
            if (on && silhouetteMaterial == null)
            {
                var shader = Shader.Find("GUI/Text Shader");
                if (shader == null) { Flash(0.05f); return; }   // stripped shader: fall back to the plain flash
                silhouetteMaterial = new Material(shader) { name = "ImpactSilhouette" };
            }
            if (on)
            {
                normalMaterial = body.sharedMaterial;
                colorBeforeSilhouette = body.color;
                body.sharedMaterial = silhouetteMaterial;
                body.color = Color.white;
            }
            else
            {
                body.sharedMaterial = normalMaterial;
                body.color = colorBeforeSilhouette;
            }
            Silhouette = on;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) billboard.rotation = cam.transform.rotation;
            ApplyRecoil(Time.unscaledTime);
            bool hidden = blink && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.4f;
            body.enabled = !hidden;
            // A silhouette is already pure white; the flash must not touch its colour.
            if (!Silhouette && Time.unscaledTime < flashUntil) body.color = Color.Lerp(body.color, Color.white, 0.5f);
        }
    }
}
