using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftSort
{
    public static class PinnedRecipeTracker
    {
        private static readonly HashSet<string> _pinnedRecipes = new HashSet<string>();
        private static readonly List<GameObject> _boundElements = new List<GameObject>();
        private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();
        private static string _currentCharacter = "";
        private static bool _initialized;

        public static bool HasAny => _pinnedRecipes.Count > 0;

        public static void EnsureLoaded()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            string character = player.GetPlayerName();
            if (character == _currentCharacter && _initialized) return;

            _currentCharacter = character;
            _pinnedRecipes.Clear();
            string file = GetSavePath(character);
            if (File.Exists(file))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string name = line.Trim();
                    if (name.Length > 0)
                        _pinnedRecipes.Add(NewRecipeTracker.NormalizeName(name));
                }
            }

            _initialized = true;
        }

        public static bool IsPinned(Recipe? recipe)
        {
            if (recipe == null) return false;
            EnsureLoaded();
            return _pinnedRecipes.Contains(NewRecipeTracker.NormalizeName(recipe.name));
        }

        internal static bool ToggleFromClick(Recipe recipe)
        {
            EnsureLoaded();
            string name = NewRecipeTracker.NormalizeName(recipe.name);
            bool pinned;
            if (_pinnedRecipes.Remove(name))
            {
                pinned = false;
            }
            else
            {
                _pinnedRecipes.Add(name);
                pinned = true;
            }

            Save();
            return pinned;
        }

        public static void BindElement(GameObject element, Recipe? recipe)
        {
            if (element == null) return;
            if (!_boundElements.Contains(element))
                _boundElements.Add(element);
            var handler = element.GetComponent<PinnedRecipeClickHandler>();
            if (handler == null)
                handler = element.AddComponent<PinnedRecipeClickHandler>();
            handler.Bind(recipe);
        }

        private static string GetSavePath(string character)
        {
            string safeName = string.Join("_", character.Split(_invalidChars));
            return Path.Combine(BepInEx.Paths.ConfigPath, "CraftSort", $"pinned_{safeName}.txt");
        }

        private static void Save()
        {
            try
            {
                string directory = Path.Combine(BepInEx.Paths.ConfigPath, "CraftSort");
                Directory.CreateDirectory(directory);
                File.WriteAllLines(GetSavePath(_currentCharacter), _pinnedRecipes);
            }
            catch (Exception ex)
            {
                CraftSortPlugin.Log($"[PinnedRecipeTracker] Save error: {ex.Message}");
            }
        }

        internal static void Reset()
        {
            for (int i = 0; i < _boundElements.Count; i++)
            {
                var element = _boundElements[i];
                if (element == null) continue;

                var handler = element.GetComponent<PinnedRecipeClickHandler>();
                if (handler != null)
                    UnityEngine.Object.DestroyImmediate(handler);

                var marker = element.transform.Find("icon/CraftSortPinnedMark");
                if (marker == null) marker = element.transform.Find("CraftSortPinnedMark");
                if (marker != null)
                    UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }
            _boundElements.Clear();

            _pinnedRecipes.Clear();
            _currentCharacter = "";
            _initialized = false;

        }
    }

    internal sealed class PinnedRecipeClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private Recipe? _recipe;
        private GameObject? _marker;

        internal void Bind(Recipe? recipe)
        {
            _recipe = recipe;
            UpdateMarker(PinnedRecipeTracker.IsPinned(recipe));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right
                || !CraftSortPlugin.Enabled || _recipe == null)
                return;

            bool pinned = PinnedRecipeTracker.ToggleFromClick(_recipe);
            UpdateMarker(pinned);
            Patch_UpdateRecipeList.RefreshPinnedOrder();
            eventData.Use();
        }

        private void UpdateMarker(bool pinned)
        {
            if (_marker == null)
            {
                Transform? icon = transform.Find("icon");
                Transform parent = icon != null ? icon : transform;
                var marker = parent.Find("CraftSortPinnedMark");
                if (marker == null && pinned)
                {
                    var markerObject = new GameObject("CraftSortPinnedMark");
                    markerObject.transform.SetParent(parent, false);
                    var rect = markerObject.AddComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(1f, -1f);
                    rect.sizeDelta = new Vector2(13f, 13f);

                    var background = markerObject.AddComponent<Image>();
                    background.color = new Color(0.05f, 0.42f, 0.5f, 0.95f);
                    background.raycastTarget = false;

                    var labelObject = new GameObject("Label");
                    labelObject.transform.SetParent(markerObject.transform, false);
                    var labelRect = labelObject.AddComponent<RectTransform>();
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                    var label = labelObject.AddComponent<Text>();
                    label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.text = "P";
                    label.fontSize = 9;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = Color.white;
                    label.raycastTarget = false;
                    _marker = markerObject;
                }
                else if (marker != null)
                {
                    _marker = marker.gameObject;
                }
            }

            if (_marker != null)
                _marker.SetActive(pinned);
        }
    }
}
