using BetterTeamUpgrades.Config;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BetterTeamUpgrades.Patches
{
    [HarmonyPatch(typeof(StatsManager), "Start")]
    public class StatsManagerInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(StatsManager __instance)
        {
            SharedUpgradesPatch.VanillaKeys.Clear();
            SharedUpgradesPatch.ModdedKeys.Clear();

            HashSet<string> vanillaFields = typeof(StatsManager).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.Name).ToHashSet();

            foreach (var kvp in Plugin.GetDictionaryOfDictionaries(__instance))
            {
                string key = kvp.Key;
                if (!key.StartsWith("playerUpgrade")) continue;

                string displayKey = key.Replace("player", "");

                if (vanillaFields.Contains(key))
                {
                    SharedUpgradesPatch.VanillaKeys.Add(key);

                    Plugin.PlguinConfig.Bind<bool>(
                        "Vanilla Upgrade Settings",
                        displayKey,
                        true,
                        $"Enable shared upgrade syncing for {key}"
                    );
                }
                else
                {
                    SharedUpgradesPatch.ModdedKeys.Add(key);

                    Plugin.PlguinConfig.Bind<bool>(
                        "Modded Upgrade Settings",
                        displayKey,
                        true,
                        $"Enable shared upgrade syncing for modded upgrade {key}"
                    );
                }
            }

            Plugin.Log.LogInfo($"Auto-discovered {SharedUpgradesPatch.VanillaKeys.Count} vanilla upgrade keys and {SharedUpgradesPatch.ModdedKeys.Count} modded upgrade keys.");
        }
    }
}
