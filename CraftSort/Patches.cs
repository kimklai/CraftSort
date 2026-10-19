using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace CraftSort
{
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) })]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] {
        "com.maxsch.valheim.vnei",
        "org.bepinex.plugins.jewelcrafting",
        "Azumatt.Recycle_N_Reclaim",
        "Azumatt.AzuAntiArthriticCrafting",
        "com.sighsorry1029.InventorySlots",
        "shudnal.MyLittleUI",
        "goldenrevolver.SortedMenus",
        "aedenthorn.CraftingFilter",
        "com.MoistGravy.CraftingSearchBar"
    })]
    class Patch_UpdateRecipeList
    {
        private static FieldInfo? _availableRecipesField;
        private static PropertyInfo? _recipeProp;
        private static FieldInfo? _recipeField;
        private static PropertyInfo? _interfaceElementProp;
        private static FieldInfo? _interfaceElementField;
        private static object[] _reorderCache = System.Array.Empty<object>();

        [HarmonyPrepare]
        static bool Prepare()
        {
            var method = AccessTools.Method(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) });
            if (method == null)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: UpdateRecipeList not found — Transpiler disabled");
                return false;
            }
            return true;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var sortMethod = AccessTools.Method(typeof(Patch_UpdateRecipeList), nameof(SortAvailableRecipes));

            if (sortMethod == null)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: Transpiler cannot resolve sort method");
                return codes;
            }

            // Find the positioning for-loop at the end of UpdateRecipeList:
            //   ldc.i4.0 → stloc.s → br/br.s → ldarg.0 → ldfld m_availableRecipes
            int insertAt = -1;
            for (int i = 0; i < codes.Count - 4; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_0 &&
                    codes[i + 1].opcode == OpCodes.Stloc_S &&
                    (codes[i + 2].opcode == OpCodes.Br || codes[i + 2].opcode == OpCodes.Br_S) &&
                    codes[i + 3].opcode == OpCodes.Ldarg_0 &&
                    codes[i + 4].opcode == OpCodes.Ldfld &&
                    codes[i + 4].operand is FieldInfo fi && fi.Name == "m_availableRecipes")
                {
                    insertAt = i;
                }
            }

            if (insertAt < 0)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: positioning loop not found in UpdateRecipeList IL");
                return codes;
            }

            // Transfer labels so switch/br jumps land on our code, not skip over it
            var ldarg = new CodeInstruction(OpCodes.Ldarg_0);
            var call = new CodeInstruction(OpCodes.Call, sortMethod);
            ldarg.labels = codes[insertAt].labels;
            codes[insertAt].labels = new List<Label>();
            codes.Insert(insertAt, call);
            codes.Insert(insertAt, ldarg);

            CraftSortPlugin.Log("[CraftSort] Transpiler injected sort before positioning loop");
            return codes;
        }

        /// <summary>
        /// Called from injected IL right before vanilla positions recipe elements.
        /// Sorts m_availableRecipes in-place so vanilla positions them in our order.
        /// </summary>
        static void SortAvailableRecipes(InventoryGui gui)
        {
            if (!CraftSortPlugin.Enabled) return;
            if (gui == null) return;

            var list = GetAvailableRecipesList(gui);
            if (list == null || list.Count == 0) return;

            PinnedRecipeTracker.EnsureLoaded();

            if (SortLogic.CurrentFilter != WeaponFilter.None)
                FilterWeaponOnList(list);

            if (SortLogic.CurrentMode == SortMode.Eitr)
                FilterWithoutRecoveryOnList(list, SortMode.Eitr);

            if (SortLogic.CurrentMode == SortMode.None)
            {
                PrioritizePinnedOnList(list);
                return;
            }

            if (SortLogic.CurrentMode == SortMode.New)
            {
                FilterNewOnList(list);
                return;
            }

            if (list.Count < 2) return;

            int count = list.Count;
            SortLogic.EnsureCaches(count);

            if (SortLogic.CurrentMode == SortMode.Name)
                SortByNameOnList(list, count);
            else
                SortByValueOnList(list, count);
        }

        internal static IList? GetAvailableRecipesList(InventoryGui gui)
        {
            _availableRecipesField ??= AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
            return _availableRecipesField?.GetValue(gui) as IList;
        }

        internal static Recipe? GetRecipeFromPair(object pair)
        {
            if (_recipeProp == null && _recipeField == null)
            {
                var type = pair.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _recipeProp = type.GetProperty("Recipe", flags);
                if (_recipeProp == null)
                    _recipeField = type.GetField("Recipe", flags);
            }
            if (_recipeProp != null)
                return _recipeProp.GetValue(pair) as Recipe;
            if (_recipeField != null)
                return _recipeField.GetValue(pair) as Recipe;
            return null;
        }

        internal static GameObject? GetInterfaceElement(object pair)
        {
            if (_interfaceElementProp == null && _interfaceElementField == null)
            {
                var type = pair.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _interfaceElementProp = type.GetProperty("InterfaceElement", flags);
                if (_interfaceElementProp == null)
                    _interfaceElementField = type.GetField("InterfaceElement", flags);
            }
            if (_interfaceElementProp != null)
                return _interfaceElementProp.GetValue(pair) as GameObject;
            if (_interfaceElementField != null)
                return _interfaceElementField.GetValue(pair) as GameObject;
            return null;
        }

        private static void FilterWeaponOnList(IList list)
        {
            int originalCount = list.Count;
            int writeIdx = 0;
            for (int i = 0; i < originalCount; i++)
            {
                var recipe = GetRecipeFromPair(list[i]);
                if (SortLogic.PassesFilter(recipe))
                {
                    if (writeIdx != i)
                        list[writeIdx] = list[i];
                    writeIdx++;
                }
                else
                {
                    var element = GetInterfaceElement(list[i]);
                    if (element != null)
                        element.SetActive(false);
                }
            }

            while (list.Count > writeIdx)
                list.RemoveAt(list.Count - 1);
        }

        private static void FilterNewOnList(IList list)
        {
            NewRecipeTracker.EnsureLoaded();

            int originalCount = list.Count;
            int writeIdx = 0;
            for (int i = 0; i < originalCount; i++)
            {
                var recipe = GetRecipeFromPair(list[i]);
                if (NewRecipeTracker.IsNew(recipe))
                {
                    if (writeIdx != i)
                        list[writeIdx] = list[i];
                    writeIdx++;
                }
                else
                {
                    var element = GetInterfaceElement(list[i]);
                    if (element != null)
                        element.SetActive(false);
                }
            }

            while (list.Count > writeIdx)
                list.RemoveAt(list.Count - 1);
        }

        private static void FilterWithoutRecoveryOnList(IList list, SortMode mode)
        {
            int originalCount = list.Count;
            int writeIndex = 0;
            for (int i = 0; i < originalCount; i++)
            {
                var pair = list[i];
                if (SortLogic.ProvidesRecovery(GetRecipeFromPair(pair), mode))
                {
                    if (writeIndex != i)
                        list[writeIndex] = pair;
                    writeIndex++;
                }
                else
                {
                    var element = GetInterfaceElement(pair);
                    if (element != null)
                        element.SetActive(false);
                }
            }

            while (list.Count > writeIndex)
                list.RemoveAt(list.Count - 1);
        }

        private static void SortByValueOnList(IList list, int count)
        {
            var valueCache = SortLogic.GetValueCache();
            var indexCache = SortLogic.GetIndexCache();
            var pinnedCache = SortLogic.GetPinnedCache();

            for (int i = 0; i < count; i++)
            {
                var recipe = GetRecipeFromPair(list[i]);
                valueCache[i] = SortLogic.GetSortValue(recipe);
                indexCache[i] = i;
                pinnedCache[i] = SortLogic.CurrentMode == SortMode.None && PinnedRecipeTracker.IsPinned(recipe);
            }

            System.Array.Sort(indexCache, 0, count, SortLogic.ValueComparer);
            ApplyListOrder(list, count, indexCache);
        }

        private static void SortByNameOnList(IList list, int count)
        {
            var nameCache = SortLogic.GetNameCache();
            var indexCache = SortLogic.GetIndexCache();
            var pinnedCache = SortLogic.GetPinnedCache();
            var loc = Localization.instance;

            for (int i = 0; i < count; i++)
            {
                var recipe = GetRecipeFromPair(list[i]);
                pinnedCache[i] = SortLogic.CurrentMode == SortMode.None && PinnedRecipeTracker.IsPinned(recipe);
                string? key = recipe?.m_item?.m_itemData?.m_shared?.m_name;
                indexCache[i] = i;
                string name = "";
                if (key != null && loc != null)
                {
                    string locName = loc.Localize(key);
                    if (locName != null) name = locName;
                }
                else if (key != null)
                {
                    name = key;
                }
                nameCache[i] = name;
            }

            System.Array.Sort(indexCache, 0, count, SortLogic.NameComparer);
            ApplyListOrder(list, count, indexCache);
        }

        private static void ApplyListOrder(IList list, int count, int[] indexCache)
        {
            if (_reorderCache.Length < count)
                _reorderCache = new object[count];

            for (int i = 0; i < count; i++)
                _reorderCache[i] = list[indexCache[i]];
            for (int i = 0; i < count; i++)
                list[i] = _reorderCache[i];
        }

        private static void PrioritizePinnedOnList(IList list)
        {
            int count = list.Count;
            if (count < 2 || !PinnedRecipeTracker.HasAny) return;

            if (_reorderCache.Length < count)
                _reorderCache = new object[count];

            int writeIndex = 0;
            for (int i = 0; i < count; i++)
            {
                if (PinnedRecipeTracker.IsPinned(GetRecipeFromPair(list[i])))
                    _reorderCache[writeIndex++] = list[i];
            }
            for (int i = 0; i < count; i++)
            {
                if (!PinnedRecipeTracker.IsPinned(GetRecipeFromPair(list[i])))
                    _reorderCache[writeIndex++] = list[i];
            }
            for (int i = 0; i < count; i++)
                list[i] = _reorderCache[i];
        }

        internal static void RefreshPinnedOrder()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;
            var list = GetAvailableRecipesList(gui);
            if (list == null || list.Count < 2) return;

            int count = list.Count;
            var positions = new Vector2[count];
            var hasPosition = new bool[count];
            for (int i = 0; i < count; i++)
            {
                var element = GetInterfaceElement(list[i]);
                var rect = element != null ? element.GetComponent<RectTransform>() : null;
                if (rect == null) continue;
                positions[i] = rect.anchoredPosition;
                hasPosition[i] = true;
            }

            SortAvailableRecipes(gui);
            int newCount = System.Math.Min(count, list.Count);
            for (int i = 0; i < newCount; i++)
            {
                if (!hasPosition[i]) continue;
                var element = GetInterfaceElement(list[i]);
                var rect = element != null ? element.GetComponent<RectTransform>() : null;
                if (rect != null)
                    rect.anchoredPosition = positions[i];
            }
        }
    }

    /// <summary>
    /// AAA Crafting compatibility: sorts the FULL cached recipe list before pagination slices it.
    /// Runs AFTER AAA Crafting's Prefix (which populates RecipeListPerfCache.CraftSortedFiltered).
    /// Without this, our Transpiler only sorts the current page (13 items).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) })]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] { "Azumatt.AzuAntiArthriticCrafting" })]
    class Patch_UpdateRecipeList_AAACrafting
    {
        private static bool _aaaChecked;
        private static FieldInfo? _cacheField;
        private static FieldInfo? _pageField;
        private static MethodInfo? _getPerPageMethod;
        private static readonly List<Recipe> _filteredCache = new List<Recipe>();
        private static readonly List<Recipe> _emptyList = new List<Recipe>();

        [HarmonyPrepare]
        static bool Prepare()
        {
            return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("Azumatt.AzuAntiArthriticCrafting");
        }

        static void Prefix(ref List<Recipe> recipes)
        {
            if (!CraftSortPlugin.Enabled) return;
            PinnedRecipeTracker.EnsureLoaded();
            if (SortLogic.CurrentMode == SortMode.None && SortLogic.CurrentFilter == WeaponFilter.None
                && !PinnedRecipeTracker.HasAny) return;

            if (!_aaaChecked)
            {
                _aaaChecked = true;
                ResolveAAATypes();
            }

            if (_cacheField == null) return;

            var fullList = _cacheField.GetValue(null) as List<Recipe>;
            if (fullList == null || fullList.Count == 0) return;

            _filteredCache.Clear();
            bool filterEitr = SortLogic.CurrentMode == SortMode.Eitr;
            for (int i = 0; i < fullList.Count; i++)
            {
                var recipe = fullList[i];
                if (SortLogic.CurrentFilter != WeaponFilter.None && !SortLogic.PassesFilter(recipe))
                    continue;
                if (filterEitr && !SortLogic.ProvidesRecovery(recipe, SortMode.Eitr))
                    continue;
                _filteredCache.Add(recipe);
            }

            if (SortLogic.CurrentMode == SortMode.New)
            {
                NewRecipeTracker.EnsureLoaded();
                for (int i = _filteredCache.Count - 1; i >= 0; i--)
                {
                    if (!NewRecipeTracker.IsNew(_filteredCache[i]))
                        _filteredCache.RemoveAt(i);
                }
            }
            SortLogic.SortRecipeList(_filteredCache);

            if (_filteredCache.Count == 0) { recipes = _emptyList; return; }

            if (_pageField == null || _getPerPageMethod == null)
            {
                recipes = new List<Recipe>(_filteredCache);
                return;
            }

            int page = (int)_pageField.GetValue(null);
            int perPage = (int)_getPerPageMethod.Invoke(null, null);
            if (perPage < 1) perPage = 13;

            int offset = (page - 1) * perPage;
            int count = System.Math.Min(perPage, _filteredCache.Count - offset);
            if (count <= 0) { recipes = _emptyList; return; }
            recipes = _filteredCache.GetRange(offset, count);
        }

        private static void ResolveAAATypes()
        {
            try
            {
                var cacheType = FindType("AzuAntiArthriticCrafting.Patches.RecipeListPerfCache");
                if (cacheType != null)
                    _cacheField = cacheType.GetField("CraftSortedFiltered",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                var paginatorType = FindType("AzuAntiArthriticCrafting.Patches.PaginatorPatches");
                if (paginatorType != null)
                    _pageField = paginatorType.GetField("CraftingWindowPage",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                var utilsType = FindType("AzuAntiArthriticCrafting.Utilities.Utilities");
                if (utilsType != null)
                    _getPerPageMethod = utilsType.GetMethod("GetPerPage",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                CraftSortPlugin.Log($"[CraftSort] AAA Crafting compat: cache={_cacheField != null}, page={_pageField != null}, perPage={_getPerPageMethod != null}");
            }
            catch (System.Exception ex)
            {
                CraftSortPlugin.Log($"[CraftSort] AAA Crafting compat error: {ex.Message}");
            }
        }

        private static System.Type? FindType(string fullName)
        {
            var type = System.Type.GetType($"{fullName}, AzuAntiArthriticCrafting");
            if (type != null) return type;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] {
        "com.maxsch.valheim.vnei",
        "org.bepinex.plugins.jewelcrafting",
        "Azumatt.Recycle_N_Reclaim",
        "Azumatt.AzuAntiArthriticCrafting",
        "com.sighsorry1029.InventorySlots",
        "shudnal.MyLittleUI",
        "goldenrevolver.SortedMenus",
        "aedenthorn.CraftingFilter",
        "com.MoistGravy.CraftingSearchBar"
    })]
    class Patch_UpdateCraftingPanel
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            TabUI.EnsureTabsExist(__instance);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    class Patch_InventoryGui_Hide
    {
        static void Postfix()
        {
            TabUI.Reset();
            if (!CraftSortPlugin.RememberLastMode)
            {
                SortLogic.CurrentMode = System.Enum.TryParse<SortMode>(
                    CraftSortPlugin.DefaultSortMode, true, out var mode)
                    ? mode : SortMode.None;
                SortLogic.CurrentFilter = WeaponFilter.None;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) })]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] {
        "com.maxsch.valheim.vnei",
        "org.bepinex.plugins.jewelcrafting",
        "Azumatt.Recycle_N_Reclaim",
        "Azumatt.AzuAntiArthriticCrafting",
        "com.sighsorry1029.InventorySlots",
        "shudnal.MyLittleUI",
        "goldenrevolver.SortedMenus",
        "aedenthorn.CraftingFilter",
        "com.MoistGravy.CraftingSearchBar"
    })]
    class Patch_UpdateRecipeList_NewDots
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            NewRecipeTracker.EnsureLoaded();
            PinnedRecipeTracker.EnsureLoaded();

            var list = Patch_UpdateRecipeList.GetAvailableRecipesList(__instance);
            if (list == null) return;

            int newCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var recipe = Patch_UpdateRecipeList.GetRecipeFromPair(list[i]);
                var element = Patch_UpdateRecipeList.GetInterfaceElement(list[i]);
                if (element == null) continue;

                PinnedRecipeTracker.BindElement(element, recipe);
                bool isNew = NewRecipeTracker.IsNew(recipe);
                NewRecipeTracker.SetDot(element, isNew);
                if (isNew) newCount++;
            }

            NewRecipeTracker.SetNewCount(newCount);
            TabUI.UpdateNewCount(newCount);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnSelectedRecipe")]
    class Patch_OnSelectedRecipe_Viewed
    {
        private static FieldInfo? _selectedRecipeField;
        private static bool _fieldLogged;

        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            NewRecipeTracker.EnsureLoaded();

            _selectedRecipeField ??= AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
            if (_selectedRecipeField == null)
            {
                if (!_fieldLogged)
                {
                    _fieldLogged = true;
                    CraftSortPlugin.Log("[CraftSort] WARNING: m_selectedRecipe field not found — viewed tracking disabled");
                }
                return;
            }

            object selected = _selectedRecipeField.GetValue(__instance);
            if (selected == null) return;

            var recipe = Patch_UpdateRecipeList.GetRecipeFromPair(selected);
            if (recipe == null) return;

            if (!NewRecipeTracker.IsNew(recipe)) return;

            NewRecipeTracker.MarkViewed(recipe);

            var element = Patch_UpdateRecipeList.GetInterfaceElement(selected);
            if (element != null)
                NewRecipeTracker.RemoveDot(element);

            TabUI.UpdateNewCount(NewRecipeTracker.NewCount);
        }
    }

    [HarmonyPatch(typeof(Player), "ResetCharacterKnownItems")]
    class Patch_Player_ResetKnown
    {
        static void Postfix()
        {
            NewRecipeTracker.ClearAll();

            var gui = InventoryGui.instance;
            if (gui != null)
            {
                var list = Patch_UpdateRecipeList.GetAvailableRecipesList(gui);
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var element = Patch_UpdateRecipeList.GetInterfaceElement(list[i]);
                        if (element != null)
                            NewRecipeTracker.RemoveDot(element);
                    }
                }
            }

            NewRecipeTracker.SetNewCount(0);
            TabUI.UpdateNewCount(0);
        }
    }
}
