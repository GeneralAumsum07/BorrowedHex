using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// One place that knows the painted shader's name and its texture slots. _FromMap starts
    /// equal to _BaseMap so a material that never morphs shows its own texture.
    /// </summary>
    public static class PaintedMaterials
    {
        public const string ShaderName = "BorrowedHex/PaintedWorld";

        public static Material Create(string name, Texture texture, Color tint)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                // Loud rather than silent: a pink or unlit arena would hide the real cause.
                Debug.LogError("PaintedWorld shader missing; falling back to URP Lit for " + name);
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var material = new Material(shader) { name = name };
            if (texture != null) { material.SetTexture("_BaseMap", texture); material.SetTexture("_FromMap", texture); }
            material.SetColor("_BaseColor", tint);
            return material;
        }
    }
}
