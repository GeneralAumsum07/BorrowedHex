using System;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// The only kit models the recipes may use. The importer copies exactly this list, so the
    /// committed subset stays small; a name typed in a recipe but missing here fails a test.
    /// Excluded on purpose (spec 3.5): pumpkins, hay, characters, snow/autumn/cactus/palm.
    /// </summary>
    public static class WorldModelCatalog
    {
        public const string Root = "WorldModels";
        public const string Crystal = "crystal-cluster"; // neither kit has one; WorldModelLibrary builds it

        public static readonly string[] Kenney =
        {
            "altar-stone", "border-pillar", "brick-wall", "candle-multiple", "column-large", "crypt-a", "crypt-b",
            "crypt-large", "crypt-large-door", "crypt-small", "debris", "fire-basket", "grave-border",
            "gravestone-broken", "gravestone-cross", "gravestone-debris", "gravestone-decorative", "gravestone-round",
            "gravestone-wide", "iron-fence", "iron-fence-border", "iron-fence-damaged", "lantern-candle",
            "lightpost-single", "pillar-obelisk", "pine-crooked", "rocks", "rocks-tall", "stone-wall",
            "stone-wall-column", "stone-wall-damaged", "trunk", "urn-round",
        };

        public static readonly string[] Quaternius =
        {
            "CommonTree_Dead_1", "CommonTree_Dead_2", "CommonTree_Dead_3", "CommonTree_Dead_4", "CommonTree_Dead_5",
            "Willow_Dead_1", "Willow_Dead_2", "Willow_Dead_3", "Willow_Dead_4", "Willow_Dead_5",
            "Rock_1", "Rock_2", "Rock_3", "Rock_4", "Rock_5", "Rock_6", "Rock_7",
            "Rock_Moss_1", "Rock_Moss_2", "Rock_Moss_3", "Rock_Moss_4", "Rock_Moss_5", "Rock_Moss_6", "Rock_Moss_7",
            "TreeStump_Moss", "Bush_1", "Bush_2",
        };

        public static bool IsKenney(string name) => Array.IndexOf(Kenney, name) >= 0;
        public static string ResourcePath(string name) => Root + (IsKenney(name) ? "/Kenney/" : "/Quaternius/") + name;
        public static bool Exists(string name) => name == Crystal || Resources.Load<GameObject>(ResourcePath(name)) != null;
    }
}
