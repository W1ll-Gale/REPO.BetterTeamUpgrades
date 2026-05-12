using HarmonyLib;
using BetterTeamUpgrades.Config;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using System;
using BepInEx.Configuration;

namespace BetterTeamUpgrades.Patches
{
    [HarmonyPatch(typeof(PlayerAvatar), "Start")]
    public class LateJoinPlayerUpgradeSyncPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

            if (
                RunManager.instance.levelCurrent == RunManager.instance.levelMainMenu ||
                RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu ||
                RunManager.instance.levelCurrent == RunManager.instance.levelRecording ||
                RunManager.instance.levelCurrent == RunManager.instance.levelSplashScreen)
            {
                return;
            }

            __instance.StartCoroutine(SyncWithDelay(__instance));
        }

        private static IEnumerator SyncWithDelay(PlayerAvatar newPlayer)
        {
            float timeWaited = 0f;
            float timeout = 10f;

            while (string.IsNullOrEmpty(SemiFunc.PlayerGetSteamID(newPlayer)) && timeWaited < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                timeWaited += 0.5f;
            }

            yield return new WaitForSeconds(1f);

            if (StatsManager.instance == null || PunManager.instance == null) yield break;

            PhotonView punView = PunManager.instance.GetComponent<PhotonView>();
            if (punView == null)
            {
                Plugin.Log.LogWarning("Late Join: PunManager PhotonView not found.");
                yield break;
            }

            string newPlayerID = SemiFunc.PlayerGetSteamID(newPlayer);

            if (string.IsNullOrEmpty(newPlayerID))
            {
                Plugin.Log.LogWarning($"Late Join: Timed out waiting for SteamID for player {newPlayer.photonView.ViewID}. Skipping sync.");
                yield break;
            }

            string playerName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(newPlayer);

            Plugin.Log.LogInfo($"Late Join: Player {playerName} ({newPlayerID}) is ready. Starting sync...");

            List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
            List<string> steamIDs = players.Select(p => SemiFunc.PlayerGetSteamID(p)).Where(id => !string.IsNullOrEmpty(id)).ToList();

            foreach (KeyValuePair<string, Dictionary<string, int>> kvp in Plugin.GetDictionaryOfDictionaries(StatsManager.instance))
            {
                if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                string fullKey = kvp.Key;
                Dictionary<string, int> upgradeDict = kvp.Value;

                bool isVanilla = SharedUpgradesPatch.VanillaKeys.Contains(fullKey);
                string section = isVanilla ? "Vanilla Upgrade Settings" : "Modded Upgrade Settings";
                string displayKey = fullKey.Replace("player", "");
                ConfigEntry<bool> toggle = Plugin.PlguinConfig.Bind<bool>(section, displayKey, true, $"Enable upgrade syncing for {fullKey}");
                if (!toggle.Value)
                {
                    Plugin.Log.LogInfo($"Late Join: Skipping {fullKey} because config toggle '{section}:{displayKey}' is disabled.");
                    continue;
                }

                if (!isVanilla && !Configuration.EnableCustomUpgradeSyncing.Value)
                {
                    Plugin.Log.LogInfo($"Late Join: Custom Upgrade Syncing is disabled. Skipping: {fullKey}");
                    continue;
                }

                int maxLevel = 0;
                foreach (string id in steamIDs)
                {
                    if (upgradeDict.TryGetValue(id, out int level))
                    {
                        if (level > maxLevel) maxLevel = level;
                    }
                }

                if (maxLevel > 0)
                {
                    foreach (string id in steamIDs)
                    {
                        int currentLevel = upgradeDict.ContainsKey(id) ? upgradeDict[id] : 0;
                        int diff = maxLevel - currentLevel;

                        if (diff > 0)
                        {
                            for (int i = 0; i < diff; i++)
                            {
                                Plugin.Log.LogInfo($"Late Join: Considering sync {fullKey} for {id} (+1)");

                                int roll = Plugin.Roll(0, 100);
                                if (roll >= Configuration.LateJoinUpgradeSyncChance.Value)
                                {
                                    Plugin.Log.LogInfo($"Late Join: Skipped syncing {fullKey} for {id} due to chance roll ({roll} >= {Configuration.LateJoinUpgradeSyncChance.Value})");
                                    continue;
                                }

                                if (isVanilla)
                                {
                                    string commandName = fullKey.Substring("playerUpgrade".Length);
                                    punView.RPC("TesterUpgradeCommandRPC", RpcTarget.Others, id, commandName, 1);

                                    if (upgradeDict.ContainsKey(id)) upgradeDict[id] += 1;
                                    else upgradeDict[id] = 1;
                                }
                                else
                                {
                                    punView.RPC("UpdateStatRPC", RpcTarget.Others, fullKey, id, currentLevel + 1);
                                    upgradeDict[id] = currentLevel + 1;
                                }

                                string pName = "Unknown";
                                PlayerAvatar pObj = players.FirstOrDefault(p => SemiFunc.PlayerGetSteamID(p) == id);
                                if (pObj != null) pName = (string)AccessTools.Field(typeof(PlayerAvatar), "playerName").GetValue(pObj);

                                Plugin.Log.LogInfo($"Late Join: Synced {fullKey} for {pName} (+1)");
                                currentLevel++;
                            }
                        }
                    }
                }
            }
            Plugin.Log.LogInfo($"Late Join: Sync complete for {playerName}.");
        }
    }
}