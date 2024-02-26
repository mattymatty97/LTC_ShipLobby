﻿using System;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class GhostItemFix
    {
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(PlayerControllerB),nameof(PlayerControllerB.GrabObjectClientRpc))]
        private static Exception CatchGhostItemCreation(Exception __exception, PlayerControllerB __instance, NetworkObjectReference grabbedObject)
        {
            if (!LobbyControl.PluginConfig.GhostItems.Enabled.Value)
                return __exception;
            
            if (StartOfRound.Instance.IsServer && __exception is IndexOutOfRangeException)
            {
                
                if (grabbedObject.TryGet(out var networkObject))
                {
                    networkObject.GetComponentInChildren<GrabbableObject>().heldByPlayerOnServer = false;
                    networkObject.RemoveOwnership();
                }

                if (!LobbyControl.PluginConfig.GhostItems.ForceDrop.Value)
                    return __exception;
                
                //if this did generate a ghost item force the attempting player to drop all held items :smirk:
                __instance.DropAllHeldItemsServerRpc();
                HUDManager.Instance.AddTextToChatOnServer($"{__instance.playerUsername} was forced to drop all Items!!");
                return null;
            }

            return __exception;
        }
    }
}