using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using VampireCommandFramework;

namespace Bloodcraft;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    private Harmony _harmony;
    internal static Plugin Instance { get; private set; }
    public static ManualLogSource LogInstance => Instance.Log;

    public static readonly string ConfigPath = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);

    // old paths to migrate data if needed
    public static readonly string OldPlayerExperiencePath = Path.Combine(ConfigPath, "ExperienceLeveling");

    // current paths
    public static readonly string PlayerLevelingPath = Path.Combine(ConfigPath, "PlayerLeveling");
    public static readonly string PlayerExpertisePath = Path.Combine(ConfigPath, "WeaponExpertise");
    public static readonly string PlayerBloodPath = Path.Combine(ConfigPath, "BloodLegacies");
    public static readonly string PlayerProfessionPath = Path.Combine(ConfigPath, "Professions");
    public static readonly string PlayerFamiliarsPath = Path.Combine(ConfigPath, "Familiars");
    public static readonly string FamiliarExperiencePath = Path.Combine(PlayerFamiliarsPath, "FamiliarLeveling");
    public static readonly string FamiliarUnlocksPath = Path.Combine(PlayerFamiliarsPath, "FamiliarUnlocks");

    private static ConfigEntry<bool> _levelingSystem;
    private static ConfigEntry<bool> _prestigeSystem;
    private static ConfigEntry<int> _maxLevelingPrestiges;
    private static ConfigEntry<float> _prestigeRatesReducer; //reduces gains by this percent per level of prestige for all systems, expertise/legacy rates can be raised by prestiging in leveling
    private static ConfigEntry<float> _prestigeStatMultiplier; //increases stats gained from expertise/legacies per level of prestige
    private static ConfigEntry<float> _prestigeRatesMultiplier; //increases player gains in expertise/legacy by this percent per level of prestige (leveling prestige)
    private static ConfigEntry<int> _maxPlayerLevel;
    private static ConfigEntry<int> _startingLevel;
    private static ConfigEntry<float> _unitLevelingMultiplier;
    private static ConfigEntry<float> _vBloodLevelingMultiplier;
    private static ConfigEntry<float> _groupLevelingMultiplier;
    private static ConfigEntry<float> _levelScalingMultiplier;
    private static ConfigEntry<bool> _playerGrouping;
    private static ConfigEntry<int> _maxGroupSize;

    private static ConfigEntry<bool> _expertiseSystem;
    private static ConfigEntry<int> _maxExpertisePrestiges;
    private static ConfigEntry<bool> _sanguimancy;
    private static ConfigEntry<int> _firstSlot;
    private static ConfigEntry<int> _secondSlot;
    private static ConfigEntry<int> _maxExpertiseLevel;
    private static ConfigEntry<float> _unitExpertiseMultiplier;
    private static ConfigEntry<float> _vBloodExpertiseMultiplier;
    private static ConfigEntry<int> _expertiseStatChoices;
    private static ConfigEntry<int> _resetExpertiseItem;
    private static ConfigEntry<int> _resetExpertiseItemQuantity;

    private static ConfigEntry<float> _maxHealth;
    private static ConfigEntry<float> _movementSpeed;
    private static ConfigEntry<float> _primaryAttackSpeed;
    private static ConfigEntry<float> _physicalLifeLeech;
    private static ConfigEntry<float> _spellLifeLeech;
    private static ConfigEntry<float> _primaryLifeLeech;
    private static ConfigEntry<float> _physicalPower;
    private static ConfigEntry<float> _spellPower;
    private static ConfigEntry<float> _physicalCritChance;
    private static ConfigEntry<float> _physicalCritDamage;
    private static ConfigEntry<float> _spellCritChance;
    private static ConfigEntry<float> _spellCritDamage;

    private static ConfigEntry<bool> _bloodSystem;
    private static ConfigEntry<int> _maxLegacyPrestiges;
    private static ConfigEntry<bool> _bloodQualityBonus;
    private static ConfigEntry<int> _maxBloodLevel;
    private static ConfigEntry<float> _unitLegacyMultiplier;
    private static ConfigEntry<float> _vBloodLegacyMultipler;
    private static ConfigEntry<int> _legacyStatChoices;
    private static ConfigEntry<int> _resetLegacyItem;
    private static ConfigEntry<int> _resetLegacyItemQuantity;

    private static ConfigEntry<float> _healingReceived;
    private static ConfigEntry<float> _damageReduction;
    private static ConfigEntry<float> _physicalResistance;
    private static ConfigEntry<float> _spellResistance;
    private static ConfigEntry<float> _resourceYield;
    private static ConfigEntry<float> _ccReduction;
    private static ConfigEntry<float> _spellCooldownRecoveryRate;
    private static ConfigEntry<float> _weaponCooldownRecoveryRate;
    private static ConfigEntry<float> _ultimateCooldownRecoveryRate;
    private static ConfigEntry<float> _minionDamage;
    private static ConfigEntry<float> _shieldAbsorb;
    private static ConfigEntry<float> _bloodEfficiency;

    private static ConfigEntry<bool> _professionSystem;
    private static ConfigEntry<int> _maxProfessionLevel;
    private static ConfigEntry<float> _professionMultiplier;

    private static ConfigEntry<bool> _familiarSystem;
    private static ConfigEntry<int> _maxFamiliarLevel;
    private static ConfigEntry<float> _unitFamiliarMultiplier;
    private static ConfigEntry<float> _vBloodFamiliarMultiplier;
    private static ConfigEntry<float> _unitUnlockChance;
    //private static ConfigEntry<float> _vBloodUnlockChance;

    public static ConfigEntry<bool> LevelingSystem => _levelingSystem;
    public static ConfigEntry<bool> PrestigeSystem => _prestigeSystem;
    public static ConfigEntry<int> MaxLevelingPrestiges => _maxLevelingPrestiges;
    public static ConfigEntry<float> PrestigeRatesReducer => _prestigeRatesReducer;
    public static ConfigEntry<float> PrestigeStatMultiplier => _prestigeStatMultiplier;
    public static ConfigEntry<float> PrestigeRatesMultiplier => _prestigeRatesMultiplier;
    public static ConfigEntry<int> MaxPlayerLevel => _maxPlayerLevel;
    public static ConfigEntry<int> StartingLevel => _startingLevel;
    public static ConfigEntry<float> UnitLevelingMultiplier => _unitLevelingMultiplier;
    public static ConfigEntry<float> VBloodLevelingMultiplier => _vBloodLevelingMultiplier;
    public static ConfigEntry<float> GroupLevelingMultiplier => _groupLevelingMultiplier;
    public static ConfigEntry<float> LevelScalingMultiplier => _levelScalingMultiplier;
    public static ConfigEntry<int> MaxGroupSize => _maxGroupSize;
    public static ConfigEntry<bool> PlayerGrouping => _playerGrouping;

    public static ConfigEntry<bool> ExpertiseSystem => _expertiseSystem;

    public static ConfigEntry<int> MaxExpertisePrestiges => _maxExpertisePrestiges;
    public static ConfigEntry<bool> Sanguimancy => _sanguimancy;
    public static ConfigEntry<int> FirstSlot => _firstSlot;
    public static ConfigEntry<int> SecondSlot => _secondSlot;
    public static ConfigEntry<int> MaxExpertiseLevel => _maxExpertiseLevel;
    public static ConfigEntry<float> UnitExpertiseMultiplier => _unitExpertiseMultiplier;
    public static ConfigEntry<float> VBloodExpertiseMultiplier => _vBloodExpertiseMultiplier;
    public static ConfigEntry<int> ExpertiseStatChoices => _expertiseStatChoices;
    public static ConfigEntry<int> ResetExpertiseItem => _resetExpertiseItem;
    public static ConfigEntry<int> ResetExpertiseItemQuantity => _resetExpertiseItemQuantity;

    public static ConfigEntry<float> MaxHealth => _maxHealth;
    public static ConfigEntry<float> MovementSpeed => _movementSpeed;
    public static ConfigEntry<float> PrimaryAttackSpeed => _primaryAttackSpeed;
    public static ConfigEntry<float> PhysicalLifeLeech => _physicalLifeLeech;
    public static ConfigEntry<float> SpellLifeLeech => _spellLifeLeech;
    public static ConfigEntry<float> PrimaryLifeLeech => _primaryLifeLeech;
    public static ConfigEntry<float> PhysicalPower => _physicalPower;
    public static ConfigEntry<float> SpellPower => _spellPower;
    public static ConfigEntry<float> PhysicalCritChance => _physicalCritChance;
    public static ConfigEntry<float> PhysicalCritDamage => _physicalCritDamage;
    public static ConfigEntry<float> SpellCritChance => _spellCritChance;
    public static ConfigEntry<float> SpellCritDamage => _spellCritDamage;

    public static ConfigEntry<bool> BloodSystem => _bloodSystem;
    public static ConfigEntry<int> MaxLegacyPrestiges => _maxLegacyPrestiges;
    public static ConfigEntry<bool> BloodQualityBonus => _bloodQualityBonus;
    public static ConfigEntry<int> MaxBloodLevel => _maxBloodLevel;
    public static ConfigEntry<float> UnitLegacyMultiplier => _unitLegacyMultiplier;
    public static ConfigEntry<float> VBloodLegacyMultipler => _vBloodLegacyMultipler;
    public static ConfigEntry<int> LegacyStatChoices => _legacyStatChoices;
    public static ConfigEntry<int> ResetLegacyItem => _resetLegacyItem;
    public static ConfigEntry<int> ResetLegacyItemQuantity => _resetLegacyItemQuantity;

    public static ConfigEntry<float> HealingReceived => _healingReceived;

    public static ConfigEntry<float> DamageReduction => _damageReduction;

    public static ConfigEntry<float> PhysicalResistance => _physicalResistance;

    public static ConfigEntry<float> SpellResistance => _spellResistance;

    public static ConfigEntry<float> ResourceYield => _resourceYield;

    public static ConfigEntry<float> CCReduction => _ccReduction;

    public static ConfigEntry<float> SpellCooldownRecoveryRate => _spellCooldownRecoveryRate;

    public static ConfigEntry<float> WeaponCooldownRecoveryRate => _weaponCooldownRecoveryRate;

    public static ConfigEntry<float> UltimateCooldownRecoveryRate => _ultimateCooldownRecoveryRate;

    public static ConfigEntry<float> MinionDamage => _minionDamage;

    public static ConfigEntry<float> ShieldAbsorb => _shieldAbsorb;

    public static ConfigEntry<float> BloodEfficiency => _bloodEfficiency;

    public static ConfigEntry<bool> ProfessionSystem => _professionSystem;
    public static ConfigEntry<int> MaxProfessionLevel => _maxProfessionLevel;
    public static ConfigEntry<float> ProfessionMultiplier => _professionMultiplier;

    public static ConfigEntry<bool> FamiliarSystem => _familiarSystem;
    public static ConfigEntry<int> MaxFamiliarLevel => _maxFamiliarLevel;
    public static ConfigEntry<float> UnitFamiliarMultiplier => _unitFamiliarMultiplier;
    public static ConfigEntry<float> VBloodFamiliarMultiplier => _vBloodFamiliarMultiplier;
    public static ConfigEntry<float> UnitUnlockChance => _unitUnlockChance;
    //public static ConfigEntry<float> VBloodUnlockChance => _vBloodUnlockChance;

    public override void Load()
    {
        Instance = this;
        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        InitConfig();
        CommandRegistry.RegisterAll();
        LoadAllData();
        Core.Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME}[{MyPluginInfo.PLUGIN_VERSION}] loaded!");
    }

    static void InitConfig()
    {
        if (Directory.Exists(OldPlayerExperiencePath))
        {
            // Move contents from the old path to the new path
            Directory.Move(OldPlayerExperiencePath, PlayerLevelingPath);
        }

        foreach (string path in directoryPaths)
        {
            CreateDirectories(path);
        }
 
        _levelingSystem = InitConfigEntry("Config", "LevelingSystem", false, "Enable or disable the leveling system.");
        _prestigeSystem = InitConfigEntry("Config", "PrestigeSystem", false, "Enable or disable the prestige system.");
        _maxLevelingPrestiges = InitConfigEntry("Config", "MaxLevelingPrestiges", 10, "The maximum number of prestiges a player can reach in leveling.");
        _prestigeRatesReducer = InitConfigEntry("Config", "PrestigeRatesReducer", 0.20f, "Multiplicative factor by which rates are reduced in expertise/legacy/experience per increment of prestige in expertise/legacy/experience.");
        _prestigeStatMultiplier = InitConfigEntry("Config", "PrestigeStatMultiplier", 0.25f, "Multiplicative rate by which stats are increased in expertise/legacy bonuses per increment of prestige in expertise/legacy.");
        _prestigeRatesMultiplier = InitConfigEntry("Config", "PrestigeRateMultiplier", 0.15f, "Multiplicative factor by which rates are increased in expertise/legacy per increment of prestige in leveling.");
        _maxPlayerLevel = InitConfigEntry("Config", "MaxLevel", 90, "The maximum level a player can reach.");
        _startingLevel = InitConfigEntry("Config", "StartingLevel", 0, "Starting level for players if no data is found.");
        _unitLevelingMultiplier = InitConfigEntry("Config", "UnitLevelingMultiplier", 5f, "The multiplier for experience gained from units.");
        _vBloodLevelingMultiplier = InitConfigEntry("Config", "VBloodLevelingMultiplier", 15f, "The multiplier for experience gained from VBloods.");
        _groupLevelingMultiplier = InitConfigEntry("Config", "GroupLevelingMultiplier", 1f, "The multiplier for experience gained from group kills.");
        _levelScalingMultiplier = InitConfigEntry("Config", "LevelScalingMultiplier", 0.05f, "Scaling multiplier for tapering experience gained at higher levels.");
        _playerGrouping = InitConfigEntry("Config", "PlayerGrouping", false, "Enable or disable the ability to group with players not in your clan for experience sharing.");
        _maxGroupSize = InitConfigEntry("Config", "MaxGroupSize", 5, "The maximum number of players that can share experience in a group.");

        _expertiseSystem = InitConfigEntry("Config", "ExpertiseSystem", false, "Enable or disable the expertise system.");
        _maxExpertisePrestiges = InitConfigEntry("Config", "MaxExpertisePrestiges", 10, "The maximum number of prestiges a player can reach in expertise.");
        _sanguimancy = InitConfigEntry("Config", "Sanguimancy", false, "Enable or disable sanguimancy (extra spells for unarmed expertise).");
        _firstSlot = InitConfigEntry("Config", "FirstSlot", 25, "Level of sanguimancy required for first slot unlock.");
        _secondSlot = InitConfigEntry("Config", "SecondSlot", 50, "Level of sanguimancy required for second slot unlock.");
        _maxExpertiseLevel = InitConfigEntry("Config", "MaxExpertiseLevel", 99, "The maximum level a player can reach in weapon expertise.");
        _unitExpertiseMultiplier = InitConfigEntry("Config", "UnitExpertiseMultiplier", 2f, "The multiplier for expertise gained from units.");
        _vBloodExpertiseMultiplier = InitConfigEntry("Config", "VBloodExpertiseMultiplier", 5f, "The multiplier for expertise gained from VBloods.");
        _expertiseStatChoices = InitConfigEntry("Config", "ExpertiseStatChoices", 2, "The maximum number of stat choices a player can pick for a weapon expertise (5 max).");
        _resetExpertiseItem = InitConfigEntry("Config", "ResetExpertiseItem", 0, "Item PrefabGUID cost for resetting weapon stats.");
        _resetExpertiseItemQuantity = InitConfigEntry("Config", "ResetExpertiseItemQuantity", 0, "Quantity of item required for resetting stats.");

        _maxHealth = InitConfigEntry("Config", "MaxHealth", 250f, "The base cap for maximum health.");
        _movementSpeed = InitConfigEntry("Config", "MovementSpeed", 0.25f, "The base cap for movement speed.");
        _primaryAttackSpeed = InitConfigEntry("Config", "PrimaryAttackSpeed", 0.25f, "The base cap for primary attack speed.");
        _physicalLifeLeech = InitConfigEntry("Config", "PhysicalLifeLeech", 0.15f, "The base cap for physical life leech.");
        _spellLifeLeech = InitConfigEntry("Config", "SpellLifeLeech", 0.15f, "The base cap for spell life leech.");
        _primaryLifeLeech = InitConfigEntry("Config", "PrimaryLifeLeech", 0.25f, "The base cap for primary life leech.");
        _physicalPower = InitConfigEntry("Config", "PhysicalPower", 15f, "The base cap for physical power.");
        _spellPower = InitConfigEntry("Config", "SpellPower", 15f, "The base cap for spell power.");
        _physicalCritChance = InitConfigEntry("Config", "PhysicalCritChance", 0.15f, "The base cap for physical critical strike chance.");
        _physicalCritDamage = InitConfigEntry("Config", "PhysicalCritDamage", 0.75f, "The base cap for physical critical strike damage.");
        _spellCritChance = InitConfigEntry("Config", "SpellCritChance", 0.15f, "The base cap for spell critical strike chance.");
        _spellCritDamage = InitConfigEntry("Config", "SpellCritDamage", 0.75f, "The base cap for spell critical strike damage.");

        _bloodSystem = InitConfigEntry("Config", "BloodSystem", false, "Enable or disable the blood legacy system.");
        _maxLegacyPrestiges = InitConfigEntry("Config", "MaxLegacyPrestiges", 10, "The maximum number of prestiges a player can reach in blood legacies.");
        _bloodQualityBonus = InitConfigEntry("Config", "BloodQualityBonus", false, "Enable or disable blood quality bonus (wouldn't recommend using this after the revamp but left it in on request, blood system must be turned on as well).");
        _maxBloodLevel = InitConfigEntry("Config", "MaxBloodLevel", 99, "The maximum level a player can reach in blood legacies.");
        _unitLegacyMultiplier = InitConfigEntry("Config", "UnitLegacyMultiplier", 1f, "The multiplier for lineage gained from units.");
        _vBloodLegacyMultipler = InitConfigEntry("Config", "VBloodLegacyMultipler", 5f, "The multiplier for lineage gained from VBloods.");
        _legacyStatChoices = InitConfigEntry("Config", "LegacyStatChoices", 2, "The maximum number of stat choices a player can pick for a blood legacy (5 max).");
        _resetLegacyItem = InitConfigEntry("Config", "ResetLegacyItem", 0, "Item PrefabGUID cost for resetting blood stats.");
        _resetLegacyItemQuantity = InitConfigEntry("Config", "ResetLegacyItemQuantity", 0, "Quantity of item required for resetting blood stats.");

        _healingReceived = InitConfigEntry("Config", "HealingReceived", 0.25f, "The base cap for healing received.");
        _damageReduction = InitConfigEntry("Config", "DamageReduction", 0.10f, "The base cap for damage reduction.");
        _physicalResistance = InitConfigEntry("Config", "PhysicalResistance", 0.20f, "The base cap for physical resistance.");
        _spellResistance = InitConfigEntry("Config", "SpellResistance", 0.20f, "The base cap for spell resistance.");
        _resourceYield = InitConfigEntry("Config", "ResourceYield", 0.25f, "The base cap for resource yield.");
        _ccReduction = InitConfigEntry("Config", "CCReduction", 0.25f, "The base cap for crowd control reduction.");
        _spellCooldownRecoveryRate = InitConfigEntry("Config", "SpellCooldownRecoveryRate", 0.15f, "The base cap for spell cooldown recovery rate.");
        _weaponCooldownRecoveryRate = InitConfigEntry("Config", "WeaponCooldownRecoveryRate", 0.15f, "The base cap for weapon cooldown recovery rate.");
        _ultimateCooldownRecoveryRate = InitConfigEntry("Config", "UltimateCooldownRecoveryRate", 0.20f, "The base cap for ultimate cooldown recovery rate.");
        _minionDamage = InitConfigEntry("Config", "MinionDamage", 0.25f, "The base cap for minion damage.");
        _shieldAbsorb = InitConfigEntry("Config", "ShieldAbsorb", 0.50f, "The base cap for shield absorb.");
        _bloodEfficiency = InitConfigEntry("Config", "BloodEfficiency", 0.10f, "The base cap for blood efficiency.");

        _professionSystem = InitConfigEntry("Config", "ProfessionSystem", false, "Enable or disable the profession system.");
        _maxProfessionLevel = InitConfigEntry("Config", "MaxProfessionLevel", 99, "The maximum level a player can reach in professions.");
        _professionMultiplier = InitConfigEntry("Config", "ProfessionMultiplier", 10f, "The multiplier for profession experience gained.");

        _familiarSystem = InitConfigEntry("Config", "FamiliarSystem", false, "Enable or disable the familiar system.");
        _maxFamiliarLevel = InitConfigEntry("Config", "MaxFamiliarLevel", 90, "The maximum level a familiar can reach.");
        _unitFamiliarMultiplier = InitConfigEntry("Config", "UnitFamiliarMultiplier", 5f, "The multiplier for experience gained from units.");
        _vBloodFamiliarMultiplier = InitConfigEntry("Config", "VBloodFamiliarMultiplier", 15f, "The multiplier for experience gained from VBloods.");
        _unitUnlockChance = InitConfigEntry("Config", "UnitUnlockChance", 0.05f, "The chance for a unit to unlock a familiar.");
        //_vBloodUnlockChance = InitConfigEntry("Config", "VBloodUnlockChance", 0.01f, "The chance for a VBlood to unlock a familiar.");
        // Initialize configuration settings
    }

    static ConfigEntry<T> InitConfigEntry<T>(string section, string key, T defaultValue, string description)
    {
        // Bind the configuration entry and get its value
        var entry = Instance.Config.Bind(section, key, defaultValue, description);

        // Check if the key exists in the configuration file and retrieve its current value
        var configFile = Path.Combine(ConfigPath, $"{MyPluginInfo.PLUGIN_GUID}.cfg");
        if (File.Exists(configFile))
        {
            var config = new ConfigFile(configFile, true);
            if (config.TryGetEntry(section, key, out ConfigEntry<T> existingEntry))
            {
                // If the entry exists, update the value to the existing value
                entry.Value = existingEntry.Value;
            }
        }
        return entry;
    }
    static void CreateDirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
    public override bool Unload()
    {
        Config.Clear();
        _harmony.UnpatchSelf();
        return true;
    }
    static void LoadAllData()
    {
        Core.DataStructures.LoadPlayerBools();
        if (LevelingSystem.Value)
        {
            foreach (var loadFunction in loadLeveling)
            {
                loadFunction();
            }
        }
        if (ExpertiseSystem.Value)
        {
            foreach (var loadFunction in loadExpertises)
            {
                loadFunction();
            }
            if (Sanguimancy.Value)
            {
                foreach (var loadFunction in loadSanguimancy)
                {
                    loadFunction();
                }
            }
        }
        if (BloodSystem.Value)
        {
            foreach (var loadFunction in loadLegacies)
            {
                loadFunction();
            }
        }
        if (ProfessionSystem.Value)
        {
            foreach (var loadFunction in loadProfessions)
            {
                loadFunction();
            }
        }
        if (FamiliarSystem.Value)
        {
            foreach (var loadFunction in loadFamiliars)
            {
                loadFunction();
            }
        }
    }

    static readonly Action[] loadLeveling =
    [
        Core.DataStructures.LoadPlayerExperience,
        Core.DataStructures.LoadPlayerPrestiges
    ];

    static readonly Action[] loadExpertises =
    [
        Core.DataStructures.LoadPlayerSwordExpertise,
        Core.DataStructures.LoadPlayerAxeExpertise,
        Core.DataStructures.LoadPlayerMaceExpertise,
        Core.DataStructures.LoadPlayerSpearExpertise,
        Core.DataStructures.LoadPlayerCrossbowExpertise,
        Core.DataStructures.LoadPlayerGreatSwordExpertise,
        Core.DataStructures.LoadPlayerSlashersExpertise,
        Core.DataStructures.LoadPlayerPistolsExpertise,
        Core.DataStructures.LoadPlayerReaperExpertise,
        Core.DataStructures.LoadPlayerLongbowExpertise,
        Core.DataStructures.LoadPlayerWhipExpertise,
        Core.DataStructures.LoadPlayerWeaponStats
    ];

    static readonly Action[] loadSanguimancy =
    [
        Core.DataStructures.LoadPlayerSanguimancy,
        Core.DataStructures.LoadPlayerSanguimancySpells
    ];

    static readonly Action[] loadLegacies =
    [
        Core.DataStructures.LoadPlayerWorkerLegacy,
        Core.DataStructures.LoadPlayerWarriorLegacy,
        Core.DataStructures.LoadPlayerScholarLegacy,
        Core.DataStructures.LoadPlayerRogueLegacy,
        Core.DataStructures.LoadPlayerMutantLegacy,
        Core.DataStructures.LoadPlayerVBloodLegacy,
        Core.DataStructures.LoadPlayerDraculinLegacy,
        Core.DataStructures.LoadPlayerImmortalLegacy,
        Core.DataStructures.LoadPlayerCreatureLegacy,
        Core.DataStructures.LoadPlayerBruteLegacy,
        Core.DataStructures.LoadPlayerBloodStats
    ];

    static readonly Action[] loadProfessions =
    [
        Core.DataStructures.LoadPlayerWoodcutting,
        Core.DataStructures.LoadPlayerMining,
        Core.DataStructures.LoadPlayerFishing,
        Core.DataStructures.LoadPlayerBlacksmithing,
        Core.DataStructures.LoadPlayerTailoring,
        Core.DataStructures.LoadPlayerEnchanting,
        Core.DataStructures.LoadPlayerAlchemy,
        Core.DataStructures.LoadPlayerHarvesting,
    ];
    static readonly Action[] loadFamiliars =
    [
        Core.DataStructures.LoadPlayerFamiliarActives,
        Core.DataStructures.LoadPlayerFamiliarSets
    ];

    static readonly List<string> directoryPaths =
        [
        ConfigPath,
        PlayerLevelingPath,
        PlayerExpertisePath,
        PlayerBloodPath,
        PlayerProfessionPath,
        PlayerFamiliarsPath,
        FamiliarExperiencePath,
        FamiliarUnlocksPath
        ];
}