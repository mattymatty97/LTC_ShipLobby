﻿using HarmonyLib;
using Unity.Netcode;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class SavePatches
    {
        
        /// <summary>
        ///     Skip saving the lobby if AutoSave is of
        ///     Write the AutoSave status to the SaveFile
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGame))]
        private static bool PreventSave(GameNetworkManager __instance)
        {
            if (LobbyControl.CanSave)
                ES3.Save("LC_SavingMethod", LobbyControl.AutoSaveEnabled, __instance.currentSaveFileName);
            return LobbyControl.CanSave;
        }

        /// <summary>
        ///     Read the AutoSave status of the current File.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
        private static void ReadCustomLobbyStatus(StartOfRound __instance)
        {
            if (__instance.IsServer)
                LobbyControl.AutoSaveEnabled = LobbyControl.CanSave = ES3.Load("LC_SavingMethod",
                    GameNetworkManager.Instance.currentSaveFileName, true);
        }
    }
}