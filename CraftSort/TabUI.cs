using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftSort
{
    public sealed class ButtonDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _container = null!;
        private RectTransform _parent = null!;
        private Vector2 _startPos;
        private Vector2 _startPointer;
        private bool _dragging;
        private System.Action<Vector2>? _onPositionSaved;

        public void SetPositionCallback(System.Action<Vector2> callback)
        {
            _onPositionSaved = callback;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!TabUI.EditMode) return;
            _dragging = true;
            _container = (RectTransform)transform.parent;
            _parent = (RectTransform)_container.parent;
            _startPos = _container.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent, eventData.position, eventData.pressEventCamera, out _startPointer);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _container == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent, eventData.position, eventData.pressEventCamera, out var local);
            _container.anchoredPosition = _startPos + (local - _startPointer);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || _container == null) return;
            _dragging = false;
            _onPositionSaved?.Invoke(_container.anchoredPosition);
        }
    }

    public sealed class ResizeHandleController : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private RectTransform _target = null!;
        private GridLayoutGroup? _grid;
        private Image? _bgImg;
        private Image? _iconImg;
        private System.Action<float>? _onWidthSaved;
        private Vector2 _lastPointer;
        private Vector2 _originalPivot;
        private static readonly Color BgInvisible = new Color(0.1f, 0.08f, 0.05f, 0f);
        private static readonly Color BgHover     = new Color(0.1f, 0.08f, 0.05f, 0.7f);
        private static readonly Color BgActive    = new Color(0.1f, 0.08f, 0.05f, 0.9f);
        private static readonly Color IconInvisible = new Color(1f, 0.82f, 0.15f, 0f);
        private static readonly Color IconHover     = new Color(1f, 0.82f, 0.15f, 0.6f);
        private static readonly Color IconActive    = new Color(1f, 0.82f, 0.15f, 1.0f);

        public void Setup(RectTransform target, Image bgImg, Image iconImg, System.Action<float>? onWidthSaved = null)
        {
            _target = target;
            _grid = target.GetComponent<GridLayoutGroup>();
            _bgImg = bgImg;
            _iconImg = iconImg;
            _onWidthSaved = onWidthSaved;
        }

        public void SetVisuals(bool editMode)
        {
            if (_bgImg != null)
                _bgImg.color = editMode ? BgActive : BgInvisible;
            if (_iconImg != null)
                _iconImg.color = editMode ? IconActive : IconInvisible;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_bgImg != null)
                _bgImg.color = TabUI.EditMode ? BgActive : BgHover;
            if (_iconImg != null)
                _iconImg.color = TabUI.EditMode ? IconActive : IconHover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!TabUI.EditMode)
            {
                if (_bgImg != null) _bgImg.color = BgInvisible;
                if (_iconImg != null) _iconImg.color = IconInvisible;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                TabUI.ToggleEditMode();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_target == null) return;
            _lastPointer = eventData.position;
            _originalPivot = _target.pivot;
            SetPivot(_target, new Vector2(0f, 1f));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_target == null) return;
            Vector2 delta = eventData.position - _lastPointer;
            float newWidth = Mathf.Clamp(_target.sizeDelta.x + delta.x, 54f, 800f);
            _target.sizeDelta = new Vector2(newWidth, _target.sizeDelta.y);
            _lastPointer = eventData.position;

            int cols = 1;
            if (_grid != null)
            {
                float cell = _grid.cellSize.x;
                float spacing = _grid.spacing.x;
                cols = Mathf.Max(1, Mathf.FloorToInt((newWidth + spacing) / (cell + spacing)));
                _grid.constraintCount = cols;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_target == null) return;
            SetPivot(_target, _originalPivot);

            float snapped = _target.sizeDelta.x;
            int cols = 1;
            if (_grid != null)
            {
                float cell = _grid.cellSize.x;
                float spacing = _grid.spacing.x;
                float cellPlus = cell + spacing;
                cols = Mathf.Max(1, Mathf.RoundToInt(_target.sizeDelta.x / cellPlus));
                snapped = Mathf.Clamp(cols * cellPlus - spacing, cell, 800f);
                _target.sizeDelta = new Vector2(snapped, _target.sizeDelta.y);
                _grid.constraintCount = cols;
            }

            _onWidthSaved?.Invoke(snapped);
        }

        private static void SetPivot(RectTransform rt, Vector2 pivot)
        {
            Vector3 delta = rt.pivot - pivot;
            delta.Scale(rt.rect.size);
            delta.Scale(rt.localScale);
            delta = rt.rotation * delta;
            rt.pivot = pivot;
            rt.localPosition -= delta;
        }
    }

    public static class TabUI
    {
        private static GameObject? _foodContainer;
        private static GameObject? _combatContainer;
        private static readonly List<(Button btn, Image img, Image border, SortMode mode)> _sortButtons
            = new List<(Button, Image, Image, SortMode)>();
        private static readonly List<(Button btn, Image img, Image border, WeaponFilter filter)> _filterButtons
            = new List<(Button, Image, Image, WeaponFilter)>();
        private static readonly List<Text> _newTabTexts = new List<Text>();
        private static MethodInfo? _updateCraftingPanel;
        private static readonly object[] _updatePanelArgs = { false };
        private static Sprite? _roundedSprite;
        private static Sprite? _borderSprite;
        private static Font? _cachedFont;
        private static SortMode _lastActiveMode = (SortMode)(-1);
        private static WeaponFilter _lastActiveFilter = (WeaponFilter)(-1);
        private static string? _cachedStationKey;
        private static bool _cachedIsFoodStation;

        private static BepInEx.Configuration.ConfigEntry<float>? _cfgFoodPosX;
        private static BepInEx.Configuration.ConfigEntry<float>? _cfgFoodPosY;
        private static BepInEx.Configuration.ConfigEntry<float>? _cfgFoodWidth;
        private static BepInEx.Configuration.ConfigEntry<float>? _cfgCombatPosX;
        private static BepInEx.Configuration.ConfigEntry<float>? _cfgCombatPosY;
        private static BepInEx.Configuration.ConfigEntry<float>? _cfgCombatWidth;

        private static readonly Color NormalBg   = new Color(0.08f, 0.05f, 0.02f, 0.95f);
        private static readonly Color HoverBg    = new Color(0.18f, 0.13f, 0.05f, 0.97f);
        private static readonly Color ActiveBg   = new Color(0.15f, 0.10f, 0.04f, 0.97f);
        private static readonly Color BorderCol  = new Color(1.0f, 0.82f, 0.15f, 1.0f);
        private static readonly Color FilterBorderCol = new Color(0.3f, 0.7f, 1.0f, 1.0f);
        private static readonly Color CleanBg    = new Color(0.12f, 0.04f, 0.04f, 0.95f);

        private static bool _editMode;
        private static ResizeHandleController? _foodResizeCtrl;
        private static ResizeHandleController? _combatResizeCtrl;

        public static bool EditMode => _editMode;

        public static void ToggleEditMode()
        {
            _editMode = !_editMode;
            UpdateResizeHandleVisuals();
        }

        private static void UpdateResizeHandleVisuals()
        {
            if (_foodResizeCtrl != null)
                _foodResizeCtrl.SetVisuals(_editMode);
            if (_combatResizeCtrl != null)
                _combatResizeCtrl.SetVisuals(_editMode);
        }

        private const float ButtonSize = 50f;
        private const float GridSpacingX = 4f;
        private const float GridSpacingY = 2f;
        private const float DefaultPosX = -9f;
        private const float DefaultPosY = -200f;
        private const float DefaultCombatWidth = 104f; // 2 × 50 + 4
        private const int CornerRadius = 7;
        private const int BorderThickness = 5;
        private const float IconSize = 34f;
        private const float ResizeHandleSize = 20f;

        public static void EnsureTabsExist(InventoryGui gui)
        {
            if (gui == null || gui.m_crafting == null)
                return;

            if (_foodContainer != null && _combatContainer != null)
            {
                UpdateButtonStates();
                UpdateGroupVisibility();
                UpdateNewCount(NewRecipeTracker.NewCount);
                return;
            }

            CreateTabs(gui);
        }

        private static void EnsureConfig()
        {
            if (_cfgFoodPosX != null) return;
            var cfg = CraftSortPlugin.Instance.Config;
            _cfgFoodPosX = cfg.Bind("UI", "FoodPosX", DefaultPosX, "Food panel X offset");
            _cfgFoodPosY = cfg.Bind("UI", "FoodPosY", DefaultPosY, "Food panel Y offset");
            _cfgFoodWidth = cfg.Bind("UI", "FoodWidth", ButtonSize, "Food panel width");
            _cfgCombatPosX = cfg.Bind("UI", "CombatPosX", DefaultPosX, "Combat panel X offset");
            _cfgCombatPosY = cfg.Bind("UI", "CombatPosY", DefaultPosY, "Combat panel Y offset");
            _cfgCombatWidth = cfg.Bind("UI", "CombatWidth", DefaultCombatWidth, "Combat panel width");
        }

        internal static void SaveFoodPosition(Vector2 pos)
        {
            EnsureConfig();
            _cfgFoodPosX!.Value = pos.x;
            _cfgFoodPosY!.Value = pos.y;
        }

        internal static void SaveCombatPosition(Vector2 pos)
        {
            EnsureConfig();
            _cfgCombatPosX!.Value = pos.x;
            _cfgCombatPosY!.Value = pos.y;
        }

        internal static void SaveFoodWidth(float width)
        {
            EnsureConfig();
            _cfgFoodWidth!.Value = width;
        }

        internal static void SaveCombatWidth(float width)
        {
            EnsureConfig();
            _cfgCombatWidth!.Value = width;
        }

        private static void CreateTabs(InventoryGui gui)
        {
            var panel = gui.m_crafting;
            EnsureConfig();

            _roundedSprite = CreateRoundedSprite(32, 32, CornerRadius);
            _borderSprite = CreateRoundedBorderSprite(32, 32, CornerRadius, BorderThickness);

            _cachedFont = null;
            if (InventoryGui.instance != null)
            {
                var existingText = InventoryGui.instance.GetComponentInChildren<Text>();
                if (existingText != null)
                    _cachedFont = existingText.font;
            }
            _cachedFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");

            float foodPosX = _cfgFoodPosX!.Value;
            float foodPosY = _cfgFoodPosY!.Value;
            float foodWidth = _cfgFoodWidth!.Value;
            float combatPosX = _cfgCombatPosX!.Value;
            float combatPosY = _cfgCombatPosY!.Value;
            float combatWidth = _cfgCombatWidth!.Value;

            // ── Food container ──
            _foodContainer = new GameObject("CraftSortFoodTabs");
            _foodContainer.transform.SetParent(panel, false);

            var foodRt = _foodContainer.AddComponent<RectTransform>();
            foodRt.anchorMin = new Vector2(0f, 1f);
            foodRt.anchorMax = new Vector2(0f, 1f);
            foodRt.pivot = new Vector2(1f, 1f);
            foodRt.anchoredPosition = new Vector2(foodPosX, foodPosY);
            foodRt.sizeDelta = new Vector2(foodWidth, 0f);

            var foodGrid = _foodContainer.AddComponent<GridLayoutGroup>();
            foodGrid.cellSize = new Vector2(ButtonSize, ButtonSize);
            foodGrid.spacing = new Vector2(GridSpacingX, GridSpacingY);
            foodGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            foodGrid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((foodWidth + GridSpacingX) / (ButtonSize + GridSpacingX)));
            foodGrid.childAlignment = TextAnchor.UpperLeft;
            foodGrid.padding = new RectOffset(0, 0, 0, (int)ResizeHandleSize);

            var foodFitter = _foodContainer.AddComponent<ContentSizeFitter>();
            foodFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _foodResizeCtrl = AddResizeHandle(_foodContainer, foodRt, SaveFoodWidth);

            // ── Combat container ──
            _combatContainer = new GameObject("CraftSortCombatTabs");
            _combatContainer.transform.SetParent(panel, false);

            var combatRt = _combatContainer.AddComponent<RectTransform>();
            combatRt.anchorMin = new Vector2(0f, 1f);
            combatRt.anchorMax = new Vector2(0f, 1f);
            combatRt.pivot = new Vector2(1f, 1f);
            combatRt.anchoredPosition = new Vector2(combatPosX, combatPosY);
            combatRt.sizeDelta = new Vector2(combatWidth, 0f);

            var combatGrid = _combatContainer.AddComponent<GridLayoutGroup>();
            combatGrid.cellSize = new Vector2(ButtonSize, ButtonSize);
            combatGrid.spacing = new Vector2(GridSpacingX, GridSpacingY);
            combatGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            combatGrid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((combatWidth + GridSpacingX) / (ButtonSize + GridSpacingX)));
            combatGrid.childAlignment = TextAnchor.UpperLeft;
            combatGrid.padding = new RectOffset(0, 0, 0, (int)ResizeHandleSize);

            var combatFitter = _combatContainer.AddComponent<ContentSizeFitter>();
            combatFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _combatResizeCtrl = AddResizeHandle(_combatContainer, combatRt, SaveCombatWidth);

            // ── Food buttons ──
            CreateSortButton("All", SortMode.None, "Food_All", _foodContainer.transform);
            CreateSortButton("HP", SortMode.Health, "Food_HP", _foodContainer.transform);
            CreateSortButton("Stam", SortMode.Stamina, "Food_Stamina", _foodContainer.transform);
            CreateSortButton("Eitr", SortMode.Eitr, "Food_Eitr", _foodContainer.transform);
            CreateSortButton("A\u2192Z", SortMode.Name, "Food_AZ", _foodContainer.transform);
            CreateSortButton("New", SortMode.New, "Food_New", _foodContainer.transform);
            CreateCleanButton("Food_Clean", _foodContainer.transform);

            // ── Combat buttons ──
            CreateSortButton("All", SortMode.None, "Combat_All", _combatContainer.transform);
            CreateSortButton("Armor", SortMode.Armor, "Combat_Armor", _combatContainer.transform);
            CreateSortButton("Block", SortMode.Block, "Combat_Block", _combatContainer.transform);
            CreateSortButton("Slash", SortMode.SlashDmg, "Combat_Slash", _combatContainer.transform);
            CreateSortButton("Pierce", SortMode.PierceDmg, "Combat_Pierce", _combatContainer.transform);
            CreateSortButton("Blunt", SortMode.BluntDmg, "Combat_Blunt", _combatContainer.transform);
            CreateSortButton("Fire", SortMode.FireDmg, "Combat_Fire", _combatContainer.transform);
            CreateSortButton("Frost", SortMode.FrostDmg, "Combat_Frost", _combatContainer.transform);
            CreateSortButton("Ltng", SortMode.LightningDmg, "Combat_Lightning", _combatContainer.transform);
            CreateSortButton("Psn", SortMode.PoisonDmg, "Combat_Poison", _combatContainer.transform);
            CreateSortButton("Sprt", SortMode.SpiritDmg, "Combat_Spirit", _combatContainer.transform);
            CreateFilterButton("1H", WeaponFilter.OneHanded, "Combat_1H", _combatContainer.transform);
            CreateFilterButton("2H", WeaponFilter.TwoHanded, "Combat_2H", _combatContainer.transform);
            CreateSortButton("A\u2192Z", SortMode.Name, "Combat_AZ", _combatContainer.transform);
            CreateSortButton("New", SortMode.New, "Combat_New", _combatContainer.transform);
            CreateCleanButton("Combat_Clean", _combatContainer.transform);

            UpdateNewCount(NewRecipeTracker.NewCount);
            UpdateGroupVisibility();
            UpdateButtonStates();
        }

        private static ResizeHandleController AddResizeHandle(GameObject container, RectTransform targetRt, System.Action<float>? onWidthSaved = null)
        {
            var handleGo = new GameObject("ResizeHandle");
            handleGo.transform.SetParent(container.transform, false);

            var rt = handleGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ResizeHandleSize, ResizeHandleSize);

            var bgImg = handleGo.AddComponent<Image>();
            bgImg.sprite = _roundedSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.1f, 0.08f, 0.05f, 0f);
            bgImg.raycastTarget = true;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(handleGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(ResizeHandleSize - 6f, ResizeHandleSize - 6f);
            iconRt.anchoredPosition = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            var resizeSprite = IconFactory.GetIconByKey("resize");
            if (resizeSprite != null)
            {
                iconImg.sprite = resizeSprite;
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
            }
            iconImg.color = new Color(1f, 0.82f, 0.15f, 0f);
            iconImg.raycastTarget = false;

            var le = handleGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var ctrl = handleGo.AddComponent<ResizeHandleController>();
            ctrl.Setup(targetRt, bgImg, iconImg, onWidthSaved);
            return ctrl;
        }

        private static void CreateSortButton(string label, SortMode mode, string configKey, Transform parent)
        {
            if (!CraftSortPlugin.IsButtonEnabled(configKey)) return;

            var (btn, img, borderImg) = CreateButtonBase(label, mode, configKey, parent);

            var capturedMode = mode;
            btn.onClick.AddListener(() =>
            {
                SortLogic.CurrentMode = SortLogic.CurrentMode == capturedMode
                    ? SortMode.None
                    : capturedMode;
                UpdateButtonStates();
                InvokeUpdateCraftingPanel();
            });

            _sortButtons.Add((btn, img, borderImg, mode));
        }

        private static void CreateFilterButton(string label, WeaponFilter filter, string configKey, Transform parent)
        {
            if (!CraftSortPlugin.IsButtonEnabled(configKey)) return;

            string iconKey = filter == WeaponFilter.OneHanded ? "1h" : "2h";
            var (btn, img, borderImg) = CreateButtonBase(label, SortMode.None, configKey, parent, iconKey);
            borderImg.color = FilterBorderCol;

            var capturedFilter = filter;
            btn.onClick.AddListener(() =>
            {
                SortLogic.CurrentFilter = SortLogic.CurrentFilter == capturedFilter
                    ? WeaponFilter.None
                    : capturedFilter;
                UpdateButtonStates();
                InvokeUpdateCraftingPanel();
            });

            _filterButtons.Add((btn, img, borderImg, filter));
        }

        private static void CreateCleanButton(string configKey, Transform parent)
        {
            if (!CraftSortPlugin.IsButtonEnabled(configKey)) return;

            var go = new GameObject("SortTab_Clean");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = ButtonSize;
            le.preferredHeight = ButtonSize;

            var img = go.AddComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = CleanBg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = CleanBg;
            colors.highlightedColor = new Color(0.22f, 0.08f, 0.08f, 0.97f);
            colors.pressedColor = new Color(0.28f, 0.10f, 0.10f, 0.97f);
            colors.selectedColor = CleanBg;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var cleanSprite = IconFactory.GetIconByKey("clear");
            if (cleanSprite != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);

                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                iconRt.anchoredPosition = Vector2.zero;

                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = cleanSprite;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }
            else
            {
                AddTextLabel(go, "CLR");
            }

            var drag = go.AddComponent<ButtonDragHandler>();
            drag.SetPositionCallback(parent.name == "CraftSortFoodTabs" ? SaveFoodPosition : SaveCombatPosition);

            btn.onClick.AddListener(() =>
            {
                NewRecipeTracker.MarkAllViewed();
                InvokeUpdateCraftingPanel();
                UpdateNewCount(0);
            });
        }

        private static (Button, Image, Image) CreateButtonBase(string label, SortMode mode, string configKey, Transform parent, string? iconKey = null)
        {
            var go = new GameObject($"SortTab_{mode}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = ButtonSize;
            le.preferredHeight = ButtonSize;

            var img = go.AddComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = NormalBg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = NormalBg;
            colors.highlightedColor = HoverBg;
            colors.pressedColor = ActiveBg;
            colors.selectedColor = NormalBg;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var sprite = iconKey != null ? IconFactory.GetIconByKey(iconKey) : IconFactory.GetIcon(mode);
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);

                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                iconRt.anchoredPosition = Vector2.zero;

                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }
            else
            {
                AddTextLabel(go, label);
            }

            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(go.transform, false);
            borderGo.transform.SetAsFirstSibling();

            var borderRt = borderGo.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            var borderImg = borderGo.AddComponent<Image>();
            borderImg.sprite = _borderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = BorderCol;
            borderImg.raycastTarget = false;
            borderGo.SetActive(false);

            var drag2 = go.AddComponent<ButtonDragHandler>();
            drag2.SetPositionCallback(parent.name == "CraftSortFoodTabs" ? SaveFoodPosition : SaveCombatPosition);

            return (btn, img, borderImg);
        }

        private static void AddTextLabel(GameObject go, string label)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);

            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.color = Color.white;
            text.fontSize = 10;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = _cachedFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (label == "New")
                _newTabTexts.Add(text);
        }

        private static void UpdateButtonStates()
        {
            bool fullUpdate = _lastActiveMode == (SortMode)(-1) || _lastActiveFilter == (WeaponFilter)(-1);

            if (!fullUpdate && _lastActiveMode == SortLogic.CurrentMode && _lastActiveFilter == SortLogic.CurrentFilter)
                return;

            for (int i = 0; i < _sortButtons.Count; i++)
            {
                var (btn, img, border, mode) = _sortButtons[i];
                if (btn == null) continue;

                if (fullUpdate || mode == _lastActiveMode || mode == SortLogic.CurrentMode)
                {
                    bool active = mode == SortLogic.CurrentMode;
                    if (border != null)
                        border.gameObject.SetActive(active);
                    if (img != null)
                        img.color = active ? ActiveBg : NormalBg;
                }
            }

            for (int i = 0; i < _filterButtons.Count; i++)
            {
                var (btn, img, border, filter) = _filterButtons[i];
                if (btn == null) continue;

                if (fullUpdate || filter == _lastActiveFilter || filter == SortLogic.CurrentFilter)
                {
                    bool active = filter == SortLogic.CurrentFilter;
                    if (border != null)
                        border.gameObject.SetActive(active);
                    if (img != null)
                        img.color = active ? ActiveBg : NormalBg;
                }
            }

            _lastActiveMode = SortLogic.CurrentMode;
            _lastActiveFilter = SortLogic.CurrentFilter;
        }

        private static void UpdateGroupVisibility()
        {
            var station = Player.m_localPlayer?.GetCurrentCraftingStation();
            string? stationKey = station != null ? station.gameObject.name : null;

            bool isFoodStation;
            if (stationKey == _cachedStationKey)
            {
                isFoodStation = _cachedIsFoodStation;
            }
            else
            {
                isFoodStation = DetectFoodStation(station);
                _cachedStationKey = stationKey;
                _cachedIsFoodStation = isFoodStation;
                CraftSortPlugin.Log($"[CraftSort] Station '{stationKey}' → {(isFoodStation ? "food" : "combat")} UI");
            }

            if (_foodContainer != null) _foodContainer.SetActive(isFoodStation);
            if (_combatContainer != null) _combatContainer.SetActive(!isFoodStation);
        }

        private static bool DetectFoodStation(CraftingStation? station)
        {
            if (station == null) return false;

            var gui = InventoryGui.instance;
            if (gui != null)
            {
                var list = Patch_UpdateRecipeList.GetAvailableRecipesList(gui);
                if (list != null && list.Count > 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var recipe = Patch_UpdateRecipeList.GetRecipeFromPair(list[i]);
                        var shared = recipe?.m_item?.m_itemData?.m_shared;
                        if (shared == null) continue;
                        if (IsFoodItem(shared)) return true;
                    }
                }
            }

            var db = ObjectDB.instance;
            if (db != null && db.m_recipes != null)
            {
                string stationPrefab = GetPrefabName(station.gameObject);

                for (int i = 0; i < db.m_recipes.Count; i++)
                {
                    var recipe = db.m_recipes[i];
                    if (recipe == null || recipe.m_craftingStation == null) continue;
                    if (GetPrefabName(recipe.m_craftingStation.gameObject) != stationPrefab) continue;

                    var shared = recipe.m_item?.m_itemData?.m_shared;
                    if (shared == null) continue;

                    if (IsFoodItem(shared)) return true;
                }
            }

            string name = station.gameObject.name;
            return name.IndexOf("cauldron", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("preptable", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFoodItem(ItemDrop.ItemData.SharedData shared)
        {
            return shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
        }

        private static string GetPrefabName(GameObject go)
        {
            string name = go.name;
            const string cloneSuffix = "(Clone)";
            if (name.EndsWith(cloneSuffix))
                return name.Substring(0, name.Length - cloneSuffix.Length);
            return name;
        }

        private static void InvokeUpdateCraftingPanel()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;

            if (_updateCraftingPanel == null)
            {
                _updateCraftingPanel = typeof(InventoryGui).GetMethod(
                    "UpdateCraftingPanel",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(bool) },
                    null);

                if (_updateCraftingPanel == null)
                {
                    _updateCraftingPanel = typeof(InventoryGui).GetMethod(
                        "UpdateCraftingPanel",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_updateCraftingPanel == null)
                {
                    CraftSortPlugin.Log("[TabUI] ERROR: UpdateCraftingPanel method not found via reflection");
                    return;
                }
            }

            try
            {
                _updateCraftingPanel.Invoke(gui, _updatePanelArgs);
            }
            catch (Exception ex)
            {
                CraftSortPlugin.Log($"[TabUI] InvokeUpdateCraftingPanel error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public static void UpdateNewCount(int count)
        {
            string text = count > 0 ? $"New ({count})" : "New";
            for (int i = 0; i < _newTabTexts.Count; i++)
            {
                if (_newTabTexts[i] != null)
                    _newTabTexts[i].text = text;
            }
        }

        public static void Reset()
        {
            if (_foodContainer != null)
                UnityEngine.Object.Destroy(_foodContainer);
            if (_combatContainer != null)
                UnityEngine.Object.Destroy(_combatContainer);

            DestroySpriteAndTexture(_roundedSprite);
            DestroySpriteAndTexture(_borderSprite);

            _foodContainer = null;
            _combatContainer = null;
            _sortButtons.Clear();
            _filterButtons.Clear();
            _newTabTexts.Clear();
            _roundedSprite = null;
            _borderSprite = null;
            _lastActiveMode = (SortMode)(-1);
            _lastActiveFilter = (WeaponFilter)(-1);
            _cachedStationKey = null;
            _cachedIsFoodStation = false;
            _editMode = false;
            _foodResizeCtrl = null;
            _combatResizeCtrl = null;
            _roundedSprite = null;
            _borderSprite = null;
            _updateCraftingPanel = null;
            _cfgFoodPosX = null;
            _cfgFoodPosY = null;
            _cfgFoodWidth = null;
            _cfgCombatPosX = null;
            _cfgCombatPosY = null;
            _cfgCombatWidth = null;
        }

        private static void DestroySpriteAndTexture(Sprite? sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        private static Sprite CreateRoundedSprite(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float alpha = RoundedRectAlpha(x, y, width, height, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }

            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateRoundedBorderSprite(int width, int height, int radius, int thickness)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float outer = RoundedRectAlpha(x, y, width, height, radius);
                    float inner = RoundedRectAlpha(
                        x - thickness, y - thickness,
                        width - thickness * 2, height - thickness * 2,
                        Mathf.Max(1, radius - thickness));
                    float alpha = Mathf.Clamp01(outer - inner);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }

            tex.Apply();
            float b = thickness + 1;
            var border = new Vector4(b, b, b, b);
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static float RoundedRectAlpha(int x, int y, int w, int h, int r)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return 0f;
            int cx = Mathf.Clamp(x, r, w - 1 - r);
            int cy = Mathf.Clamp(y, r, h - 1 - r);
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            if (x >= r && x < w - r) return 1f;
            if (y >= r && y < h - r) return 1f;
            return Mathf.Clamp01(r - dist + 0.5f);
        }
    }
}
