using System.Collections.Generic;
using UnityEngine;

namespace CraftSort
{
    public static class IconFactory
    {
        private static readonly Dictionary<SortMode, string> ModeToKey = new Dictionary<SortMode, string>
        {
            { SortMode.None,         "all" },
            { SortMode.Armor,        "armor" },
            { SortMode.Block,        "block" },
            { SortMode.PhysDmg,      "phys" },
            { SortMode.SlashDmg,     "chop" },
            { SortMode.PierceDmg,    "pierce" },
            { SortMode.BluntDmg,     "blunt" },
            { SortMode.FireDmg,      "fire" },
            { SortMode.FrostDmg,     "frost" },
            { SortMode.LightningDmg, "lightning" },
            { SortMode.PoisonDmg,    "poison" },
            { SortMode.SpiritDmg,    "spirit" },
            { SortMode.ChopDmg,      "chop" },
            { SortMode.Health,       "hp" },
            { SortMode.Stamina,      "stamina" },
            { SortMode.Eitr,         "eitr" },
            { SortMode.Name,         "name" },
            { SortMode.New,          "new" },
        };

        private static readonly Dictionary<SortMode, Sprite> _cache = new Dictionary<SortMode, Sprite>();
        private static readonly Dictionary<string, Sprite> _keyCache = new Dictionary<string, Sprite>();

        public static Sprite? GetIcon(SortMode mode)
        {
            if (_cache.TryGetValue(mode, out var cached))
                return cached;

            if (!ModeToKey.TryGetValue(mode, out string key))
                return null;

            var sprite = GetIconByKey(key);
            if (sprite != null)
                _cache[mode] = sprite;
            return sprite;
        }

        public static Sprite? GetIconByKey(string key)
        {
            if (_keyCache.TryGetValue(key, out var cached))
                return cached;

            if (!IconData.Raw.TryGetValue(key, out string b64))
                return null;

            byte[] rgba = System.Convert.FromBase64String(b64);
            int size = IconData.Size;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.LoadRawTextureData(rgba);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            _keyCache[key] = sprite;
            return sprite;
        }

        internal static void Reset()
        {
            foreach (var sprite in _keyCache.Values)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                Object.Destroy(sprite);
                if (texture != null) Object.Destroy(texture);
            }

            _keyCache.Clear();
            _cache.Clear();
        }
    }
}
