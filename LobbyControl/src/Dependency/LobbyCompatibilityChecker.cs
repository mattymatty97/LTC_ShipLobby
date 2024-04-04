using System;
using System.Runtime.CompilerServices;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;

namespace LobbyControl.Dependency
{
    public static class LobbyCompatibilityChecker
    {
        public static bool Enabled { get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"); } }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init(string GUID, Version version, int Level, int strictness)
        {
            PluginHelper.RegisterPlugin(GUID, version, (CompatibilityLevel)Level, (VersionStrictness)strictness);
        }
        
    }
}