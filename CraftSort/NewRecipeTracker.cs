using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CraftSort
{
    public static class NewRecipeTracker
    {
        private static readonly HashSet<string> _viewedRecipes = new HashSet<string>();
        private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();
        private static string _currentCharacter = "";
        private static bool _initialized;
        private static Sprite? _dotSprite;
        private static int _newCount;

        public static int NewCount => _newCount;

        internal static string NormalizeName(string name)
        {
            if (name.EndsWith("(Clone)"))
                return name.Substring(0, name.Length - 7);
            return name;
        }

        public static void EnsureLoaded()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            string charName = player.GetPlayerName();
            if (charName == _currentCharacter && _initialized) return;

            _currentCharacter = charName;
            _viewedRecipes.Clear();

            string file = GetSavePath(charName);
            if (File.Exists(file))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0)
                        _viewedRecipes.Add(NormalizeName(trimmed));
                }
            }
            else
            {
                var knownField = typeof(Player).GetField("m_knownRecipes",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (knownField?.GetValue(player) is HashSet<string> known)
                {
                    foreach (string r in known)
                        _viewedRecipes.Add(NormalizeName(r));
                }
                Save();
                CraftSortPlugin.Log($"[NewRecipeTracker] First run for '{charName}' — snapshotted {_viewedRecipes.Count} known recipes as viewed");
            }

            _initialized = true;
        }

        public static bool IsNew(Recipe? recipe)
        {
            if (recipe == null) return false;
            return !_viewedRecipes.Contains(NormalizeName(recipe.name));
        }

        public static void MarkViewed(Recipe? recipe)
        {
            if (recipe == null) return;
            string name = NormalizeName(recipe.name);
            if (_viewedRecipes.Add(name))
            {
                Save();
                if (_newCount > 0) _newCount--;
            }
        }

        public static void MarkAllViewed()
        {
            EnsureLoaded();

            var player = Player.m_localPlayer;
            if (player != null)
            {
                var knownField = typeof(Player).GetField("m_knownRecipes",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (knownField?.GetValue(player) is HashSet<string> known)
                {
                    foreach (string r in known)
                        _viewedRecipes.Add(NormalizeName(r));
                }
            }

            var gui = InventoryGui.instance;
            if (gui != null)
            {
                var list = Patch_UpdateRecipeList.GetAvailableRecipesList(gui);
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var recipe = Patch_UpdateRecipeList.GetRecipeFromPair(list[i]);
                        if (recipe != null)
                            _viewedRecipes.Add(NormalizeName(recipe.name));
                    }
                }
            }

            Save();
            _newCount = 0;
        }

        public static void ClearAll()
        {
            _viewedRecipes.Clear();
            _newCount = 0;

            var player = Player.m_localPlayer;
            if (player != null)
            {
                _currentCharacter = player.GetPlayerName();
                _initialized = true;

                string file = GetSavePath(_currentCharacter);
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        public static void SetNewCount(int count) => _newCount = count;

        public static void SetDot(GameObject element, bool show)
        {
            var existing = element.transform.Find("icon/CraftSortNewDot");
            if (show)
            {
                if (existing != null)
                {
                    existing.gameObject.SetActive(true);
                    return;
                }

                var icon = element.transform.Find("icon");
                if (icon == null) return;

                _dotSprite ??= CreateDotSprite();

                var dotGo = new GameObject("CraftSortNewDot");
                dotGo.transform.SetParent(icon, false);

                var rt = dotGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-2f, -2f);
                rt.sizeDelta = new Vector2(10f, 10f);

                var img = dotGo.AddComponent<Image>();
                img.sprite = _dotSprite;
                img.color = new Color(1f, 0.82f, 0.15f, 1f);
                img.raycastTarget = false;
            }
            else if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        public static void RemoveDot(GameObject element)
        {
            var existing = element.transform.Find("icon/CraftSortNewDot");
            if (existing != null)
                existing.gameObject.SetActive(false);
        }

        private static Sprite CreateDotSprite()
        {
            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size / 2f;
            float radius = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static string GetSavePath(string charName)
        {
            string safeName = string.Join("_", charName.Split(_invalidChars));
            return Path.Combine(BepInEx.Paths.ConfigPath, "CraftSort", $"viewed_{safeName}.txt");
        }

        private static void Save()
        {
            try
            {
                string dir = Path.Combine(BepInEx.Paths.ConfigPath, "CraftSort");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(GetSavePath(_currentCharacter), _viewedRecipes);
            }
            catch (Exception ex)
            {
                CraftSortPlugin.Log($"[NRT] Save error: {ex.Message}");
            }
        }

        public static void Reset()
        {
            var gui = InventoryGui.instance;
            if (gui != null)
            {
                var list = Patch_UpdateRecipeList.GetAvailableRecipesList(gui);
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var element = Patch_UpdateRecipeList.GetInterfaceElement(list[i]);
                        var dot = element != null
                            ? element.transform.Find("icon/CraftSortNewDot")
                            : null;
                        if (dot != null)
                            UnityEngine.Object.Destroy(dot.gameObject);
                    }
                }
            }

            if (_dotSprite != null)
            {
                var texture = _dotSprite.texture;
                UnityEngine.Object.Destroy(_dotSprite);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
                _dotSprite = null;
            }

            _initialized = false;
            _currentCharacter = "";
            _viewedRecipes.Clear();
            _newCount = 0;
        }
    }
}
