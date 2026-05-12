using BetterTeamUpgrades.Config;
using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;

namespace BetterTeamUpgrades.Patches
{
    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    public class SharedUpgradesPatch
    {
        public static HashSet<string> VanillaKeys = new HashSet<string>();
        public static HashSet<string> ModdedKeys = new HashSet<string>();

        public struct UpgradeContext
        {
            public string SteamID;
            public int ViewID;
            public string PlayerName;
            public Dictionary<string, int> PreUpgradeStats;
        }

        [HarmonyPrefix]
        public static void Prefix(ItemUpgrade __instance, out UpgradeContext __state)
        {
            __state = default;

            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

            ItemToggle toggle = AccessTools.Field(typeof(ItemUpgrade), "itemToggle").GetValue(__instance) as ItemToggle;
            if (toggle == null || !toggle.toggleState) return;

            int targetViewID = (int)AccessTools.Field(typeof(ItemToggle), "playerTogglePhotonID").GetValue(toggle);

            PlayerAvatar avatar = SemiFunc.PlayerAvatarGetFromPhotonID(targetViewID);
            if (avatar == null) return;

            string targetPlayerName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(avatar);
            string targetSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(avatar);

            Dictionary<string, int> preUpgradeStats = new Dictionary<string, int>();

            if (StatsManager.instance != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, int>> kvp in Plugin.GetDictionaryOfDictionaries(StatsManager.instance))
                {
                    if (kvp.Key.StartsWith("playerUpgrade"))
                    {
                        if (kvp.Value.TryGetValue(targetSteamID, out int val))
                        {
                            preUpgradeStats[kvp.Key] = val;
                        }
                        else
                        {
                            preUpgradeStats[kvp.Key] = 0;
                        }
                    }
                }
            }

            __state = new UpgradeContext
            {
                SteamID = targetSteamID,
                ViewID = targetViewID,
                PlayerName = targetPlayerName,
                PreUpgradeStats = preUpgradeStats
            };
        }

        [HarmonyPostfix]
        public static void Postfix(ItemUpgrade __instance, UpgradeContext __state)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            if (string.IsNullOrEmpty(__state.SteamID)) return;
            if (PunManager.instance == null) return;

            PhotonView punView = PunManager.instance.GetComponent<PhotonView>();
            if (punView == null)
            {
                Plugin.Log.LogError("SharedUpgrades: PunManager PhotonView not found!");
                return;
            }

            foreach (KeyValuePair<string, Dictionary<string, int>> kvp in Plugin.GetDictionaryOfDictionaries(StatsManager.instance))
            {
                if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                bool isVanilla = VanillaKeys.Contains(kvp.Key);
                string section = isVanilla ? "Vanilla Upgrade Settings" : "Modded Upgrade Settings";
                string displayKey = kvp.Key.Replace("player", "");
                ConfigEntry<bool> toggle = Plugin.PlguinConfig.Bind<bool>(section, displayKey, true, $"Enable shared upgrade syncing for {kvp.Key}");
                if (!toggle.Value)
                {
                    Plugin.Log.LogInfo($"SharedUpgrades: Skipping {kvp.Key} because config toggle '{section}:{displayKey}' is disabled.");
                    continue;
                }

                int currentVal = kvp.Value.ContainsKey(__state.SteamID) ? kvp.Value[__state.SteamID] : 0;
                int preVal = __state.PreUpgradeStats.ContainsKey(kvp.Key) ? __state.PreUpgradeStats[kvp.Key] : 0;

                if (currentVal > preVal)
                {
                    int diff = currentVal - preVal;
                    string fullKey = kvp.Key;

                    Plugin.Log.LogInfo($"Detected upgrade: {fullKey} (+{diff}) for {__state.PlayerName}({__state.SteamID})");

                    int roll = Plugin.Roll(0, 100);
                    if (roll >= Configuration.SharedUpgradeChance.Value)
                    {
                        Plugin.Log.LogInfo($"Skipped syncing {fullKey} due to chance roll ({roll} >= {Configuration.SharedUpgradeChance.Value})");
                        continue;
                    }

                    if (isVanilla)
                    {
                        string commandName = fullKey.Substring("playerUpgrade".Length);
                        DistributeVanillaUpgrade(punView, commandName, diff, __state);
                    }
                    else
                    {
                        if (!Configuration.EnableCustomUpgradeSyncing.Value)
                        {
                            Plugin.Log.LogInfo($"Custom Upgrade Syncing is disabled. Skipping: {fullKey}");
                            continue;
                        }
                        DistributeCustomUpgrade(punView, fullKey, currentVal, __state);
                    }
                }
            }
        }

        private static void DistributeVanillaUpgrade(PhotonView punView, string command, int amount, UpgradeContext context)
        {
            foreach (PlayerAvatar player in SemiFunc.PlayerGetAll())
            {
                if (player == null || player.photonView == null) continue;

                if (player.photonView.ViewID == context.ViewID)
                {
                    Plugin.Log.LogInfo($"Skipping original upgrader: {command} for {context.PlayerName}({context.SteamID})");
                    continue;
                }

                string playerName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(player);
                if (string.IsNullOrEmpty(playerName)) playerName = "Unknown";

                string playerSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(player);
                if (string.IsNullOrEmpty(playerSteamID)) continue;

                punView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, playerSteamID, command, amount);
                Plugin.Log.LogInfo($"Synced Vanilla: {command} for {playerName}({playerSteamID})");
            }
        }

        private static void DistributeCustomUpgrade(PhotonView punView, string dictionaryKey, int totalValue, UpgradeContext context)
        {
            foreach (PlayerAvatar player in SemiFunc.PlayerGetAll())
            {
                if (player == null || player.photonView == null) continue;

                if (player.photonView.ViewID == context.ViewID)
                {
                    Plugin.Log.LogInfo($"Skipping original upgrader: {dictionaryKey} for {context.PlayerName}({context.SteamID})");
                    continue;
                }

                string playerName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(player);
                if (string.IsNullOrEmpty(playerName)) playerName = "Unknown";

                string playerSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(player);
                if (string.IsNullOrEmpty(playerSteamID)) continue;

                punView.RPC("UpdateStatRPC", RpcTarget.All, dictionaryKey, playerSteamID, totalValue);
                Plugin.Log.LogInfo($"Synced Custom: {dictionaryKey} for {playerName}({playerSteamID})");
            }
        }
    }
}
