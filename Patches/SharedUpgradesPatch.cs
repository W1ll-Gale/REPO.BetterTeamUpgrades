using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;
using BetterTeamUpgrades.Config;

namespace BetterTeamUpgrades.Patches
{
    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    public class SharedUpgradesPatch
    {
        public static HashSet<string> VanillaKeys = new HashSet<string>();

        private static Dictionary<string, int> _preUpgradeStats = new Dictionary<string, int>();
        private static string _targetSteamID;
        private static int _targetViewID;

        [HarmonyPrefix]
        public static void Prefix(ItemUpgrade __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

            ItemToggle toggle = AccessTools.Field(typeof(ItemUpgrade), "itemToggle").GetValue(__instance) as ItemToggle;
            if (toggle == null || !toggle.toggleState) return;

            _targetViewID = (int)AccessTools.Field(typeof(ItemToggle), "playerTogglePhotonID").GetValue(toggle);

            PlayerAvatar avatar = SemiFunc.PlayerAvatarGetFromPhotonID(_targetViewID);
            if (avatar == null) return;

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

            foreach (KeyValuePair<string, Dictionary<string, int>> kvp in StatsManager.instance.dictionaryOfDictionaries)
            {
                if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                int currentVal = kvp.Value.ContainsKey(_targetSteamID) ? kvp.Value[_targetSteamID] : 0;
                int preVal = _preUpgradeStats.ContainsKey(kvp.Key) ? _preUpgradeStats[kvp.Key] : 0;

                if (currentVal > preVal)
                {
                    int diff = currentVal - preVal;
                    string fullKey = kvp.Key;

                    Plugin.Log.LogInfo($"Detected upgrade: {fullKey} (+{diff}) for {_targetSteamID}");

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
            List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
            foreach (var avatar in players)
            {
                if (avatar == null || avatar.photonView == null) continue;
                if (avatar.photonView.ViewID == _targetViewID) 
                {
                    Plugin.Log.LogInfo($"Skipping original upgrader: {command} for {_targetSteamID}");
                    continue; 
                }

                string pSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(avatar);
                if (string.IsNullOrEmpty(pSteamID)) continue;

                punView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, pSteamID, command, amount);
                Plugin.Log.LogInfo($"Synced Vanilla: {command} for {pSteamID}");
            }
        }

        private static void DistributeCustomUpgrade(PhotonView punView, string dictionaryKey, int totalValue)
        {
            List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
            foreach (var avatar in players)
            {
                if (avatar == null || avatar.photonView == null) continue;
                if (avatar.photonView.ViewID == _targetViewID)
                {
                    Plugin.Log.LogInfo($"Skipping original upgrader: {dictionaryKey} for {_targetSteamID}");
                    continue;
                }

                string pSteamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(avatar);
                if (string.IsNullOrEmpty(pSteamID)) continue;

                punView.RPC("UpdateStatRPC", RpcTarget.All, dictionaryKey, pSteamID, totalValue);
                Plugin.Log.LogInfo($"Synced Custom: {dictionaryKey} for {pSteamID}");
            }
        }
    }
}