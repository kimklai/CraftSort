using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace CraftSort
{
    [BepInPlugin("dev.craftsort", "CraftSort", "1.3.2")]
    public class CraftSortPlugin : BaseUnityPlugin
    {
        public static CraftSortPlugin Instance { get; private set; } = null!;

        private ConfigEntry<bool> _enabled = null!;
        private ConfigEntry<string> _defaultSortMode = null!;
        private ConfigEntry<bool> _rememberLastMode = null!;
        private Harmony? _harmony;

        private static bool _cachedEnabled;
        public static bool Enabled => _cachedEnabled;
        public static string DefaultSortMode => Instance._defaultSortMode.Value;
        public static bool RememberLastMode => Instance._rememberLastMode.Value;

        private static readonly Dictionary<string, ConfigEntry<bool>> _buttonConfigs
            = new Dictionary<string, ConfigEntry<bool>>();

        public static bool IsButtonEnabled(string key)
        {
            if (_buttonConfigs.TryGetValue(key, out var cfg))
                return cfg.Value;
            Log($"[CraftSort] WARNING: button config key '{key}' not found");
            return true;
        }

        public static void Log(string msg) => Instance?.Logger.LogInfo(msg);

        private void Awake()
        {
            Instance = this;

            _enabled = Config.Bind("General", "Enabled", true, "Enable or disable the mod");
            _defaultSortMode = Config.Bind("General", "DefaultSortMode", "None", "Sort mode on open: None/Armor/Block/PhysDmg/SlashDmg/PierceDmg/BluntDmg/etc");
            _rememberLastMode = Config.Bind("General", "RememberLastMode", false, "Keep last sort mode between station openings");

            BindButtonConfigs();

            _cachedEnabled = _enabled.Value;
            _enabled.SettingChanged += OnEnabledSettingChanged;

            if (!_cachedEnabled)
                return;

            if (System.Enum.TryParse<SortMode>(_defaultSortMode.Value, true, out var defaultMode))
                SortLogic.CurrentMode = defaultMode;

            _harmony = new Harmony("dev.craftsort");
            _harmony.PatchAll();
            Logger.LogInfo("CraftSort loaded");
        }

        private void OnEnabledSettingChanged(object sender, System.EventArgs e)
        {
            _cachedEnabled = _enabled.Value;
        }

        private void OnDestroy()
        {
            if (_enabled != null)
                _enabled.SettingChanged -= OnEnabledSettingChanged;

            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to remove Harmony patches during unload: {ex}");
            }
            finally
            {
                _harmony = null;
                TabUI.Reset();
                NewRecipeTracker.Reset();
                IconFactory.Reset();
                SortLogic.Reset();
                _buttonConfigs.Clear();
                _cachedEnabled = false;

                if (Instance == this)
                    Instance = null!;
            }
        }

        private void BindButtonConfigs()
        {
            string[] foodButtons = { "All", "HP", "Stamina", "Eitr", "AZ", "New", "Clean" };
            foreach (string b in foodButtons)
            {
                string key = $"Food_{b}";
                _buttonConfigs[key] = Config.Bind("Buttons.Food", $"Show_{b}", true,
                    $"Show '{b}' button in food station panel");
            }

            string[] combatButtons = { "All", "Armor", "Block", "Slash", "Pierce", "Blunt",
                "Fire", "Frost", "Lightning", "Poison", "Spirit",
                "1H", "2H", "AZ", "New", "Clean" };
            foreach (string b in combatButtons)
            {
                string key = $"Combat_{b}";
                _buttonConfigs[key] = Config.Bind("Buttons.Combat", $"Show_{b}", true,
                    $"Show '{b}' button in combat station panel");
            }
        }
    }
}
