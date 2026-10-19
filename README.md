# CraftSort

**Sort crafting recipes by stats — one click, instant order.**

Revival of the deprecated SortCraft mod. Adds icon tab buttons to the left side of every crafting station. Click to sort, click again to toggle off. Drag to reposition, resize to fit your screen.

<p align="center">
  <img src="https://raw.githubusercontent.com/GrandpaUA/CraftSort/main/docs/2.png" alt="Combat station">
  &ensp;
  <img src="https://raw.githubusercontent.com/GrandpaUA/CraftSort/main/docs/1.png" alt="Food station">
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/GrandpaUA/CraftSort/main/docs/3.gif" alt="Drag & resize demo">
</p>

---

## ⚡ Features

- **18 sort/filter modes** — food stations show 7 tabs, combat stations show up to 17 in a flexible grid
- **All-icon buttons** — every button uses a custom icon, no text
- **Weapon filters** — 1H / 2H toggle buttons (blue border) combine with any sort mode
- **Auto food/combat detection** — works with any modded station by inspecting recipe content
- **New recipe indicator** — yellow dot on unviewed recipes + "New" filter tab
- **Clean button** — marks all known recipes as viewed, clears all yellow dots
- **Independent panels** — food and combat menus have separate position, width, and layout
- **Drag & resize** — right-click the corner handle to enter edit mode, then drag buttons to reposition or resize the panel
- **Per-button config** — enable/disable any button via config file
- **Per-character persistence** — tracked per save file
- **AAA Crafting compatible** — global sort across paginated pages
- **Client-side only** — no server mods required

## 📊 Sort Modes

| Icon | Sorts by | Icon | Sorts by |
|:----:|----------|:----:|----------|
| All | Default order | HP | Food health |
| Armor | Armor value | Stam | Food stamina |
| Block | Block power | Eitr | Food eitr |
| Slash | Slash damage | A→Z | Localized name |
| Pierce | Pierce damage | New | Unviewed only |
| Blunt | Blunt damage | CLR | Mark all viewed |
| Fire | Fire damage | | |
| Frost | Frost damage | | |
| Ltng | Lightning damage | | |
| Psn | Poison damage | | |
| Sprt | Spirit damage | | |

### Weapon Filters (blue border, combinable with any sort)

| Button | Shows |
|:------:|-------|
| 1H | One-handed weapons only |
| 2H | Two-handed weapons + bows |

## 🔧 Configuration

`BepInEx/config/dev.craftsort.cfg`

### General

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable or disable the mod |
| `DefaultSortMode` | `None` | Sort mode on panel open |
| `RememberLastMode` | `false` | Keep last mode between stations |

### Per-Button Visibility

Each button can be individually enabled/disabled:

```ini
[Buttons.Food]
Show_All = true
Show_HP = true
Show_Stamina = true
Show_Eitr = true
Show_AZ = true
Show_New = true
Show_Clean = true

[Buttons.Combat]
Show_All = true
Show_Armor = true
Show_Block = true
Show_Slash = true
Show_Pierce = true
Show_Blunt = true
Show_Fire = true
Show_Frost = true
Show_Lightning = true
Show_Poison = true
Show_Spirit = true
Show_1H = true
Show_2H = true
Show_AZ = true
Show_New = true
Show_Clean = true
```

### UI Position & Size

Each panel has independent position and width, persisted automatically when you drag or resize:

```ini
[UI]
FoodPosX = -9
FoodPosY = -200
FoodWidth = 50
CombatPosX = -9
CombatPosY = -200
CombatWidth = 104
```

## 🖱 Drag & Resize

1. **Hover** over the bottom-right corner of the button panel — a small handle with dark background appears
2. **Right-click** the handle to enter **Edit Mode** (handle turns solid yellow)
3. **Drag any button** to move the panel
4. **Drag the corner handle** to resize (columns adjust automatically)
5. **Right-click** the handle again to exit Edit Mode

Each panel (food / combat) is positioned and resized independently.

## 🎮 Console Command

With `devcommands` enabled:

```
resetknownitems
```

Clears CraftSort's viewed-recipe cache alongside the vanilla reset. All recipes will show as "new" again.

## ✅ Compatibility

Tested with Valheim **0.221.13** (Unity 6) across 6 mod profiles (35–101 mods each).

**Tested compatible — crafting interaction:**
AAA Crafting, BetterUI Forever Maintained, SearsCatalog, ExtraSlots, CraftFromContainers, AzuCraftyBoxes, Jewelcrafting, ItemCompare, Quick Stack Store Sort Trash Restock, Recycle N Reclaim, SmarterContainers, ContentsWithin

**Tested compatible — adds crafting stations:**
PlantEverything, BoneAppetit, KnowledgeTable, Armory, Shipwright, MassFarming, PlanBuild

**Tested compatible — adds recipes / framework:**
Jotunn, EpicLoot

**Incompatible:** InventorySlots (replaces the crafting UI entirely)

## 🛠 How It Works

- **Transpiler** on `InventoryGui.UpdateRecipeList` injects sort/filter before vanilla's positioning loop
- **AAA Crafting**: separate Prefix sorts `RecipeListPerfCache.CraftSortedFiltered` before pagination
- **Weapon filter**: applied before sort — removes non-matching items from the list
- **Food detection**: three-layer check (UI recipes → ObjectDB → name fallback) by item type & food stats
- **Yellow dots**: Postfix on `UpdateRecipeList` + `OnSelectedRecipe` for tracking viewed recipes
- **Drag/resize**: `ButtonDragHandler` + `ResizeHandleController` with pivot-swap trick for correct growth direction
- **Icons**: 20 RGBA icons embedded as base64, loaded via `Texture2D.LoadRawTextureData` at runtime

## 📦 Building from Source

```powershell
$env:VALHEIM_INSTALL = 'E:\SteamLibrary\steamapps\common\Valheim'
dotnet build CraftSort/CraftSort.csproj -c Debug
```

Requires .NET 8 SDK. Set `VALHEIM_INSTALL` to your Valheim installation directory when it is not at the default Steam location. Both Debug and Release configurations are supported:

```powershell
dotnet build CraftSort/CraftSort.csproj -c Release
```

Build outputs are written to `CraftSort/bin/{Configuration}/net48/`. Each build deploys both `CraftSort.dll` and `CraftSort.pdb` to `%VALHEIM_INSTALL%/BepInEx/scripts` for ScriptEngine.

### In-game development reload

CraftSort supports reloads through the BepInEx.Debug **ScriptEngine**. Install ScriptEngine in the profile's `BepInEx/plugins` folder, then move CraftSort out of `BepInEx/plugins` and into `BepInEx/scripts`. Do not keep a second CraftSort DLL in `plugins`, since both copies use the same plugin GUID.

Press **F6** in-game to unload and reload scripts. CraftSort removes its Harmony patches, UI objects, event handlers, and generated sprites during unload. Reopen the crafting panel after reload so its UI is recreated.

---

## 💬 Feedback & Contact

Found a bug, have an idea, or want to request compatibility with another mod? Reach out:

- **Discord:** [@mr_grandpa_oldman](https://discord.gg/29J7ygQ8R3)
- **Telegram:** [@mr_grandpa_oldman](https://t.me/mr_grandpa_oldman)

---

## ☕ Support

> I'm a simple engineer who has fun after work writing mods to improve the gaming experience. If you liked the mod, you can thank me, I'd be happy to.

[![Ko-fi](https://storage.ko-fi.com/cdn/kofi3.png?v=6)](https://ko-fi.com/grandpa_ai)

---

## 📄 License

AGPL-3.0

---

## 📋 Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.
