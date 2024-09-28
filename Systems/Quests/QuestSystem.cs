﻿using Bloodcraft.Services;
using Bloodcraft.Systems.Leveling;
using Bloodcraft.Utilities;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Bloodcraft.Patches.DeathEventListenerSystemPatch;
using Match = System.Text.RegularExpressions.Match;
using Random = System.Random;
using Regex = System.Text.RegularExpressions.Regex;

namespace Bloodcraft.Systems.Quests;
internal static class QuestSystem
{
    static EntityManager EntityManager => Core.EntityManager;
    static ServerGameManager ServerGameManager => Core.ServerGameManager;
    static SystemService SystemService => Core.SystemService;
    static PrefabCollectionSystem PrefabCollectionSystem => SystemService.PrefabCollectionSystem;

    static readonly float ResourceYieldModifier = SystemService.ServerGameSettingsSystem._Settings.MaterialYieldModifier_Global;

    static readonly Random Random = new();
    static readonly Regex Regex = new(@"T\d{2}");

    public static HashSet<PrefabGUID> CraftPrefabs = [];
    public static HashSet<PrefabGUID> ResourcePrefabs = [];

    static readonly PrefabGUID graveyardSkeleton = new(1395549638);
    static readonly PrefabGUID forestWolf = new(-1418430647);

    static readonly PrefabGUID reinforcedBoneSword = new(-796306296);
    static readonly PrefabGUID reinforcedBoneMace = new(-1998017941);

    static readonly PrefabGUID standardWood = new(-1593377811);
    static readonly PrefabGUID stone = new(-1531666018);
    public enum TargetType
    {
        Kill,
        Craft,
        Gather
    }

    static readonly List<TargetType> TargetTypes =
    [
        TargetType.Kill,
        TargetType.Craft,
        TargetType.Gather
    ];
    public enum QuestType
    {
        Daily,
        Weekly
    }

    static readonly Dictionary<QuestType, int> QuestMultipliers = new()
    {
        { QuestType.Daily, 1 },
        { QuestType.Weekly, 5 }
    };

    public static Dictionary<PrefabGUID, int> QuestRewards = [];
    public class QuestObjective
    {
        public TargetType Goal { get; set; }
        public string Name { get; set; }
        public PrefabGUID PrefabGUID { get; set; }
        public int Level { get; set; }
        public int RequiredAmount { get; set; }
        public bool Complete { get; set; }
    }

    static readonly Dictionary<string, (int MinLevel, int MaxLevel)> EquipmentTierLevelRangeMap = new()
    {
        { "T01", (0, 15) },
        { "T02", (10, 30) },
        { "T03", (20, 45) },
        { "T04", (30, 60) },
        { "T05", (40, 75) },
        { "T06", (50, 90) },
        { "T07", (60, ConfigService.MaxLevel) },
        { "T08", (70, ConfigService.MaxLevel) },
        { "T09", (80, ConfigService.MaxLevel) }
    };

    static readonly Dictionary<string, (int MinLevel, int MaxLevel)> ConsumableTierLevelRangeMap = new()
    {
        { "Salve_Vermin", (0, 30) },
        { "PhysicalPowerPotion_T01", (15, ConfigService.MaxLevel) },
        { "SpellPowerPotion_T01", (15, ConfigService.MaxLevel) },
        { "WranglersPotion_T01", (15, ConfigService.MaxLevel) },
        { "SunResistancePotion_T01", (15, ConfigService.MaxLevel) },
        { "HealingPotion_T01", (15, ConfigService.MaxLevel) },
        { "FireResistancePotion_T01", (15, ConfigService.MaxLevel) },
        { "DuskCaller", (50, ConfigService.MaxLevel) },
        { "SpellLeechPotion_T01", (50, ConfigService.MaxLevel) },
        { "PhysicalPowerPotion_T02", (65, ConfigService.MaxLevel) },
        { "SpellPowerPotion_T02", (65, ConfigService.MaxLevel) },
        { "HealingPotion_T02", (40, ConfigService.MaxLevel) },
        { "HolyResistancePotion_T01", (40, ConfigService.MaxLevel) },
        { "HolyResistancePotion_T02", (40, ConfigService.MaxLevel) }
    };

    static readonly Dictionary<ulong, Dictionary<QuestType, (int Progress, bool Active)>> QuestCoroutines = [];
    static readonly WaitForSeconds QuestMessageDelay = new(0.1f);
    static HashSet<PrefabGUID> GetKillPrefabsForLevel(int playerLevel)
    {
        Dictionary<PrefabGUID, HashSet<Entity>> TargetPrefabs = new(QuestService.TargetCache);
        HashSet<PrefabGUID> prefabs = [];

        foreach (PrefabGUID prefab in TargetPrefabs.Keys)
        {
            Entity targetEntity = TargetPrefabs[prefab].FirstOrDefault();
            if (targetEntity.TryGetComponent(out UnitLevel unitLevel) && Math.Abs(unitLevel.Level._Value - playerLevel) <= 10)
            {
                //if (prefabEntity.Has<VBloodUnit>() && level.Level._Value > playerLevel) continue;
                //if (TargetPrefabs[prefab].FirstOrDefault().Exists())
                //if (SpawnTransformSystemOnSpawnPatch.shardBearers.Contains(prefabGUID) || prefabGUID.Equals(villageElder)) continue;

                if (targetEntity.Has<VBloodUnit>() && unitLevel.Level._Value > playerLevel) continue;
                prefabs.Add(prefab);
            }
        }

        return prefabs;
    }
    static HashSet<PrefabGUID> GetCraftPrefabsForLevel(int playerLevel)
    {
        HashSet<PrefabGUID> prefabs = [];

        foreach (PrefabGUID prefab in CraftPrefabs)
        {
            Entity prefabEntity = PrefabCollectionSystem._PrefabGuidToEntityMap[prefab];
            PrefabGUID prefabGUID = prefabEntity.Read<PrefabGUID>();
            ItemData itemData = prefabEntity.Read<ItemData>();

            string prefabName = prefabGUID.LookupName();
            string tier = "";

            Match match = Regex.Match(prefabName);
            if (match.Success) tier = match.Value;
            else continue;

            if (itemData.ItemType == ItemType.Equippable)
            {
                if (IsWithinLevelRange(tier, playerLevel, EquipmentTierLevelRangeMap))
                {
                    prefabs.Add(prefabGUID);
                }
            }
            else if (itemData.ItemType == ItemType.Consumable)
            {
                if (IsConsumableWithinLevelRange(prefabName, playerLevel, ConsumableTierLevelRangeMap))
                {
                    prefabs.Add(prefabGUID);
                }
            }
        }

        return prefabs;
    }
    static HashSet<PrefabGUID> GetGatherPrefabsForLevel(int playerLevel)
    {
        HashSet<PrefabGUID> prefabs = [];
        foreach (PrefabGUID prefab in ResourcePrefabs)
        {
            Entity prefabEntity = PrefabCollectionSystem._PrefabGuidToEntityMap[prefab];

            if (prefabEntity.TryGetComponent(out EntityCategory entityCategory) && entityCategory.ResourceLevel._Value <= playerLevel)
            {
                var buffer = prefabEntity.ReadBuffer<DropTableBuffer>();

                foreach (var drop in buffer)
                {
                    if (drop.DropTrigger == DropTriggerType.YieldResourceOnDamageTaken)
                    {
                        Entity dropTable = PrefabCollectionSystem._PrefabGuidToEntityMap[drop.DropTableGuid];
                        if (!dropTable.Has<DropTableDataBuffer>()) continue;

                        var dropTableDataBuffer = dropTable.ReadBuffer<DropTableDataBuffer>();
                        foreach (DropTableDataBuffer dropTableData in dropTableDataBuffer)
                        {
                            if (dropTableData.ItemGuid.LookupName().Contains("Item_Ingredient"))
                            {
                                prefabs.Add(dropTableData.ItemGuid);
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        return prefabs;
    }
    static bool IsWithinLevelRange(string tier, int playerLevel, Dictionary<string, (int MinLevel, int MaxLevel)> tierMap)
    {
        if (tierMap.TryGetValue(tier, out var range))
        {
            return playerLevel >= range.MinLevel && playerLevel <= range.MaxLevel;
        }
        return false;
    }
    static bool IsConsumableWithinLevelRange(string prefabName, int playerLevel, Dictionary<string, (int MinLevel, int MaxLevel)> tierMap)
    {
        foreach (var kvp in tierMap)
        {
            if (prefabName.Contains(kvp.Key))
            {
                return playerLevel >= kvp.Value.MinLevel && playerLevel <= kvp.Value.MaxLevel;
            }
        }
        return false;
    }
    static QuestObjective GenerateQuestObjective(TargetType goal, HashSet<PrefabGUID> targets, int level, QuestType questType)
    {
        PrefabGUID target = new(0);
        int requiredAmount;
        int targetLevel = 0;

        switch (goal)
        {
            case TargetType.Kill:

                if (targets.Count != 0)
                {
                    target = targets.ElementAt(Random.Next(targets.Count));
                    targets.Remove(target);
                }
                else if (questType.Equals(QuestType.Daily)) target = graveyardSkeleton;
                else if (questType.Equals(QuestType.Weekly)) target = forestWolf;

                requiredAmount = Random.Next(6, 8) * QuestMultipliers[questType];

                if ((target.LookupName().ToLower().Contains("vblood") || target.LookupName().ToLower().Contains("vhunter")) && !questType.Equals(QuestType.Weekly))
                {
                    requiredAmount = 2;
                }
                else if ((target.LookupName().ToLower().Contains("vblood") || target.LookupName().ToLower().Contains("vhunter")) && questType.Equals(QuestType.Weekly))
                {
                    requiredAmount = 10;
                }

                Core.SystemService.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(target, out Entity targetEntity);
                targetLevel = targetEntity.Read<UnitLevel>().Level._Value;

                break;
            case TargetType.Craft:

                if (targets.Count != 0)
                {
                    target = targets.ElementAt(Random.Next(targets.Count));
                    targets.Remove(target);
                }
                else if (questType.Equals(QuestType.Daily)) target = reinforcedBoneSword;
                else if (questType.Equals(QuestType.Weekly)) target = reinforcedBoneMace;

                requiredAmount = Random.Next(6, 8) * QuestMultipliers[questType];

                break;
            case TargetType.Gather:

                if (targets.Count != 0)
                {
                    target = targets.ElementAt(Random.Next(targets.Count));
                    targets.Remove(target);
                }
                else if (questType.Equals(QuestType.Daily)) target = standardWood;
                else if (questType.Equals(QuestType.Weekly)) target = stone;

                List<int> amounts = [500, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000];
                requiredAmount = (int)(amounts.ElementAt(Random.Next(amounts.Count)) * QuestMultipliers[questType] * ResourceYieldModifier);

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        return new QuestObjective { Goal = goal, PrefabGUID = target, Name = target.GetPrefabName(), Level = targetLevel, RequiredAmount = requiredAmount };
    }
    static HashSet<PrefabGUID> GetGoalPrefabsForLevel(TargetType goal, int level)
    {
        HashSet<PrefabGUID> prefabs = goal switch
        {
            TargetType.Kill => GetKillPrefabsForLevel(level),
            TargetType.Craft => GetCraftPrefabsForLevel(level),
            TargetType.Gather => GetGatherPrefabsForLevel(level),
            _ => throw new ArgumentOutOfRangeException(),
        };

        return prefabs;
    }
    public static void InitializePlayerQuests(ulong steamId, int level)
    {
        List<TargetType> targetTypes = GetRandomQuestTypes();

        TargetType dailyGoal = targetTypes.First();
        TargetType weeklyGoal = targetTypes.Last();

        HashSet<PrefabGUID> dailyTargets = GetGoalPrefabsForLevel(dailyGoal, level);
        HashSet<PrefabGUID> weeklyTargets = GetGoalPrefabsForLevel(weeklyGoal, level);

        Dictionary<QuestType, (QuestObjective Objective, int Progress, DateTime LastReset)> questData = new()
            {
                { QuestType.Daily, (GenerateQuestObjective(dailyGoal, dailyTargets, level, QuestType.Daily), 0, DateTime.UtcNow) },
                { QuestType.Weekly, (GenerateQuestObjective(weeklyGoal, weeklyTargets, level, QuestType.Weekly), 0, DateTime.UtcNow) }
            };

        steamId.SetPlayerQuests(questData);
    }
    public static void RefreshQuests(User user, ulong steamId, int level)
    {
        if (steamId.TryGetPlayerQuests(out var questData))
        {
            DateTime lastDaily = questData[QuestType.Daily].LastReset;
            DateTime lastWeekly = questData[QuestType.Weekly].LastReset;

            DateTime nextDaily = lastDaily.AddDays(1);
            DateTime nextWeekly = lastWeekly.AddDays(7);

            DateTime now = DateTime.UtcNow;

            bool refreshDaily = now >= nextDaily;
            bool refreshWeekly = now >= nextWeekly;

            if (refreshDaily || refreshWeekly)
            {
                HashSet<PrefabGUID> targets;
                TargetType goal;

                if (refreshDaily && refreshWeekly)
                {
                    List<TargetType> targetTypes = GetRandomQuestTypes();

                    goal = targetTypes.First();
                    targets = GetGoalPrefabsForLevel(goal, level);

                    questData[QuestType.Daily] = (GenerateQuestObjective(goal, targets, level, QuestType.Daily), 0, now);
                    LocalizationService.HandleServerReply(EntityManager, user, "Your <color=#00FFFF>Daily Quest</color> has been refreshed~");

                    goal = targetTypes.Last();
                    targets = GetGoalPrefabsForLevel(goal, level);

                    questData[QuestType.Weekly] = (GenerateQuestObjective(goal, targets, level, QuestType.Weekly), 0, now);
                    LocalizationService.HandleServerReply(EntityManager, user, "Your <color=#BF40BF>Weekly Quest</color> has been refreshed~");
                }
                else if (refreshDaily)
                {
                    goal = GetRandomQuestType(questData, QuestType.Weekly);
                    targets = GetGoalPrefabsForLevel(goal, level);

                    questData[QuestType.Daily] = (GenerateQuestObjective(goal, targets, level, QuestType.Daily), 0, now);
                    LocalizationService.HandleServerReply(EntityManager, user, "Your <color=#00FFFF>Daily Quest</color> has been refreshed~");
                }
                else if (refreshWeekly)
                {
                    goal = GetRandomQuestType(questData, QuestType.Daily);
                    targets = GetGoalPrefabsForLevel(goal, level);

                    questData[QuestType.Weekly] = (GenerateQuestObjective(goal, targets, level, QuestType.Weekly), 0, now);
                    LocalizationService.HandleServerReply(EntityManager, user, "Your <color=#BF40BF>Weekly Quest</color> has been refreshed~");
                }

                steamId.SetPlayerQuests(questData);
            }
        }
        else
        {
            InitializePlayerQuests(steamId, level);
        }
    }
    public static void ForceRefresh(ulong steamId, int level)
    {
        List<TargetType> goals = GetRandomQuestTypes();
        TargetType dailyGoal = goals.First();
        TargetType weeklyGoal = goals.Last();

        if (steamId.TryGetPlayerQuests(out var questData))
        {
            HashSet<PrefabGUID> targets = GetGoalPrefabsForLevel(dailyGoal, level);
            questData[QuestType.Daily] = (GenerateQuestObjective(dailyGoal, targets, level, QuestType.Daily), 0, DateTime.UtcNow);

            targets = GetGoalPrefabsForLevel(weeklyGoal, level);
            questData[QuestType.Weekly] = (GenerateQuestObjective(weeklyGoal, targets, level, QuestType.Weekly), 0, DateTime.UtcNow);

            steamId.SetPlayerQuests(questData);
        }
        else
        {
            InitializePlayerQuests(steamId, level);
        }
    }
    public static void ForceDaily(User user, ulong steamId, int level)
    {
        if (steamId.TryGetPlayerQuests(out var questData))
        {
            TargetType goal = GetRandomQuestType(questData, QuestType.Weekly); // get unique goal different from weekly
            HashSet<PrefabGUID> targets = GetGoalPrefabsForLevel(goal, level);

            questData[QuestType.Daily] = (GenerateQuestObjective(goal, targets, level, QuestType.Daily), 0, DateTime.UtcNow);
            steamId.SetPlayerQuests(questData);

            //LocalizationService.HandleServerReply(EntityManager, user, "<color=#00FFFF>Daily Quest</color> has been rerolled~");
        }
    }
    public static void ForceWeekly(User user, ulong steamId, int level)
    {
        if (steamId.TryGetPlayerQuests(out var questData))
        {
            TargetType goal = GetRandomQuestType(questData, QuestType.Daily); // get unique goal different from daily
            HashSet<PrefabGUID> targets = GetGoalPrefabsForLevel(goal, level);

            questData[QuestType.Weekly] = (GenerateQuestObjective(goal, targets, level, QuestType.Weekly), 0, DateTime.UtcNow);
            steamId.SetPlayerQuests(questData);

            //LocalizationService.HandleServerReply(EntityManager, user, "Your <color=#BF40BF>Weekly Quest</color> has been rerolled~");
        }
    }
    static TargetType GetRandomQuestType(Dictionary<QuestType, (QuestObjective Objective, int Progress, DateTime LastReset)> questData, QuestType questType)
    {
        List<TargetType> targetTypes = new(TargetTypes);      
        if (questData.TryGetValue(questType, out var dailyData))
        {
            targetTypes.Remove(dailyData.Objective.Goal);
        }

        return targetTypes[Random.Next(targetTypes.Count)];
    }
    static List<TargetType> GetRandomQuestTypes()
    {
        List<TargetType> targetTypes = new(TargetTypes);

        TargetType firstGoal = targetTypes[Random.Next(targetTypes.Count)];
        targetTypes.Remove(firstGoal);

        TargetType secondGoal = targetTypes[Random.Next(targetTypes.Count)];

        return [firstGoal, secondGoal];
    }
    public static void OnUpdate(object sender, DeathEventArgs deathEvent)
    {
        List<ulong> processed = []; // may not need to check this with new event subscription stuff, will check later

        Entity source = deathEvent.Source;
        Entity died = deathEvent.Target;

        Entity userEntity = source.Read<PlayerCharacter>().UserEntity;
        PrefabGUID target = died.Read<PrefabGUID>();

        HashSet<Entity> participants = LevelingSystem.GetParticipants(source, userEntity);
        foreach (Entity participant in participants)
        {
            User user = participant.Read<PlayerCharacter>().UserEntity.Read<User>();
            ulong steamId = user.PlatformId; // participants are character entities

            if (steamId.TryGetPlayerQuests(out var questData) && !processed.Contains(steamId))
            {
                ProcessQuestProgress(questData, target, 1, user);
                processed.Add(steamId);
            }
        }
    }
    public static void ProcessQuestProgress(Dictionary<QuestType, (QuestObjective Objective, int Progress, DateTime LastReset)> questData, PrefabGUID target, int amount, User user)
    {
        bool updated = false;
        ulong steamId = user.PlatformId;

        for (int i = 0; i < questData.Count; i++)
        {
            var quest = questData.ElementAt(i);

            int targetLevel = 0;
            Core.SystemService.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(target, out Entity targetEntity);
            if (targetEntity.TryGetComponent(out UnitLevel targetUnitLevel))
            {
                targetLevel = targetUnitLevel.Level._Value;
            }
            if (quest.Value.Objective.Name == target.GetPrefabName() && quest.Value.Objective.Level <= targetLevel)
            {
                updated = true;
                string colorType = quest.Key == QuestType.Daily ? $"<color=#00FFFF>{QuestType.Daily} Quest</color>" : $"<color=#BF40BF>{QuestType.Weekly} Quest</color>";

                questData[quest.Key] = new(quest.Value.Objective, quest.Value.Progress + amount, quest.Value.LastReset);

                if (!QuestCoroutines.ContainsKey(steamId))
                {
                    QuestCoroutines[steamId] = [];
                }

                if (!QuestCoroutines[steamId].ContainsKey(quest.Key))
                {
                    var questEntry = (questData[quest.Key].Progress, true);
                    QuestCoroutines[steamId].Add(quest.Key, questEntry);

                    Core.StartCoroutine(DelayedProgressUpdate(questData, quest, user, steamId, colorType));
                }
                else
                {
                    QuestCoroutines[steamId][quest.Key] = (questData[quest.Key].Progress, true);
                }

                /*
                if (PlayerUtilities.GetPlayerBool(steamId, "QuestLogging") && !quest.Value.Objective.Complete)
                {
                    string message = $"Progress added to {colorType}: <color=green>{quest.Value.Objective.Goal}</color> <color=white>{quest.Value.Objective.Target.GetPrefabName()}</color> [<color=white>{questData[quest.Key].Progress}</color>/<color=yellow>{quest.Value.Objective.RequiredAmount}</color>]";
                    LocalizationService.HandleServerReply(EntityManager, user, message);
                }
                */

                if (quest.Value.Objective.RequiredAmount <= questData[quest.Key].Progress && !quest.Value.Objective.Complete)
                {
                    quest.Value.Objective.Complete = true;

                    LocalizationService.HandleServerReply(EntityManager, user, $"{colorType} complete!");
                    if (QuestRewards.Count > 0)
                    {
                        PrefabGUID reward = QuestRewards.Keys.ElementAt(Random.Next(QuestRewards.Count));
                        int quantity = QuestRewards[reward];

                        if (quest.Key == QuestType.Weekly) quantity *= QuestMultipliers[quest.Key];

                        if (quest.Value.Objective.Name.ToLower().Contains("vblood")) quantity *= 3;

                        if (ServerGameManager.TryAddInventoryItem(user.LocalCharacter._Entity, reward, quantity))
                        {
                            string message = $"You've received <color=#ffd9eb>{reward.GetPrefabName()}</color>x<color=white>{quantity}</color> for completing your {colorType}!";
                            LocalizationService.HandleServerReply(EntityManager, user, message);
                        }
                        else
                        {
                            InventoryUtilitiesServer.CreateDropItem(EntityManager, user.LocalCharacter._Entity, reward, quantity, new Entity());
                            string message = $"You've received <color=#ffd9eb>{reward.GetPrefabName()}</color>x<color=white>{quantity}</color> for completing your {colorType}! It dropped on the ground because your inventory was full.";
                            LocalizationService.HandleServerReply(EntityManager, user, message);
                        }

                        if (ConfigService.LevelingSystem)
                        {
                            LevelingSystem.ProcessQuestExperienceGain(user, Entity.Null, QuestMultipliers[quest.Key]);
                            string xpMessage = $"Additionally, you've been awarded <color=yellow>{(0.025f * QuestMultipliers[quest.Key] * 100).ToString("F0") + "%"}</color> of your total <color=#FFC0CB>experience</color>.";
                            LocalizationService.HandleServerReply(EntityManager, user, xpMessage);
                        }
                    }
                    else
                    {
                        LocalizationService.HandleServerReply(EntityManager, user, $"Couldn't find any valid reward prefabs...");
                    }

                    if (quest.Key == QuestType.Daily && ConfigService.InfiniteDailies)
                    {
                        int level = (ConfigService.LevelingSystem && steamId.TryGetPlayerExperience(out var data)) ? data.Key : (int)user.LocalCharacter._Entity.Read<Equipment>().GetFullLevel();
                        TargetType goal = TargetType.Kill;
                        HashSet<PrefabGUID> targets = GetGoalPrefabsForLevel(goal, level);
                        questData[QuestType.Daily] = (GenerateQuestObjective(goal, targets, level, QuestType.Daily), 0, DateTime.UtcNow);
                        var dailyQuest = questData[QuestType.Daily];
                        LocalizationService.HandleServerReply(EntityManager, user, $"New <color=#00FFFF>Daily Quest</color> available: <color=green>{dailyQuest.Objective.Goal}</color> <color=white>{dailyQuest.Objective.Name}</color>x<color=#FFC0CB>{dailyQuest.Objective.RequiredAmount}</color> [<color=white>{dailyQuest.Progress}</color>/<color=yellow>{dailyQuest.Objective.RequiredAmount}</color>]");
                    }
                }
            }
        }
        if (updated) steamId.SetPlayerQuests(questData);
    }
    static IEnumerator DelayedProgressUpdate(
    Dictionary<QuestType, (QuestObjective Objective, int Progress, DateTime LastReset)> questData,
    KeyValuePair<QuestType, (QuestObjective Objective, int Progress, DateTime LastReset)> quest,
    User user,
    ulong steamId,
    string colorType)
    {
        if (questData[quest.Key].Progress >= questData[quest.Key].Objective.RequiredAmount)
        {
            yield break;
        }

        yield return QuestMessageDelay;

        if (PlayerUtilities.GetPlayerBool(steamId, "QuestLogging") && !quest.Value.Objective.Complete)
        {
            string message = $"Progress added to {colorType}: <color=green>{quest.Value.Objective.Goal}</color> " +
                             $"<color=white>{quest.Value.Objective.Name}</color> " +
                             $"[<color=white>{questData[quest.Key].Progress}</color>/<color=yellow>{quest.Value.Objective.RequiredAmount}</color>]";

            LocalizationService.HandleServerReply(EntityManager, user, message);
        }

        QuestCoroutines[steamId].Remove(quest.Key);
        if (QuestCoroutines[steamId].Count == 0)
        {
            QuestCoroutines.Remove(steamId);
        }
    }
    public static string GetCardinalDirection(float3 direction)
    {
        float angle = math.degrees(math.atan2(direction.z, direction.x));
        if (angle < 0) angle += 360;

        if (angle >= 337.5 || angle < 22.5)
            return "East";
        else if (angle >= 22.5 && angle < 67.5)
            return "Northeast";
        else if (angle >= 67.5 && angle < 112.5)
            return "North";
        else if (angle >= 112.5 && angle < 157.5)
            return "Northwest";
        else if (angle >= 157.5 && angle < 202.5)
            return "West";
        else if (angle >= 202.5 && angle < 247.5)
            return "Southwest";
        else if (angle >= 247.5 && angle < 292.5)
            return "South";
        else
            return "Southeast";
    }
}
