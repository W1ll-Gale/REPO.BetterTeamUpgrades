using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BetterTeamUpgrades.Config;
using BetterTeamUpgrades.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BetterTeamUpgrades
{
    [BepInPlugin(mod_guid, mod_name, mod_version)]
    public class Plugin : BaseUnityPlugin
    {
        private const string mod_guid = "MrBytesized.REPO.BetterTeamUpgrades";
        private const string mod_name = "Better Team Upgrades";
        private const string mod_version = "2.2.1";

        private readonly Harmony harmony = new Harmony(mod_guid);

        private static Plugin instance;

        internal static ManualLogSource Log;

        private (ConfigEntry<bool> configEntry, Action enablePatch, Action disablePatch, string description)[] _patchArray;
        public static ConfigFile PlguinConfig;

        internal static readonly object RandomLock = new object();
        internal static readonly System.Random Random = new System.Random();

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            Log = BepInEx.Logging.Logger.CreateLogSource(mod_guid);
            PlguinConfig = Config;

            harmony.PatchAll(typeof(StatsManagerInitPatch));

            Configuration.Init(PlguinConfig);

            _patchArray = new (ConfigEntry<bool>, Action, Action, string)[]
            {
                (
                    Configuration.EnableSharedUpgradesPatch,
                    () => harmony.PatchAll(typeof(SharedUpgradesPatch)),
                    () => harmony.UnpatchSelf(typeof(SharedUpgradesPatch)),
                    "Shared Upgrades"
                ),
                (
                    Configuration.EnableLateJoinPlayerUpdateSyncPatch,
                    () => harmony.PatchAll(typeof(LateJoinPlayerUpgradeSyncPatch)),
                    () => harmony.UnpatchSelf(typeof(LateJoinPlayerUpgradeSyncPatch)),
                    "Late Join Player Upgrade Sync"
                )
            };

            foreach (var (configEntry, enablePatch, disablePatch, description) in _patchArray)
            {
                UpdatePatchFromConfig(configEntry, enablePatch, disablePatch, description);
                configEntry.SettingChanged += (sender, args) => UpdatePatchFromConfig(configEntry, enablePatch, disablePatch, description);
            }

            Log.LogInfo("Better Team Upgrades mod has been activated");
        }

        private void UpdatePatchFromConfig(
            ConfigEntry<bool> configEntry,
            Action enablePatch,
            Action disablePatch,
            string description)
        {
            if (configEntry.Value)
            {
                try
                {
                    enablePatch.Invoke();
                    Log.LogInfo($"{description} patch enabled.");
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to enable {description}: {e.Message}");
                }
            }
            else
            {
                try
                {
                    disablePatch.Invoke();
                    Log.LogInfo($"{description} patch disabled.");
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to disable {description}: {e.Message}");
                }
            }
        }

        internal static int Roll(int min, int max)
        {
            lock (Plugin.RandomLock)
            {
                return Plugin.Random.Next(min, max);
            }
        }

        public static IEnumerable<KeyValuePair<string, Dictionary<string, int>>> GetDictionaryOfDictionaries(StatsManager instance)
        {
            if (instance == null) return Enumerable.Empty<KeyValuePair<string, Dictionary<string, int>>>();

            FieldInfo field = AccessTools.Field(typeof(StatsManager), "dictionaryOfDictionaries");
            if (field == null)
            {
                Log.LogError("StatsManager.dictionaryOfDictionaries field not found via reflection!");
                return Enumerable.Empty<KeyValuePair<string, Dictionary<string, int>>>();
            }

            Object value = field.GetValue(instance);
            if (value == null) return Enumerable.Empty<KeyValuePair<string, Dictionary<string, int>>>();

            return (IEnumerable<KeyValuePair<string, Dictionary<string, int>>>)value;
        }
    }

    public static class HarmonyExtensions
    {
        public static void UnpatchSelf(this Harmony harmony, Type patchClass)
        {
            HarmonyPatch[] classLevelPatches = patchClass.GetCustomAttributes(typeof(HarmonyPatch), true).OfType<HarmonyPatch>().ToArray();

            foreach (HarmonyPatch patch in classLevelPatches)
            {
                HarmonyMethod patchMethodInfo = patch.info;
                if (patchMethodInfo == null)
                {
                    Plugin.Log.LogWarning($"Invalid HarmonyPatch method info on class: {patchClass.FullName}");
                    continue;
                }

                MethodInfo original = ResolveOriginal(patchMethodInfo);
                if (original == null)
                {
                    Plugin.Log.LogWarning($"Original method not found for class patch: {FormatInfo(patchMethodInfo)}");
                    continue;
                }

                harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
            }

            MethodInfo[] methods = patchClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                HarmonyPatch[] methodPatches = method.GetCustomAttributes(typeof(HarmonyPatch), true).OfType<HarmonyPatch>().ToArray();

                if (methodPatches.Length == 0)
                {
                    continue;
                }

                foreach (HarmonyPatch patch in methodPatches)
                {
                    HarmonyMethod patchMethodInfo = patch.info;
                    if (patchMethodInfo == null)
                    {
                        Plugin.Log.LogWarning($"Invalid HarmonyPatch info on method: {method.DeclaringType.FullName}.{method.Name}");
                        continue;
                    }

                    MethodInfo original = ResolveOriginal(patchMethodInfo);
                    if (original == null)
                    {
                        Plugin.Log.LogWarning($"Original method not found for method-level patch: {FormatInfo(patchMethodInfo)}");
                        continue;
                    }

                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
        }

        private static MethodInfo ResolveOriginal(HarmonyMethod info)
        {
            if (info.method != null) return info.method;

            if (info.declaringType == null || string.IsNullOrEmpty(info.methodName))
                return null;

            return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
        }

        private static string FormatInfo(HarmonyMethod info)
        {
            string typeName = info.declaringType != null ? info.declaringType.FullName : "<null>";
            string methodName = !string.IsNullOrEmpty(info.methodName) ? info.methodName : "<null>";
            return $"{typeName}.{methodName}";
        }
    }
}