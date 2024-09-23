using Bloodcraft.Services;
using Bloodcraft.Systems.Professions;
using Bloodcraft.Systems.Quests;
using HarmonyLib;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using User = ProjectM.Network.User;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class CraftingSystemPatches // ForgeSystem_Update, UpdateCraftingSystem
{
    static SystemService SystemService => Core.SystemService;
    static PrefabCollectionSystem PrefabCollectionSystem => SystemService.PrefabCollectionSystem;

    static readonly float CraftRateModifier = SystemService.ServerGameSettingsSystem.Settings.CraftRateModifier;

    static readonly Dictionary<Entity, Dictionary<PrefabGUID, DateTime>> CraftCooldowns = [];

    [HarmonyPatch(typeof(ForgeSystem_Update), nameof(ForgeSystem_Update.OnUpdate))]
    [HarmonyPrefix]
    static void Prefix(ForgeSystem_Update __instance)
    {
        if (!Core.hasInitialized) return;
        if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;

        NativeArray<Entity> repairEntities = __instance.__query_1536473549_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in repairEntities)
            {
                Forge_Shared forge_Shared = entity.Read<Forge_Shared>();
                if (forge_Shared.State == ForgeState.Empty) continue;

                UserOwner userOwner = entity.Read<UserOwner>();
                Entity userEntity = userOwner.Owner._Entity;
                User user = userEntity.Read<User>();
                ulong steamId = user.PlatformId;
                Entity itemEntity = forge_Shared.ItemEntity._Entity;
                PrefabGUID itemPrefab = itemEntity.Read<PrefabGUID>();

                if (itemEntity.Has<ShatteredItem>())
                {
                    itemPrefab = itemEntity.Read<ShatteredItem>().OutputItem;
                }
                else if (itemEntity.Has<UpgradeableLegendaryItem>())
                {
                    int tier = itemEntity.Read<UpgradeableLegendaryItem>().CurrentTier;
                    var buffer = itemEntity.ReadBuffer<UpgradeableLegendaryItemTiers>();
                    itemPrefab = buffer[tier].TierPrefab;
                }

                if (forge_Shared.State == ForgeState.Finished)
                {
                    if (ConfigService.ProfessionSystem)
                    {
                        float ProfessionValue = 50f;
                        ProfessionValue *= ProfessionMappings.GetTierMultiplier(itemPrefab);
                        IProfessionHandler handler = ProfessionHandlerFactory.GetProfessionHandler(itemPrefab, "");
                        if (handler != null)
                        {
                            if (itemEntity.Has<Durability>())
                            {
                                Entity originalItem = PrefabCollectionSystem._PrefabGuidToEntityMap[itemPrefab];

                                Durability durability = itemEntity.Read<Durability>();
                                Durability originalDurability = originalItem.Read<Durability>();

                                if (durability.MaxDurability != originalDurability.MaxDurability) continue; // already handled

                                int level = handler.GetProfessionData(steamId).Key;

                                durability.MaxDurability *= 1 + level / ConfigService.MaxProfessionLevel;
                                durability.Value = durability.MaxDurability;
                                itemEntity.Write(durability);

                                ProfessionSystem.SetProfession(entity, user.LocalCharacter.GetEntityOnServer(), steamId, ProfessionValue, handler);
                            }
                        }
                    }
                    if (steamId.TryGetPlayerQuests(out var quests)) QuestSystem.ProcessQuestProgress(quests, itemPrefab, 1, user);
                }
            }
        }
        finally
        {
            repairEntities.Dispose();
        }
    }

    [HarmonyPatch(typeof(UpdateCraftingSystem), nameof(UpdateCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(UpdateCraftingSystem __instance)
    {
        if (!Core.hasInitialized) return;
        if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;
        
        NativeArray<Entity> entities = __instance.__query_1831452865_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Has<CastleWorkstation>() && entity.Has<QueuedWorkstationCraftAction>())
                {
                    var buffer = entity.ReadBuffer<QueuedWorkstationCraftAction>();

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var item = buffer[i];

                        Entity userEntity = item.InitiateUser;
                        User user = userEntity.Read<User>();
                        ulong steamId = user.PlatformId;

                        PrefabGUID recipePrefab = item.RecipeGuid;
                        Entity recipeEntity = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipePrefab) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipePrefab] : Entity.Null;

                        DateTime now = DateTime.UtcNow;

                        if (CraftCooldowns.TryGetValue(userEntity, out var cooldowns))
                        {
                            if (!cooldowns.TryGetValue(recipePrefab, out var lastCrafted))
                            {
                                cooldowns.TryAdd(recipePrefab, now);
                            }
                        }
                        else
                        {
                            CraftCooldowns.TryAdd(userEntity, new Dictionary<PrefabGUID, DateTime> { { recipePrefab, now } });
                        }

                        var outputBuffer = recipeEntity.ReadBuffer<RecipeOutputBuffer>();
                        PrefabGUID itemPrefab = outputBuffer[0].Guid;

                        if (steamId.TryGetPlayerCraftingJobs(out var playerJobs))
                        {
                            playerJobs.TryAdd(itemPrefab, 1);
                        }
                        else
                        {
                            steamId.SetPlayerCraftingJobs([]);
                            if (steamId.TryGetPlayerCraftingJobs(out playerJobs))
                            {
                                playerJobs.TryAdd(itemPrefab, 1);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}