using System.Collections.Generic;
using UnityEngine;

namespace CraftSort
{
    public enum SortMode
    {
        None,
        Armor,
        Block,
        PhysDmg,
        SlashDmg,
        PierceDmg,
        BluntDmg,
        ChopDmg,
        FireDmg,
        FrostDmg,
        LightningDmg,
        PoisonDmg,
        SpiritDmg,
        Health,
        Stamina,
        Eitr,
        Name,
        New
    }

    public enum WeaponFilter
    {
        None,
        OneHanded,
        TwoHanded
    }

    public static class SortLogic
    {
        public static SortMode CurrentMode = SortMode.None;
        public static WeaponFilter CurrentFilter = WeaponFilter.None;

        private static float[] _valueCache = System.Array.Empty<float>();
        private static int[] _indexCache = System.Array.Empty<int>();
        private static string[] _nameCache = System.Array.Empty<string>();
        private static bool[] _pinnedCache = System.Array.Empty<bool>();
        private static Recipe?[] _recipeCache = System.Array.Empty<Recipe?>();
        private static readonly Dictionary<string, ItemDrop> _convertedFoodCache = new Dictionary<string, ItemDrop>();

        public static float[] GetValueCache() => _valueCache;
        public static int[] GetIndexCache() => _indexCache;
        public static string[] GetNameCache() => _nameCache;
        public static IComparer<int> ValueComparer => ValueIndexComparer.Instance;
        public static IComparer<int> NameComparer => NameIndexComparer.Instance;
        public static bool[] GetPinnedCache() => _pinnedCache;

        internal static void Reset()
        {
            CurrentMode = SortMode.None;
            CurrentFilter = WeaponFilter.None;
            _valueCache = System.Array.Empty<float>();
            _indexCache = System.Array.Empty<int>();
            _nameCache = System.Array.Empty<string>();
            _pinnedCache = System.Array.Empty<bool>();
            _recipeCache = System.Array.Empty<Recipe?>();
            _convertedFoodCache.Clear();
        }

        public static float GetSortValue(Recipe? recipe)
        {
            if (recipe == null) return 0f;
            var item = recipe.m_item;
            if (item == null) return 0f;
            var data = item.m_itemData;
            if (data == null) return 0f;
            var s = data.m_shared;
            if (s == null) return 0f;

            switch (CurrentMode)
            {
                case SortMode.Armor:
                    return s.m_armor + ArmorTypePriority(s);
                case SortMode.Block:
                    return s.m_blockPower + BlockTypePriority(s);
                case SortMode.PhysDmg:
                    return s.m_damages.GetTotalPhysicalDamage() + WeaponTypePriority(s);
                case SortMode.SlashDmg:
                    return s.m_damages.m_slash + WeaponTypePriority(s);
                case SortMode.PierceDmg:
                    return s.m_damages.m_pierce + WeaponTypePriority(s);
                case SortMode.BluntDmg:
                    return s.m_damages.m_blunt + WeaponTypePriority(s);
                case SortMode.ChopDmg:
                    return s.m_damages.m_chop + WeaponTypePriority(s);
                case SortMode.FireDmg:
                    return s.m_damages.m_fire;
                case SortMode.FrostDmg:
                    return s.m_damages.m_frost;
                case SortMode.LightningDmg:
                    return s.m_damages.m_lightning;
                case SortMode.PoisonDmg:
                    return s.m_damages.m_poison;
                case SortMode.SpiritDmg:
                    return s.m_damages.m_spirit;
                case SortMode.Health:
                    return GetRecoveryValue(recipe, s, SortMode.Health);
                case SortMode.Stamina:
                    return GetRecoveryValue(recipe, s, SortMode.Stamina);
                case SortMode.Eitr:
                    return GetRecoveryValue(recipe, s, SortMode.Eitr);
                default:
                    return 0f;
            }
        }

        private const float TypeBonus = 100000f;
        private const float LongRegenPriority = 1000000f;
        private const float LongRegenDurationSeconds = 60f;

        private static float ArmorTypePriority(ItemDrop.ItemData.SharedData s)
        {
            switch ((int)s.m_itemType)
            {
                case 6: case 7: case 11: case 12: case 17: case 18:
                    return TypeBonus;
                default:
                    return 0f;
            }
        }

        private static float BlockTypePriority(ItemDrop.ItemData.SharedData s)
        {
            return (int)s.m_itemType == 5 ? TypeBonus : 0f;
        }

        private static float GetRecoveryValue(
            Recipe recipe,
            ItemDrop.ItemData.SharedData fallback,
            SortMode mode)
        {
            return GetItemRecoveryValue(GetRecoverySharedData(recipe, fallback), mode);
        }

        public static bool ProvidesRecovery(Recipe? recipe, SortMode mode)
        {
            if (recipe?.m_item?.m_itemData?.m_shared == null) return false;
            var shared = GetRecoverySharedData(recipe, recipe.m_item.m_itemData.m_shared);
            float baseValue;
            float upfront = 0f;
            float overTime = 0f;
            float regenMultiplier = 1f;

            var consumeStats = shared.m_consumeStatusEffect as SE_Stats;
            switch (mode)
            {
                case SortMode.Health:
                    baseValue = shared.m_food;
                    if (consumeStats != null)
                    {
                        upfront = consumeStats.m_healthUpFront;
                        overTime = consumeStats.m_healthOverTime;
                        regenMultiplier = consumeStats.m_healthRegenMultiplier;
                    }
                    break;
                case SortMode.Stamina:
                    baseValue = shared.m_foodStamina;
                    if (consumeStats != null)
                    {
                        upfront = consumeStats.m_staminaUpFront;
                        overTime = consumeStats.m_staminaOverTime;
                        regenMultiplier = consumeStats.m_staminaRegenMultiplier;
                    }
                    break;
                case SortMode.Eitr:
                    baseValue = shared.m_foodEitr;
                    if (consumeStats != null)
                    {
                        upfront = consumeStats.m_eitrUpFront;
                        overTime = consumeStats.m_eitrOverTime;
                        regenMultiplier = consumeStats.m_eitrRegenMultiplier;
                    }
                    break;
                default:
                    return true;
            }

            return baseValue > 0f || upfront > 0f || overTime > 0f || regenMultiplier > 1f;
        }

        private static ItemDrop.ItemData.SharedData GetRecoverySharedData(
            Recipe recipe,
            ItemDrop.ItemData.SharedData fallback)
        {
            var rawItem = recipe.m_item;
            if (rawItem == null) return fallback;

            string rawPrefabName = rawItem.gameObject.name;
            if (_convertedFoodCache.TryGetValue(rawPrefabName, out var cached) && cached != null)
            {
                var cachedShared = cached.m_itemData?.m_shared;
                if (cachedShared != null) return cachedShared;
            }

            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null) return fallback;

            for (int i = 0; i < scene.m_prefabs.Count; i++)
            {
                var prefab = scene.m_prefabs[i];
                if (prefab == null) continue;

                var converted = FindConvertedItem(prefab, rawPrefabName);
                var convertedShared = converted?.m_itemData?.m_shared;
                if (convertedShared == null) continue;

                _convertedFoodCache[rawPrefabName] = converted!;
                return convertedShared;
            }

            return fallback;
        }

        private static ItemDrop? FindConvertedItem(GameObject prefab, string rawPrefabName)
        {
            var cookingStation = prefab.GetComponent<CookingStation>();
            if (cookingStation != null && cookingStation.m_conversion != null)
            {
                for (int i = 0; i < cookingStation.m_conversion.Count; i++)
                {
                    var conversion = cookingStation.m_conversion[i];
                    if (conversion != null && conversion.m_from != null && conversion.m_to != null
                        && conversion.m_from.gameObject.name == rawPrefabName)
                        return conversion.m_to;
                }
            }

            var fermenter = prefab.GetComponent<Fermenter>();
            if (fermenter != null && fermenter.m_conversion != null)
            {
                for (int i = 0; i < fermenter.m_conversion.Count; i++)
                {
                    var conversion = fermenter.m_conversion[i];
                    if (conversion != null && conversion.m_from != null && conversion.m_to != null
                        && conversion.m_from.gameObject.name == rawPrefabName)
                        return conversion.m_to;
                }
            }

            return null;
        }

        private static float GetItemRecoveryValue(ItemDrop.ItemData.SharedData shared, SortMode mode)
        {
            float value;
            float upfront = 0f;
            float overTime = 0f;
            float regenMultiplier = 1f;

            var consumeStats = shared.m_consumeStatusEffect as SE_Stats;
            switch (mode)
            {
                case SortMode.Health:
                    value = shared.m_food;
                    if (consumeStats != null)
                    {
                        upfront = consumeStats.m_healthUpFront;
                        overTime = consumeStats.m_healthOverTime;
                        regenMultiplier = consumeStats.m_healthRegenMultiplier;
                    }
                    break;
                case SortMode.Stamina:
                    value = shared.m_foodStamina;
                    if (consumeStats != null)
                    {
                        upfront = consumeStats.m_staminaUpFront;
                        overTime = consumeStats.m_staminaOverTime;
                        regenMultiplier = consumeStats.m_staminaRegenMultiplier;
                    }
                    break;
                case SortMode.Eitr:
                    value = shared.m_foodEitr;
                    if (consumeStats != null)
                    {
                        upfront = consumeStats.m_eitrUpFront;
                        overTime = consumeStats.m_eitrOverTime;
                        regenMultiplier = consumeStats.m_eitrRegenMultiplier;
                    }
                    break;
                default:
                    return 0f;
            }

            float longRegenPriority = consumeStats != null
                && consumeStats.m_ttl >= LongRegenDurationSeconds
                && regenMultiplier > 1f
                    ? LongRegenPriority + regenMultiplier
                    : 0f;

            return longRegenPriority + value + System.Math.Max(0f, upfront) + System.Math.Max(0f, overTime);
        }

        private static float WeaponTypePriority(ItemDrop.ItemData.SharedData s)
        {
            int t = (int)s.m_itemType;
            return t == 3 || t == 14 || t == 4 || t == 20 ? TypeBonus : 0f;
        }

        public static bool PassesFilter(Recipe? recipe)
        {
            if (CurrentFilter == WeaponFilter.None) return true;
            if (recipe == null) return false;
            var shared = recipe.m_item?.m_itemData?.m_shared;
            if (shared == null) return false;

            int type = (int)shared.m_itemType;
            switch (CurrentFilter)
            {
                case WeaponFilter.OneHanded:
                    return type == 3; // OneHandedWeapon
                case WeaponFilter.TwoHanded:
                    return type == 14 || type == 4 || type == 20; // TwoHandedWeapon, Bow, Attach_Atgeir
                default:
                    return true;
            }
        }

        public static void EnsureCaches(int count)
        {
            if (_valueCache.Length < count)
            {
                _valueCache = new float[count];
                _indexCache = new int[count];
                _nameCache = new string[count];
            }
            if (_pinnedCache.Length < count)
                _pinnedCache = new bool[count];
        }

        /// <summary>
        /// Sorts a List&lt;Recipe&gt; in-place by CurrentMode.
        /// Used for AAA Crafting's full cached list (global pagination sort).
        /// </summary>
        public static void SortRecipeList(List<Recipe> recipes)
        {
            if (recipes == null || recipes.Count < 2) return;
            PinnedRecipeTracker.EnsureLoaded();
            if (CurrentMode == SortMode.None && !PinnedRecipeTracker.HasAny) return;

            int count = recipes.Count;
            EnsureCaches(count);
            if (_recipeCache.Length < count)
                _recipeCache = new Recipe?[count];

            if (CurrentMode == SortMode.Name)
            {
                var nameCache = GetNameCache();
                var indexCache = GetIndexCache();
                var loc = Localization.instance;
                for (int i = 0; i < count; i++)
                {
                    string? key = recipes[i]?.m_item?.m_itemData?.m_shared?.m_name;
                    indexCache[i] = i;
                    _pinnedCache[i] = CurrentMode == SortMode.None && PinnedRecipeTracker.IsPinned(recipes[i]);
                    nameCache[i] = (key != null && loc != null) ? (loc.Localize(key) ?? "") : (key ?? "");
                }
                System.Array.Sort(indexCache, 0, count, NameComparer);
            }
            else
            {
                var valueCache = GetValueCache();
                var indexCache = GetIndexCache();
                for (int i = 0; i < count; i++)
                {
                    valueCache[i] = CurrentMode == SortMode.None ? 0f : GetSortValue(recipes[i]);
                    indexCache[i] = i;
                    _pinnedCache[i] = CurrentMode == SortMode.None && PinnedRecipeTracker.IsPinned(recipes[i]);
                }
                System.Array.Sort(indexCache, 0, count, ValueComparer);
            }

            var idx = GetIndexCache();
            for (int i = 0; i < count; i++)
                _recipeCache[i] = recipes[idx[i]];
            for (int i = 0; i < count; i++)
                recipes[i] = _recipeCache[i]!;
        }

        private sealed class ValueIndexComparer : IComparer<int>
        {
            public static readonly ValueIndexComparer Instance = new ValueIndexComparer();
            public int Compare(int x, int y)
            {
                if (_pinnedCache[x] != _pinnedCache[y])
                    return _pinnedCache[x] ? -1 : 1;
                int cmp = _valueCache[y].CompareTo(_valueCache[x]);
                return cmp != 0 ? cmp : x.CompareTo(y);
            }
        }

        private sealed class NameIndexComparer : IComparer<int>
        {
            public static readonly NameIndexComparer Instance = new NameIndexComparer();
            public int Compare(int x, int y)
            {
                if (_pinnedCache[x] != _pinnedCache[y])
                    return _pinnedCache[x] ? -1 : 1;
                int cmp = string.Compare(_nameCache[x], _nameCache[y], System.StringComparison.OrdinalIgnoreCase);
                return cmp != 0 ? cmp : x.CompareTo(y);
            }
        }
    }
}
