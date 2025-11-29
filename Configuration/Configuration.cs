using BepInEx.Configuration;


namespace BetterTeamUpgrades.Config
{
    internal class Configuration
    {
        public static ConfigEntry<bool> EnableSharedUpgradesPatch;
        public static ConfigEntry<bool> EnableLateJoinPlayerUpdateSyncPatch;
        public static ConfigEntry<bool> EnableCustomUpgradeSyncing;

        public static void Init(ConfigFile config)
        {
            EnableSharedUpgradesPatch = config.Bind<bool>(
                "Upgrade Sync Settings",
                "EnableSharedUpgrades",
                true,
                "Enables Shared Upgrades for all supported Upgrades"
            );

            EnableLateJoinPlayerUpdateSyncPatch = config.Bind<bool>(
                "Late Join Settings",
                "EnableLateJoinPlayerUpgradeSync",
                false,
                "Enables Upgrade Sync for Late Joining Players"
            );

            EnableCustomUpgradeSyncing = config.Bind<bool>(
                "Extra Sync Settings",
                "EnableCustomUpgradeSyncing",
                true,
                "Enables Custom Upgrade Syncing for Modded Upgrades (may cause issues with some mods)"
            );
        }
    }
}