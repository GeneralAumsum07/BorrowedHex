using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Continuous ground extends beyond every possible camera frustum and travel route.
    /// The arena is embedded in a world instead of floating above the clear colour.
    /// </summary>
    public sealed class WorldBackdrop : System.IDisposable
    {
        readonly GameObject ground;
        readonly Material material;
        readonly Texture2D texture;
        public WorldBackdrop(Transform parent, Material template)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ContinuousWorldGround"; ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(55, -.85f, 25); ground.transform.localScale = new Vector3(600, .25f, 600);
            var collider = ground.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(.24f, .23f, .25f);
            material.SetFloat("_ReceiveShadows", 0); material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float shade = .55f + Mathf.PerlinNoise(x / 12f, y / 12f) * .35f;
                if ((x * 37 + y * 19) % 71 == 0) shade *= .6f;
                pixels[y * 64 + x] = new Color(shade * .9f, shade * .94f, shade);
            }
            texture.SetPixels(pixels); texture.Apply(); material.mainTexture = texture; material.mainTextureScale = Vector2.one * 70;
            var renderer = ground.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        public void Dispose() { WorldArtLibrary.Release(ground); WorldArtLibrary.Release(material); WorldArtLibrary.Release(texture); }
    }
}
