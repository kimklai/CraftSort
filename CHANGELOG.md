# Changelog

All notable changes to CraftSort are documented in this file.

---

## [Unreleased]

### Added
- Pin recipes with right-click; pins affect only the default recipe order and are saved per character.

### Fixed
- Eitr sorting excludes recipes that do not provide Eitr, including after cooking or fermentation.
- Upgrade item rows no longer appear in the Craft tab; the Upgrade tab remains available.

---

## [1.5.0] — 2026-10-19

### Added
- Safe ScriptEngine reload support with cleanup of Harmony patches, UI, event handlers, and runtime resources.
- Build deployment to the Valheim installation's `BepInEx/scripts` directory, selected through `VALHEIM_INSTALL`.
- Build deployment copies the PDB alongside the DLL for ScriptEngine reload support.

### Changed
- Food preparation table recipes sort using their cooked recovery stats.
- Mead recipes sort using fermented health, stamina, and Eitr recovery stats.
- Long-duration effects that increase the corresponding recovery rate take priority over direct restoration.

### Fixed
- Corrected food station classification for preparation tables and fermenters.

---

## [1.3.2] — 2026-07-28

### Changed
- Screenshots resized to equal height (692px) for consistent README layout
- Added drag & resize GIF demo to README
- GitHub README: centered images via `<p align="center">`

---

## [1.3.0] — 2026-07-28

### Added
- **All-icon buttons** — every button now uses a custom icon instead of text (20 icons total)
- **New icons**: Pierce (spear), Blunt (war hammer), 1H (one finger), 2H (two fingers), Clean (broom), Resize handle (four arrows)
- **Independent panels** — food and combat menus have separate position, width, and resize handle
- **Resize handle background** — dark rounded background prevents blending with game UI
- **Drag & resize GIF demo** in README

### Changed
- **Unified light palette** — all icon colors adjusted to soft/light tonality
- **New recipe dot**: blue → yellow `(1.0, 0.82, 0.15)`
- **Resize handle size**: 14px → 20px with two-image visibility system (bg + icon)
- **Config `[UI]` section**: replaced shared `PositionX/Y/CombatPanelWidth` with 6 independent entries: `FoodPosX`, `FoodPosY`, `FoodWidth`, `CombatPosX`, `CombatPosY`, `CombatWidth`
- Both panels use `GridLayoutGroup` for column reflow on resize
- Updated screenshots in README and docs

### Fixed
- 1H/2H filter buttons showed wrong icon (All icon instead of their own)
- Food panel couldn't reflow into multiple columns (was VerticalLayoutGroup)

---

## [1.2.0] — 2026-07-27

### Added
- **Weapon filters** — 1H / 2H toggle buttons with blue border, combinable with any sort mode
- **Clean (CLR) button** — marks all known recipes as viewed, clears all blue dots
- **Drag & resize via EditMode** — right-click corner handle to toggle; drag buttons to reposition, drag handle to resize
- **Per-button config** — `[Buttons.Food]` / `[Buttons.Combat]` sections, each button `Show_<Name> = true/false`
- **resetknownitems hook** — postfix on `Player.ResetCharacterKnownItems` clears CraftSort cache
- **Pierce / Blunt sort modes** with WeaponTypePriority bonus

### Changed
- CornerRadius 4 → 7
- Resize handle: hover-only visibility via `Image.color.a` (not CanvasGroup)
- Removed Phys and Chop buttons from UI (Chop icon repurposed for Slash)
- Logging optimized: only first-run snapshot + save errors + field-not-found warnings

### Fixed
- NormalizeName strips "(Clone)" suffix for safety

---

## [1.1.12] — 2026-07-25

### Added
- Discord invite link in README

---

## [1.1.0] — 2026-07-24

### Added
- **Icon-based tab UI** — 15 RGBA icons embedded as base64, loaded via `Texture2D.LoadRawTextureData`
- **2-column combat layout** — GridLayoutGroup with FlexibleColumnCount
- **New recipe indicator** — blue dot on unviewed recipes + "New" filter tab
- **Per-character persistence** — viewed recipes tracked per save file
- **AAA Crafting compatibility** — global sort across paginated pages via separate Prefix
- **Auto food/combat detection** — three-layer check (UI recipes → ObjectDB → name fallback)

### Changed
- Replaced Postfix reposition with **Transpiler injection** on `InventoryGui.UpdateRecipeList`
- Static anchor-based positioning (replaced world-space tracking)
- Tab buttons positioned outside crafting panel (left side)

### Fixed
- Visual sort now works correctly (transpiler injects before vanilla positioning loop)

---

## [1.0.0] — 2026-07-23

### Added
- Initial release
- Sort recipes by: Armor, Block, Phys, Fire, Frost, Lightning, Poison, Spirit, Chop, HP, Stamina, Eitr, Name
- BepInEx 5 plugin with Harmony patches
- Client-side only, no server mods required
