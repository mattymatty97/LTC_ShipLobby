using HarmonyLib;
using Object = UnityEngine.Object;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class NetworkPatcher
    {
        /// <summary>
        ///     Make the friend invite button work again once we open the lobby.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(QuickMenuManager), nameof(QuickMenuManager.InviteFriendsButton))]
        private static void FixFriendInviteButton(bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            var manager = GameNetworkManager.Instance;
            // Only do this if the game isn't doing it by itself already.
            if (GameNetworkManager.Instance.gameHasStarted && manager.currentLobby.HasValue &&
                LobbyPatcher.IsOpen(manager.currentLobby.Value))
                GameNetworkManager.Instance.InviteFriendsUI();
        }

        /// <summary>
        ///     Prevent leaving the lobby on starting the first game.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.LeaveLobbyAtGameStart))]
        private static bool PreventSteamLobbyLeaving(GameNetworkManager __instance)
        {
            LobbyControl.Log.LogDebug("Preventing the closing of Steam lobby.");
            // Do not run the method that would usually close down the lobby.
            return false;
        }

        /// <summary>
        ///     Temporarily close the lobby while a game is ongoing. This prevents people trying to join mid-game.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
        private static void CloseSteamLobby(StartOfRound __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;

            if (__instance.IsServer && __instance.inShipPhase)
            {
                LobbyControl.Log.LogDebug("Setting lobby to not joinable.");
                LobbyControl.CanModifyLobby = false;
                GameNetworkManager.Instance.SetLobbyJoinable(false);

                // Remove the friend invite button in the ESC menu.
                Object.FindObjectOfType<QuickMenuManager>().inviteFriendsTextAlpha.alpha = 0f;
            }
        }

        /// <summary>
        ///     reset the status on a new Lobby
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
        private static void ResetStatus(StartOfRound __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;

            LobbyControl.CanModifyLobby = true;
        }

        /// <summary>
        ///     Allow to reopen the steam lobby after a game has ended.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SetShipReadyToLand))]
        [HarmonyPriority(0)]
        private static void ReopenSteamLobby(StartOfRound __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;

            LobbyControl.Log.LogDebug("Lobby can be re-opened");

            LobbyControl.CanModifyLobby = true;

            if (LobbyControl.PluginConfig.SteamLobby.AutoLobby.Value)
            {
                // Restore the friend invite button in the ESC menu.
                Object.FindObjectOfType<QuickMenuManager>().inviteFriendsTextAlpha.alpha = 1f;

                var manager = GameNetworkManager.Instance;

                if (!manager.currentLobby.HasValue)
                    return;

                manager.SetLobbyJoinable(true);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerConnectedClientRpc))]
        private static void ResetDcFlags(StartOfRound __instance, ulong clientId,
            int assignedPlayerObjectId)
        {
            var controllerB = __instance.allPlayerScripts[assignedPlayerObjectId];
            controllerB.disconnectedMidGame = false;
            //re-enable the player model ( typically needed for back-filling players )
            controllerB.DisablePlayerModel(controllerB.gameObject, true, true);
        }
    }
}