# CraftSort — Agent Instructions

## What this mod does

BepInEx 5 mod for Valheim that injects sort tab buttons into crafting stations.
Buttons appear on the left side of the recipe list and reorder recipes by item stats.
Includes a "new recipe" indicator (blue dot), filter tabs, weapon filters, drag & resize.
Revival of the deprecated SortCraft mod by KGvalheim.
Target game version: Valheim 0.221.13 (Unity 6 engine).

---

## Environment

Project root:   C:\All\Project\vibecode\CraftSort\
Source files:   C:\All\Project\vibecode\CraftSort\CraftSort\
Valheim:        C:\Program Files (x86)\Steam\steamapps\common\Valheim\
BepInEx root:   C:\Users\Admin\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\Craft\BepInEx\
Deploy DLL to:  ...BepInEx\plugins\CraftSort.dll
BepInEx log:    ...BepInEx\LogOutput.log

---

## Build setup

No .NET SDK is installed. Do this first:

    winget install Microsoft.DotNet.SDK.8

Verify: dotnet --version (must return 8.x or higher)

Build command:

    cd C:\All\Project\vibecode\CraftSort
    $env:VALHEIM_INSTALL = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
    dotnet build CraftSort\CraftSort.csproj -c Debug
    dotnet build CraftSort\CraftSort.csproj -c Release

Output: CraftSort\bin\{Debug|Release}\net48\CraftSort.dll and CraftSort.pdb
The csproj copies both files to $(VALHEIM_INSTALL)\BepInEx\scripts automatically.

---

## CraftSort.csproj — what is configured

- TargetFramework: net48
- AssemblyName: CraftSort
- ValheimPath: reads from VALHEIM_INSTALL env variable, falls back to standard Steam path
- BepInExPath: $(ValheimPath)\BepInEx
- All assembly references use Private=false (DLLs are not bundled in output)
- AfterBuild target copies output DLL and PDB to $(ValheimPath)\BepInEx\scripts
- Do not restructure the csproj

Assembly references:
- $(BepInExPath)\core\BepInEx.dll
- $(BepInExPath)\core\0Harmony.dll
- $(ValheimPath)\valheim_Data\Managed\assembly_valheim.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.CoreModule.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.UI.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.UIModule.dll

---

## Source files

    CraftSort\
    ├── CraftSort.csproj
    ├── Plugin.cs          BepInEx entry point, config, per-button config, Harmony bootstrap
    ├── SortLogic.cs       SortMode enum, WeaponFilter enum, GetSortValue, PassesFilter, index-based sort
    ├── Patches.cs         Transpiler + AAA compat + NewDots + OnSelectedRecipe + Hide cleanup + ResetKnown hook
    ├── TabUI.cs           Dual-container UI, drag/resize, EditMode, station auto-detection, button config
    ├── NewRecipeTracker.cs Per-character persistence, blue dot management, ClearAll for resetknownitems
    ├── PinnedRecipeTracker.cs Per-character pinned recipes and right-click toggling
    ├── IconFactory.cs     Loads RGBA from IconData, caches sprites via LoadRawTextureData
    └── IconData.cs        Auto-generated — 15 base64 RGBA strings (64×64 icons)

---

## Architecture

### Sort injection (Transpiler)
- Transpiler on `InventoryGui.UpdateRecipeList` injects `SortAvailableRecipes(InventoryGui)` before vanilla's positioning for-loop
- IL pattern: `ldc.i4.0 → stloc.s → br/br.s → ldarg.0 → ldfld m_availableRecipes` (last match)
- Must transfer IL labels from target instruction to first injected instruction
- Vanilla positions elements in our sorted order — no manual repositioning
- Weapon filter applied BEFORE sort mode check — filters even when no sort is active

### AAA Crafting compatibility
- `Patch_UpdateRecipeList_AAACrafting` Prefix sorts full `RecipeListPerfCache.CraftSortedFiltered` before pagination
- Conditional via `[HarmonyPrepare]` checking `Chainloader.PluginInfos`
- Type resolution via `FindType()` helper (assembly-qualified name + AppDomain fallback)
- Uses `_filteredCache` copy — never mutates AAA's original cache

### New recipe indicator
- `NewRecipeTracker` persists viewed recipes per character to `BepInEx/config/CraftSort/viewed_{name}.txt`
- First run: snapshots all `Player.m_knownRecipes` as viewed
- `NormalizeName()` strips "(Clone)" suffix (safety measure — Recipe is ScriptableObject, no Clone)
- `Patch_UpdateRecipeList_NewDots` Postfix: adds/removes blue dot overlays on recipe icons
- `Patch_OnSelectedRecipe_Viewed` Postfix: marks recipe as viewed on click
- Blue dot: 10px circle at top-right of recipe icon, color (0.25, 0.55, 1.0)
- `ClearAll()`: wipes cache + save file, sets `_initialized=true` to prevent re-snapshot
- `MarkAllViewed()`: marks m_knownRecipes + currently visible recipes (covers AAA variants)

### Weapon filter
- `WeaponFilter` enum: None, OneHanded, TwoHanded
- `SortLogic.PassesFilter()` checks `m_itemType`: 1H=3, 2H=14|4|20
- `FilterWeaponOnList()` removes non-matching items from m_availableRecipes, hides their elements
- Filter combines with any sort mode (e.g., Slash + 2H = two-handed weapons sorted by slash damage)

### UI layout
- Two separate containers anchored to panel top-left, position from config (default -9, -200)
- Food container: `VerticalLayoutGroup` (single column, 50×50 buttons)
- Combat container: `GridLayoutGroup` (FlexibleColumnCount, 50×50 buttons)
- `ContentSizeFitter` on both for auto-height
- `UpdateGroupVisibility()` shows/hides containers via three-layer food station detection:
  1. Check `m_availableRecipes` in UI (already filtered by vanilla for current station)
  2. Check `ObjectDB.instance.m_recipes` matching station prefab name (handles "(Clone)" suffix)
  3. Fallback to known vanilla names (cauldron/preptable)
  - `IsFoodItem()`: ItemType==Consumable && (m_food>0 || m_foodStamina>0 || m_foodEitr>0)
  - Result cached per station name, invalidated on `Reset()`
- Each button: rounded rect background (CornerRadius=7) + icon (34px) or text fallback + yellow border (5px) when active
- Filter buttons: blue border (FilterBorderCol) instead of yellow
- Clean button: red-tinted background (CleanBg), text "CLR"

### Drag & resize
- `ButtonDragHandler` on every button — drags container when EditMode is ON
- `ResizeHandleController` on corner handle — hover to show (alpha 0→0.5), right-click toggles EditMode
- EditMode ON: handle alpha=1, buttons draggable, handle stays visible
- EditMode OFF: handle invisible (alpha=0), buttons clickable normally
- Resize uses pivot-swap trick (SearsCatalog pattern): pivot→(0,1) during drag for correct growth direction
- Width snaps to column boundaries on release: `cols * (ButtonSize + spacing) - spacing`
- Position/width persisted to `[UI]` config section

### Per-button config
- `[Buttons.Food]` and `[Buttons.Combat]` sections in BepInEx config
- Each button: `Show_<Name> = true/false`
- `CraftSortPlugin.IsButtonEnabled(key)` checked in `CreateSortButton`/`CreateFilterButton`/`CreateCleanButton`
- Disabled buttons are not created at all (not just hidden)

### Console command hook
- `Patch_Player_ResetKnown` postfix on `Player.ResetCharacterKnownItems`
- Fires when vanilla `resetknownitems` command is used (requires `devcommands`)
- Calls `NewRecipeTracker.ClearAll()` + removes dots from current recipe list

### Icon system
- 15 icons from flaticon.com, processed via `_research/gen_icondata.ps1`:
  1. Auto-crop to bounding box + 5% margin
  2. Square-ize (expand shorter side centered)
  3. Area sampling (box filter) for anti-aliased edges
  4. Recolor black → Valheim palette color
  5. Output 64×64 raw RGBA → base64 in `IconData.cs`
- Runtime: `Texture2D.LoadRawTextureData` (no ImageConversionModule needed)
- SlashDmg uses "chop" icon key (axe icon repurposed for slash damage)

---

## Key Valheim types and fields

Recipe
- m_item: ItemDrop
- m_item.m_itemData: ItemDrop.ItemData
- m_item.m_itemData.m_shared: ItemDrop.ItemData.SharedData

ItemDrop.ItemData.SharedData fields:
- m_itemType: ItemType (Weapon, Shield, Helmet, Chest, Legs, Shoulder, Consumable, etc.)
- m_armor: float (default 10f!)
- m_blockPower: float (default 10f!)
- m_food / m_foodStamina / m_foodEitr: float (default 0)
- m_damages: HitData.DamageTypes (struct, never null)
- m_name: string (localization key)

HitData.DamageTypes fields:
- m_blunt, m_slash, m_pierce, m_chop, m_pickaxe
- m_fire, m_frost, m_lightning, m_poison, m_spirit

ItemType enum values (verified from assembly_valheim.dll 0.221.13):
- None=0, Material=1, Consumable=2, OneHandedWeapon=3, Bow=4, Shield=5
- Helmet=6, Chest=7, Ammo=9, Customization=10, Legs=11, Hands=12
- Trophy=13, TwoHandedWeapon=14, Torch=15, Misc=16, Shoulder=17
- Utility=18, Tool=19, Attach_Atgeir=20, Fish=21, TwoHandedWeaponLeft=22

InventoryGui+RecipeDataPair (public struct with auto-properties):
- Recipe, ItemData, InterfaceElement (GameObject), CanCraft
- Accessed via reflection (PropertyInfo cached)

---

## Code conventions

- Every access to Recipe, ItemData, SharedData, Player, InventoryGui must be null-safe
- Use nameof() in HarmonyPatch attributes where possible
- No server-side logic — client-only mod
- Pre-allocated caches in SortLogic (grow monotonically, never shrink)
- No params arrays in hot paths (use dedicated methods)
- Cached reflection (PropertyInfo/FieldInfo resolved once)
- After any fix always rebuild and verify 0 errors 0 warnings
- Minimize logging in production — only log first-run, errors, and critical state changes
- Use `typeof(Type)` in HarmonyPatch attributes, never string-based (causes TypeLoadException in 0Harmony)

---

## Success criteria

- dotnet build exits with 0 errors and 0 warnings
- File exists: ...BepInEx\scripts\CraftSort.dll
- LogOutput.log line: [Info : BepInEx] Loading [CraftSort 1.5.0]
- LogOutput.log line: CraftSort loaded
- No lines containing [Error near CraftSort or Patch_

---

## Constraints

- BepInEx 5.4.x only — import Harmony as: using HarmonyLib
- Unity 6 engine (Valheim 0.221.13)
- Private=false on all assembly references
- Client-side only — no server synchronization
- License: AGPL-3.0
