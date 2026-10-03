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
        public WorldBackdrop(Transform parent, WorldArtLibrary art)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ContinuousWorldGround"; ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(55, -.85f, 25); ground.transform.localScale = new Vector3(600, .25f, 600);
            var collider = ground.GetComponent<Collider>(); collider.enabled = false; WorldArtLibrary.Release(collider);
            material = new Material(Shader.Find("BorrowedHex/PixelWorld"));
            texture = WorldPixelSurfaces.Create(art, "Courtyard", false);
            material.SetTexture("_BaseMap", texture); material.SetTexture("_FromMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one * 24);
            var renderer = ground.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        public void Render(Texture2D current, Texture2D previous, float blend, double clock, bool reduceFlashes, float north)
        {
            material.SetTexture("_BaseMap", current); material.SetTexture("_FromMap", previous != null ? previous : current);
            material.SetFloat("_Morph", blend); material.SetFloat("_GlitchTime", (float)clock);
            material.SetFloat("_Glitches", reduceFlashes ? 0 : 1);
            material.SetFloat("_NorthLimit", north + 1.3f);
        }
        public void SetReveal(float amount) => material.SetFloat("_Reveal", amount);
        public void Dispose() { WorldArtLibrary.Release(ground); WorldArtLibrary.Release(material); WorldArtLibrary.Release(texture); }
    }
}
