using HarmonyLib;
using LobbyControl.Dependency;

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
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGameValues))]
        private static bool PreventSave(GameNetworkManager __instance)
        {
            if (LobbyControl.CanSave && __instance.isHostingGame)
            {
                ES3.Save("LC_SavingMethod", LobbyControl.AutoSaveEnabled, __instance.currentSaveFileName);
                if (AsyncLoggerProxy.Enabled)
                    AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Save", $"Saving {GameNetworkManager.Instance.currentSaveFileName}");
            }
            
            return LobbyControl.CanSave;
        }

        /// <summary>
        ///     Skip saving the lobby if AutoSave is of
        ///     Write the AutoSave status to the SaveFile
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues))]
        private static bool PreventSaveFileReset(GameNetworkManager __instance)
        {
            return LobbyControl.CanSave;
        }
        
        /// <summary>
        ///     Read the AutoSave status of the current File.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
        private static void ReadCustomLobbyStatus(StartOfRound __instance, bool __runOriginal)
        {
            if (!__runOriginal || !__instance.IsServer)
                return;

            LobbyControl.AutoSaveEnabled = LobbyControl.CanSave = ES3.Load("LC_SavingMethod",
                GameNetworkManager.Instance.currentSaveFileName, true);
            
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Autosave", LobbyControl.CanSave.ToString());
        }
    }
}