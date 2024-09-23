using Bloodcraft.Services;
using Bloodcraft.Systems.Professions;
using Bloodcraft.Systems.Quests;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class ReactToInventoryChangedSystemPatch
{
    const float ProfessionBaseXP = 50f;

    [HarmonyPatch(typeof(ReactToInventoryChangedSystem), nameof(ReactToInventoryChangedSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(ReactToInventoryChangedSystem __instance)
    {
        if (!Core.hasInitialized) return;
        if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;

        NativeArray<Entity> entities = __instance.__query_2096870024_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (var entity in entities)
            {
                if (!entity.TryGetComponent(out InventoryChangedEvent inventoryChangedEvent)) continue;

                Entity inventory = inventoryChangedEvent.InventoryEntity;
                User user;
                ulong steamId;
                PrefabGUID itemPrefab = inventoryChangedEvent.Item;
                if (inventory.TryGetComponent(out InventoryConnection inventoryConnection) && inventoryChangedEvent.ChangeType.Equals(InventoryChangedEventType.Obtained))
                {
                    if (inventoryConnection.InventoryOwner.IsPlayer())
                    {
                        user = inventoryConnection.InventoryOwner.Read<PlayerCharacter>().UserEntity.Read<User>();
                        steamId = user.PlatformId;
                        
                        if (DealDamageSystemPatch.LastDamageTime.TryGetValue(steamId, out DateTime lastDamageTime) && (DateTime.UtcNow - lastDamageTime).TotalSeconds < 0.10f)
                        { 
                            if (steamId.TryGetPlayerQuests(out var quests)) QuestSystem.ProcessQuestProgress(quests, inventoryChangedEvent.Item, inventoryChangedEvent.Amount, user);
                        }
                        // Need some other method of catching onplayer crafted items since UpdateCraftingSystem doesn't seem to work with the player's inventory
                        else if (steamId.TryGetPlayerCraftingJobs(out Dictionary<PrefabGUID, int> playerJobs) && playerJobs.TryGetValue(itemPrefab, out int credits) && credits > 0)
                        {
                            if (steamId.TryGetPlayerQuests(out var quests)) QuestSystem.ProcessQuestProgress(quests, inventoryChangedEvent.Item, inventoryChangedEvent.Amount, user);
                            if (ConfigService.ProfessionSystem) HandleProfession(itemPrefab, steamId, user, inventoryConnection, inventoryChangedEvent.ItemEntity);
                        }
                        continue;
                    }
                    else if (inventoryConnection.InventoryOwner.TryGetComponent(out UserOwner userOwner))
                    {
                        Entity userEntity = userOwner.Owner._Entity;
                        Entity itemEntity = inventoryChangedEvent.ItemEntity;

                        if (itemEntity.Has<UpgradeableLegendaryItem>())
                        {
                            int tier = itemEntity.Read<UpgradeableLegendaryItem>().CurrentTier;
                            itemPrefab = itemEntity.ReadBuffer<UpgradeableLegendaryItemTiers>()[tier].TierPrefab;
                        }

                        if (!userEntity.TryGetComponent(out user)) continue;
                        steamId = user.PlatformId;

                        if (steamId.TryGetPlayerCraftingJobs(out Dictionary<PrefabGUID, int> playerJobs) && playerJobs.TryGetValue(itemPrefab, out int credits) && credits > 0)
                        {
                            credits--;

                            if (credits == 0) playerJobs.Remove(itemPrefab);
                            else playerJobs[itemPrefab] = credits;

                            if (ConfigService.ProfessionSystem) HandleProfession(itemPrefab, steamId, user, inventoryConnection, itemEntity);
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
    static void HandleProfession(PrefabGUID itemPrefab, ulong steamId, User user, InventoryConnection inventoryConnection, Entity itemEntity)
    {
        float professionXP = ProfessionBaseXP * ProfessionMappings.GetTierMultiplier(itemPrefab);
        IProfessionHandler handler = ProfessionHandlerFactory.GetProfessionHandler(itemPrefab, "");

        if (handler != null)
        {
            if (handler.GetProfessionName().Contains("Alchemy")) professionXP *= 3;
            if (steamId.TryGetPlayerQuests(out var quests)) QuestSystem.ProcessQuestProgress(quests, itemPrefab, 1, user);

            ProfessionSystem.SetProfession(inventoryConnection.InventoryOwner, user.LocalCharacter.GetEntityOnServer(), steamId, professionXP, handler);
            switch (handler)
            {
                case BlacksmithingHandler:
                    if (itemEntity.Has<Durability>())
                    {
                        Durability durability = itemEntity.Read<Durability>();
                        int level = handler.GetProfessionData(steamId).Key;
                        durability.MaxDurability *= 1 + level / ConfigService.MaxProfessionLevel;
                        durability.Value = durability.MaxDurability;
                        itemEntity.Write(durability);
                    }
                    //EquipmentManager.ApplyEquipmentStats(steamId, itemEntity);
                    break;
                case AlchemyHandler:
                    break;
                case EnchantingHandler:
                    if (itemEntity.Has<Durability>())
                    {
                        Durability durability = itemEntity.Read<Durability>();
                        int level = handler.GetProfessionData(steamId).Key;
                        durability.MaxDurability *= 1 + level / ConfigService.MaxProfessionLevel;
                        durability.Value = durability.MaxDurability;
                        itemEntity.Write(durability);
                    }
                    //EquipmentManager.ApplyEquipmentStats(steamId, itemEntity);
                    break;
                case TailoringHandler:
                    if (itemEntity.Has<Durability>())
                    {
                        Durability durability = itemEntity.Read<Durability>();
                        int level = handler.GetProfessionData(steamId).Key;
                        durability.MaxDurability *= 1 + level / ConfigService.MaxProfessionLevel;
                        durability.Value = durability.MaxDurability;
                        itemEntity.Write(durability);
                    }
                    //EquipmentManager.ApplyEquipmentStats(steamId, itemEntity);
                    break;
            }
        }
    }
}
