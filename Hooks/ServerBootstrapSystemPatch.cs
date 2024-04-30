﻿using Cobalt.Core;
using HarmonyLib;
using ProjectM;
using Stunlock.Network;
using Unity.Entities;
using static Cobalt.Systems.Bloodline.BloodMasteryStatsSystem;
using static Cobalt.Systems.Weapon.WeaponStatsSystem;
using User = ProjectM.Network.User;

namespace Cobalt.Hooks
{
    [HarmonyPatch]
    public class ServerBootstrapPatches
    {
        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
        [HarmonyPrefix]
        private static unsafe void OnUserConnectedPrefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
        {
            int userIndex = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
            ServerBootstrapSystem.ServerClient serverClient = __instance._ApprovedUsersLookup[userIndex];
            Entity userEntity = serverClient.UserEntity;
            User user = __instance.EntityManager.GetComponentData<User>(userEntity);
            ulong steamId = user.PlatformId;

            if (!DataStructures.PlayerBools.ContainsKey(steamId))
            {
                DataStructures.PlayerBools.Add(steamId, new Dictionary<string, bool>
                {
                    { "ExperienceLogging", false },
                    { "ExperienceShare", false },
                    { "ProfessionLogging", false },
                    { "FishingFlag", false },
                    { "BloodLogging", false },
                    { "CombatLogging", false }
                });
                DataStructures.SavePlayerBools();
            }

            if (!DataStructures.PlayerExperience.ContainsKey(steamId))
            {
                DataStructures.PlayerExperience.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerExperience();
            }

            if (!DataStructures.PlayerWoodcutting.ContainsKey(steamId))
            {
                DataStructures.PlayerWoodcutting.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerWoodcutting();
            }
            if (!DataStructures.PlayerMining.ContainsKey(steamId))
            {
                DataStructures.PlayerMining.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerMining();
            }
            if (!DataStructures.PlayerFishing.ContainsKey(steamId))
            {
                DataStructures.PlayerFishing.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerFishing();
            }
            if (!DataStructures.PlayerBlacksmithing.ContainsKey(steamId))
            {
                DataStructures.PlayerBlacksmithing.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerBlacksmithing();
            }
            if (!DataStructures.PlayerAlchemy.ContainsKey(steamId))
            {
                DataStructures.PlayerAlchemy.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerAlchemy();
            }
            if (!DataStructures.PlayerHarvesting.ContainsKey(steamId))
            {
                DataStructures.PlayerHarvesting.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerHarvesting();
            }
            if (!DataStructures.PlayerJewelcrafting.ContainsKey(steamId))
            {
                DataStructures.PlayerJewelcrafting.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerJewelcrafting();
            }
            if (!DataStructures.PlayerBloodMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerBloodMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerBloodMastery();
            }
            if (!DataStructures.PlayerSwordMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerSwordMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerSwordMastery();
            }
            if (!DataStructures.PlayerAxeMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerAxeMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerAxeMastery();
            }
            if (!DataStructures.PlayerMaceMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerMaceMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerMaceMastery();
            }
            if (!DataStructures.PlayerSpearMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerSpearMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerSpearMastery();
            }
            if (!DataStructures.PlayerCrossbowMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerCrossbowMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerCrossbowMastery();
            }
            if (!DataStructures.PlayerGreatSwordMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerGreatSwordMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerGreatSwordMastery();
            }
            if (!DataStructures.PlayerSlashersMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerSlashersMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerSlashersMastery();
            }
            if (!DataStructures.PlayerPistolsMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerPistolsMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerPistolsMastery();
            }
            if (!DataStructures.PlayerReaperMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerReaperMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerReaperMastery();
            }
            if (!DataStructures.PlayerLongbowMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerLongbowMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerLongbowMastery();
            }
            if (!DataStructures.PlayerWhipMastery.ContainsKey(steamId))
            {
                DataStructures.PlayerWhipMastery.Add(steamId, new KeyValuePair<int, float>(0, 0f));
                DataStructures.SavePlayerWhipMastery();
            }
            if (!DataStructures.PlayerWeaponStats.ContainsKey(steamId))
            {
                DataStructures.PlayerWeaponStats.Add(steamId, []);
                DataStructures.SavePlayerWeaponStats();
            }
            if (!DataStructures.PlayerBloodStats.ContainsKey(steamId))
            {
                DataStructures.PlayerBloodStats.Add(steamId, new BloodMasteryStats());
                DataStructures.SavePlayerBloodStats();
            }
            if (!DataStructures.PlayerCraftingJobs.ContainsKey(steamId))
            {
                DataStructures.PlayerCraftingJobs.Add(steamId, []);
            }
        }
    }
}