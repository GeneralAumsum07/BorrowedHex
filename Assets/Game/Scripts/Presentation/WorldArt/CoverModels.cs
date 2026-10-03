using BorrowedHex.Data;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// Gameplay cover as kit models (spec 3.4). Each model is stretched so its bounding
    /// footprint fills 92% of the analytic collision rect: what blocks a shot looks like
    /// it blocks a shot, and nothing visibly overhangs into open floor. Collision itself
    /// stays in ArenaSim.
    /// </summary>
    public static class CoverModels
    {
        public const float Fill = .92f;

        public static string Model(DecayPropKind kind, int index)
        {
            switch (kind)
            {
                case DecayPropKind.Tomb: return index % 2 == 0 ? "crypt-small" : "gravestone-wide";
                case DecayPropKind.Urn: return "urn-round";
                case DecayPropKind.Column: return "column-large";
                case DecayPropKind.Rock: return "Rock_Moss_" + (1 + index % 7);
                case DecayPropKind.DeadTree: return "CommonTree_Dead_" + (1 + index % 5);
                case DecayPropKind.Obelisk: return "pillar-obelisk";
                case DecayPropKind.Crystal: return WorldModelCatalog.Crystal;
                default: return "stone-wall-damaged"; // RuinedWall and any later kind
            }
        }

        public static string Rubble(DecayPropKind kind, int index)
        {
            switch (kind)
            {
                case DecayPropKind.Tomb: return "gravestone-debris";
                case DecayPropKind.Rock: return "Rock_" + (1 + index % 7);
                case DecayPropKind.DeadTree: return "trunk";
                case DecayPropKind.Crystal: return WorldModelCatalog.Crystal; // flattened to shards by the rubble height
                default: return "debris";
            }
        }

        // Heights keep each kind's old silhouette: the obelisk top stays under the 3.1
        // flame anchor, and low kinds (urns, tombs) stay under the actors' 1.2 eye line plus a margin.
        public static float Height(DecayPropKind kind)
        {
            switch (kind)
            {
                case DecayPropKind.Column: return 3.2f;
                case DecayPropKind.Tomb: return 1.6f;
                case DecayPropKind.DeadTree: return 3.6f;
                case DecayPropKind.Rock: return 1.6f;
                case DecayPropKind.Obelisk: return 2.8f;
                case DecayPropKind.Urn: return 1.3f;
                case DecayPropKind.Crystal: return 1.8f;
                default: return 2.2f;
            }
        }

        public static void Fit(Vector3 size, Vector2 rect, float height, out Vector3 scale, out float yaw)
        {
            // Turn the model so its long side runs along the rect's long side; stretching a
            // wall 3x across its thin axis would look far worse than a quarter turn.
            yaw = size.x >= size.z == rect.x >= rect.y ? 0 : 90;
            // After a 90 degree yaw the model's local x lies along world z.
            float alongLocalX = yaw == 0 ? rect.x : rect.y, alongLocalZ = yaw == 0 ? rect.y : rect.x;
            scale = new Vector3(alongLocalX * Fill / Mathf.Max(.01f, size.x), height / Mathf.Max(.01f, size.y),
                alongLocalZ * Fill / Mathf.Max(.01f, size.z));
        }

        public static GameObject Build(WorldModelLibrary library, Transform parent, string name, string model, Vector2 rect, float height)
        {
            var value = library.Spawn(model, parent, out var size);
            if (value == null) return null; // already logged by the library; the cover stays invisible but solid
            value.name = name;
            Fit(size, rect, height, out var scale, out float yaw);
            // TRS order: the scale applies in model space, then the yaw turns it into place.
            value.transform.localPosition = Vector3.zero;
            value.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            value.transform.localScale = scale;
            return value;
        }
    }
}
