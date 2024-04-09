using System;
using HarmonyLib;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class MoreCompanyPatches
    {
        [HarmonyPatch]
        internal class LogSpamFixes
        {
            [HarmonyFinalizer]
            [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.AddPlayerChatMessageClientRpc))]
            private static Exception CatchChatExceptions(Exception __exception)
            {
                if (LobbyControl.PluginConfig.LogSpam.Enabled.Value && 
                    LobbyControl.PluginConfig.LogSpam.MoreCompany.Value && 
                    __exception is IndexOutOfRangeException)
                {
                    LobbyControl.Log.LogWarning("Suppressed Exception from MoreCompany");
                    LobbyControl.Log.LogDebug(__exception);
                    return null;
                }

                return __exception;
            }
        }
    }
}