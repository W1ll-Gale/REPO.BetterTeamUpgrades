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

            HashSet<string> vanillaFields = typeof(StatsManager)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(f => f.Name)
                .ToHashSet();

            foreach (string key in __instance.dictionaryOfDictionaries.Keys)
            {
                if (key.StartsWith("playerUpgrade"))
                {
                    if (vanillaFields.Contains(key))
                    {
                        SharedUpgradesPatch.VanillaKeys.Add(key);
                    }
                }
            }
            Plugin.Log.LogInfo($"Auto-discovered {SharedUpgradesPatch.VanillaKeys.Count} vanilla upgrade keys.");
        }
    }
}
