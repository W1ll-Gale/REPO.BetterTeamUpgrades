using BetterTeamUpgrades.Config;
using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using System;

namespace BetterTeamUpgrades.Patches
{
    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    public class SharedUpgradesPatch
    {
        public static HashSet<string> VanillaKeys = new HashSet<string>();

        private static Dictionary<string, int> _preUpgradeStats = new Dictionary<string, int>();
        private static string _targetSteamID;
        private static int _targetViewID;
        private static string _targetPlayerName;

        [HarmonyPrefix]
        public static void Prefix(ItemUpgrade __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

            ItemToggle toggle = AccessTools.Field(typeof(ItemUpgrade), "itemToggle").GetValue(__instance) as ItemToggle;
            if (toggle == null || !toggle.toggleState) return;

            _targetViewID = (int)AccessTools.Field(typeof(ItemToggle), "playerTogglePhotonID").GetValue(toggle);

            PlayerAvatar avatar = SemiFunc.PlayerAvatarGetFromPhotonID(_targetViewID);
            if (avatar == null) return;

            _targetPlayerName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(avatar);
            _targetSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(avatar);
            _preUpgradeStats.Clear();

            if (StatsManager.instance != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, int>> kvp in StatsManager.instance.dictionaryOfDictionaries)
                {
                    if (kvp.Key.StartsWith("playerUpgrade"))
                    {
                        if (kvp.Value.TryGetValue(_targetSteamID, out int val))
                            _preUpgradeStats[kvp.Key] = val;
                        else
                            _preUpgradeStats[kvp.Key] = 0;
                    }
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(ItemUpgrade __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            if (string.IsNullOrEmpty(_targetSteamID)) return;
            if (PunManager.instance == null) return;

            PhotonView punView = PunManager.instance.GetComponent<PhotonView>();
            if (punView == null)
            {
                Plugin.Log.LogError("SharedUpgrades: PunManager PhotonView not found!");
                return;
            }

            Random rand = new();

            foreach (KeyValuePair<string, Dictionary<string, int>> kvp in StatsManager.instance.dictionaryOfDictionaries)
            {
                if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                int currentVal = kvp.Value.ContainsKey(_targetSteamID) ? kvp.Value[_targetSteamID] : 0;
                int preVal = _preUpgradeStats.ContainsKey(kvp.Key) ? _preUpgradeStats[kvp.Key] : 0;

                if (currentVal > preVal)
                {
                    int diff = currentVal - preVal;
                    string fullKey = kvp.Key;

                    int roll = rand.Next(0, 100);
                    if (roll >= Configuration.SharedUpgradeChange.Value)
                    {
                        Plugin.Log.LogInfo($"Skipped syncing {fullKey} due to chance roll ({roll} >= {Configuration.SharedUpgradeChange.Value})");
                        continue;
                    }

                    Plugin.Log.LogInfo($"Detected upgrade: {fullKey} (+{diff}) for {_targetPlayerName}({_targetSteamID})");

                    if (VanillaKeys.Contains(fullKey))
                    {
                        string commandName = fullKey.Substring("playerUpgrade".Length);
                        DistributeVanillaUpgrade(punView, commandName, diff);
                    }
                    else
                    {
                        if (!Configuration.EnableCustomUpgradeSyncing.Value)
                        {
                            Plugin.Log.LogInfo($"Custom Upgrade Syncing is disabled. Skipping: {fullKey}");
                            continue;
                        }
                        DistributeCustomUpgrade(punView, fullKey, currentVal);
                    }
                }
            }

            _targetSteamID = null;
            _targetViewID = -1;
            _preUpgradeStats.Clear();
        }

        private static void DistributeVanillaUpgrade(PhotonView punView, string command, int amount)
        {
            foreach (PlayerAvatar player in SemiFunc.PlayerGetAll())
            {
                if (player == null || player.photonView == null) continue;

                if (player.photonView.ViewID == _targetViewID) 
                {
                    Plugin.Log.LogInfo($"Skipping original upgrader: {command} for {_targetPlayerName}({_targetSteamID})");
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

        private static void DistributeCustomUpgrade(PhotonView punView, string dictionaryKey, int totalValue)
        {
            foreach (PlayerAvatar player in SemiFunc.PlayerGetAll())
            {
                if (player == null || player.photonView == null) continue;

                if (player.photonView.ViewID == _targetViewID)
                {
                    Plugin.Log.LogInfo($"Skipping original upgrader: {dictionaryKey} for {_targetPlayerName}({_targetSteamID})");
                    continue;
                }

                string playerName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(player);
                if (string.IsNullOrEmpty(playerName)) playerName = "Unknown";

                string playerSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(player);
                if (string.IsNullOrEmpty(playerSteamID)) continue;

                punView.RPC("UpdateStatRPC", RpcTarget.All, dictionaryKey, playerSteamID, totalValue);
                Plugin.Log.LogInfo($"Synced Custom: {dictionaryKey} for {player}({playerSteamID})");
            }
        }
    }
}
