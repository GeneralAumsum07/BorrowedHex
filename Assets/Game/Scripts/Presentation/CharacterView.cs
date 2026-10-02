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
            body = sprite.AddComponent<SpriteRenderer>();
            SetSprite(PixelSprites.Get(kind));
        }

        public void SetSprite(Sprite s) => body.sprite = s != null ? s : PixelSprites.Get(PixelSprites.Kind.Magician);

        public void SetTint(Color c) => body.color = c;
        public void SetVisualScale(float scale) => body.transform.localScale = Vector3.one * scale;

        /// <summary>Face left/right toward the aim; purely cosmetic.</summary>
        public void SetFacing(float x)
        {
            if (Mathf.Abs(x) > 0.05f) body.flipX = x < 0f;
        }

        public void Flash(float seconds) => flashUntil = Time.unscaledTime + seconds;
        public void SetBlink(bool on) => blink = on;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) billboard.rotation = cam.transform.rotation;
            bool hidden = blink && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.4f;
            body.enabled = !hidden;
            if (Time.unscaledTime < flashUntil) body.color = Color.Lerp(body.color, Color.white, 0.5f);
        }
    }
}
