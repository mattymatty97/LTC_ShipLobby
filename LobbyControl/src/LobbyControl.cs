using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LobbyControl.Dependency;
using LobbyControl.Patches;
using LobbyControl.PopUp;
using LobbyControl.TerminalCommands;
using MonoMod.RuntimeDetour;
using PluginInfo = BepInEx.PluginInfo;

namespace LobbyControl
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.github.tinyhoot.ShipLobby", Flags: BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("twig.latecompany", Flags: BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.potatoepet.AdvancedCompany", Flags: BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("FlipMods.ReservedItemSlotCore", Flags: BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BMX.LobbyCompatibility", Flags: BepInDependency.DependencyFlags.SoftDependency)]
    internal class LobbyControl : BaseUnityPlugin
    {
        public const string GUID = "mattymatty.LobbyControl";
        public const string NAME = "LobbyControl";
        public const string VERSION = "2.4.5";

        internal static ManualLogSource Log;

        internal static Harmony _harmony;

        public static bool CanModifyLobby = true;

        public static bool CanSave = true;
        public static bool AutoSaveEnabled = true;
        public static readonly List<Hook> Hooks = new List<Hook>();


        private static readonly string[] IncompatibleGUIDs = new string[]
        {
            "com.github.tinyhoot.ShipLobby",
            "twig.latecompany",
            "com.potatoepet.AdvancedCompany"
        };

        internal static readonly List<PluginInfo> FoundIncompatibilities = new List<PluginInfo>();

        private void Awake()
        {
            Log = Logger;
            try
            {
                PluginInfo[] incompatibleMods = Chainloader.PluginInfos.Values
                    .Where(p => IncompatibleGUIDs.Contains(p.Metadata.GUID)).ToArray();
                if (incompatibleMods.Length > 0)
                {
                    StringBuilder sb = new StringBuilder("LOBBY CONTROL was DISABLED!\nIncompatible:");
                    FoundIncompatibilities.AddRange(incompatibleMods);
                    foreach (var mod in incompatibleMods)
                    {
                        Log.LogWarning($"{mod.Metadata.Name} is incompatible!");
                        sb.Append("\n").Append(mod.Metadata.Name);
                    }

                    Log.LogError($"{incompatibleMods.Length} incompatible mods found! Disabling!");
                    var harmony = new Harmony(GUID);
                    PopUpPatch.PopUps.Add(new Tuple<string, string>("LC_Incompatibility", sb.ToString()));
                    harmony.PatchAll(typeof(PopUpPatch));
                }
                else
                {
                    if (LobbyCompatibilityChecker.Enabled)
                        LobbyCompatibilityChecker.Init(GUID, Version.Parse(VERSION), 1, 2);
                    Log.LogInfo("Initializing Configs");

                    PluginConfig.Init(this);

                    CommandManager.Initialize();

                    Log.LogInfo("Patching Methods");
                    _harmony = new Harmony(GUID);
                    _harmony.PatchAll(Assembly.GetExecutingAssembly());
                    JoinPatches.Init();
                    TransparentPlayerFix.Init();

                    Log.LogInfo(NAME + " v" + VERSION + " Loaded!");
                }
            }
            catch (Exception ex)
            {
                Log.LogError("Exception while initializing: \n" + ex);
            }
        }

        internal static class PluginConfig
        {
            internal static void Init(BaseUnityPlugin plugin)
            {
                var config = plugin.Config;
                config.SaveOnConfigSet = false;
                //Initialize Configs
                //ItemSync
                ItemSync.GhostItems = config.Bind("ItemSync", "ghost_items", true
                    , "prevent the creation of non-grabbable items in case of inventory desync");
                ItemSync.ForceDrop = config.Bind("ItemSync", "force_drop", true
                    , "forcefully drop all items of the player causing the desync");
                ItemSync.ShotGunReload = config.Bind("ItemSync", "shotgun_reload", true
                    , "prevent the shotgun disappearing when reloading it");
                ItemSync.SyncOnUse = config.Bind("ItemSync", "sync_on_use", false
                    , "sync held object upon usage");
                ItemSync.SyncOnInteract = config.Bind("ItemSync", "sync_on_interact", false
                    , "sync held object upon interaction");
                ItemSync.SyncIgnoreName = config.Bind("ItemSync", "sync_ignore_name",
                    "Flashlight,Pro-flashlight,Laser pointer"
                    , "do not attempt sync on items that are in the list (compatibility with FlashLight toggle, ecc)");
                //SaveLimit
                SaveLimit.Enabled = config.Bind("SaveLimit", "enabled", true
                    , "remove the limit to the amount of items that can be saved");
                //InvisiblePlayer
                InvisiblePlayer.Enabled = config.Bind("InvisiblePlayer", "enabled", true
                    , "attempts to fix late joining players appearing invisible to the rest of the lobby");
                //SteamLobby
                SteamLobby.AutoLobby = config.Bind("SteamLobby", "auto_lobby", false
                    , "automatically reopen the lobby as soon as you reach orbit");
                SteamLobby.RadarFix = config.Bind("SteamLobby", "radar_fix", true
                    , "fix mismatched radar names if a radar booster was activated during the play session");
                //LogSpam
                LogSpam.Enabled = config.Bind("LogSpam", "enabled", true
                    , "prevent some annoying log spam");
                LogSpam.CalculatePolygonPath = config.Bind("LogSpam", "CalculatePolygonPath", true
                    , "stop pathfinding for dead Enemies");
                LogSpam.AudioSpatializer = config.Bind("LogSpam", "audio_spatializer", true
                    , "disable audio spatialization as there is not spatialization plugin");
                //JoinQueue
                JoinQueue.Enabled = config.Bind("JoinQueue", "enabled", true
                    , "handle joining players as a queue instead of at the same time");
                JoinQueue.ConnectionTimeout = config.Bind("JoinQueue", "connection_timeout_ms", 3000
                    , "After how much time discard a hanging connection");
                JoinQueue.ConnectionDelay = config.Bind("JoinQueue", "connection_delay_ms", 500
                    , "Delay between each successful connection");

                //remove unused options
                PropertyInfo orphanedEntriesProp = config.GetType()
                    .GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

                var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

                orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
                config.Save(); // Save the config file
            }

            internal static class SteamLobby
            {
                internal static ConfigEntry<bool> AutoLobby;
                internal static ConfigEntry<bool> RadarFix;
            }

            internal static class ItemSync
            {
                internal static ConfigEntry<bool> GhostItems;
                internal static ConfigEntry<bool> ForceDrop;
                internal static ConfigEntry<bool> ShotGunReload;
                internal static ConfigEntry<bool> SyncOnUse;
                internal static ConfigEntry<bool> SyncOnInteract;
                internal static ConfigEntry<string> SyncIgnoreName;
            }

            internal static class SaveLimit
            {
                internal static ConfigEntry<bool> Enabled;
            }

            internal static class InvisiblePlayer
            {
                internal static ConfigEntry<bool> Enabled;
            }

            internal static class LogSpam
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<bool> CalculatePolygonPath;
                internal static ConfigEntry<bool> AudioSpatializer;
            }

            internal static class JoinQueue
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<int> ConnectionTimeout;
                internal static ConfigEntry<int> ConnectionDelay;
            }
        }
    }
}