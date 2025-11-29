using HarmonyLib;
using BetterTeamUpgrades.Config;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

namespace BetterTeamUpgrades.Patches
{
    [HarmonyPatch(typeof(PlayerAvatar), "Start")]
    public class LateJoinPlayerUpgradeSyncPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

            RunManager rm = RunManager.instance;
            if (rm == null ||
                rm.levelCurrent == rm.levelMainMenu ||
                rm.levelCurrent == rm.levelRecording ||
                rm.levelCurrent == rm.levelSplashScreen)
            {
                return;
            }

            __instance.StartCoroutine(SyncWithDelay());
        }

        private static IEnumerator SyncWithDelay()
        {
            yield return new WaitForSeconds(2f);

            if (StatsManager.instance == null || PunManager.instance == null) yield break;

            PhotonView punView = PunManager.instance.GetComponent<PhotonView>();
            if (punView == null)
            {
                Plugin.Log.LogWarning("Late Join: PunManager PhotonView not found.");
                yield break;
            }

            List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
            if (players == null || !players.Any()) yield break;

            List<string> steamIDs = players
                .Select(p => SemiFunc.PlayerGetSteamID(p))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            foreach (KeyValuePair<string, Dictionary<string, int>> kvp in StatsManager.instance.dictionaryOfDictionaries)
            {
                if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                string fullKey = kvp.Key;
                Dictionary<string, int> upgradeDict = kvp.Value;

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
                    bool isVanilla = SharedUpgradesPatch.VanillaKeys.Contains(fullKey);

                    foreach (string id in steamIDs)
                    {
                        int currentLevel = upgradeDict.ContainsKey(id) ? upgradeDict[id] : 0;
                        int diff = maxLevel - currentLevel;

                        if (diff > 0)
                        {
                            if (isVanilla)
                            {
                                string commandName = fullKey.Substring("playerUpgrade".Length);
                                punView.RPC("TesterUpgradeCommandRPC", RpcTarget.Others, id, commandName, diff);

                                if (upgradeDict.ContainsKey(id)) upgradeDict[id] += diff;
                                else upgradeDict[id] = diff;
                            }
                            else
                            {
                                if(!Configuration.EnableCustomUpgradeSyncing.Value)
                                {
                                    Plugin.Log.LogInfo($"Late Join: Skipped syncing custom upgrade {fullKey} for {id} (Custom Sync Disabled)");
                                    continue;
                                }

                                punView.RPC("UpdateStatRPC", RpcTarget.Others, fullKey, id, maxLevel);

                                upgradeDict[id] = maxLevel;
                            }

                            Plugin.Log.LogInfo($"Late Join: Synced {fullKey} for {id} (Safe Mode: {!isVanilla})");
                        }
                    }
                }
            }
            Plugin.Log.LogInfo("Late Join: Upgrade sync completed.");
        }
    }
}