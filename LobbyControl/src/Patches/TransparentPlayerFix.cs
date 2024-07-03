using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class TransparentPlayerFix
    {
        private static readonly HashSet<int> ToRespawn = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerDC))]
        [HarmonyPriority(Priority.First)]
        private static void OnPlayerDCPatch(StartOfRound __instance, int playerObjectNumber, ulong clientId)
        {
            if (!LobbyControl.PluginConfig.InvisiblePlayer.Enabled.Value)
                return;

            var controller = __instance.allPlayerScripts[playerObjectNumber];

            if (!StartOfRound.Instance.inShipPhase)
            {
                LobbyControl.Log.LogWarning($"Player {controller.playerUsername} disconnected while playing!");
                if (__instance.IsServer)
                {
                    LobbyControl.Log.LogInfo($"Player {controller.playerUsername} added to the list of Respawnables");
                    ToRespawn.Add(playerObjectNumber);
                }

                LobbyControl.Log.LogDebug($"Model of player {controller.playerUsername} has been re-enabled");
                controller.DisablePlayerModel(controller.gameObject, true, true);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
        private static void ClearDc()
        {
            ToRespawn.Clear();
        }

        //SendNewPlayerValuesServerRpc
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.__rpc_handler_2504133785))]
        private static void RespawnDcPlayer(NetworkBehaviour target, __RpcParams rpcParams)
        {
            var controllerB = target as PlayerControllerB;
            if (!LobbyControl.PluginConfig.InvisiblePlayer.Enabled.Value)
                return;

            var clientID = rpcParams.Server.Receive.SenderClientId;

            var startOfRound = StartOfRound.Instance;

            var objectId = startOfRound.ClientPlayerList[clientID];

            if (!controllerB!.IsServer || !ToRespawn.Contains(objectId))
                return;

            ToRespawn.Remove(objectId);
            
            try
            {
                var rpcList = startOfRound.ClientPlayerList.Keys.ToList();
                rpcList.Remove(clientID);
                rpcList.Remove(NetworkManager.Singleton.LocalClientId);

                var clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = rpcList
                    }
                };
                
                //Client Kill
                var bufferWriter = controllerB.__beginSendClientRpc(168339603U, clientRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, objectId);
                bufferWriter.WriteValueSafe(false);
                bufferWriter.WriteValueSafe(Vector3.zero);
                BytePacker.WriteValueBitPacked(bufferWriter, (int)CauseOfDeath.Kicking);
                BytePacker.WriteValueBitPacked(bufferWriter, 0);
                controllerB.__endSendClientRpc(ref bufferWriter, 168339603U, clientRpcParams, RpcDelivery.Reliable);
                LobbyControl.Log.LogInfo($"Player {controllerB.playerUsername} has been killed by host");
                
                //Client Respawn
                bufferWriter = startOfRound.__beginSendClientRpc(1279156295U, clientRpcParams, RpcDelivery.Reliable);
                startOfRound.__endSendClientRpc(ref bufferWriter, 1279156295U, clientRpcParams, RpcDelivery.Reliable);
                LobbyControl.Log.LogInfo($"Player {controllerB.playerUsername} has been revived on other clients");
            }
            catch (Exception ex)
            {
                LobbyControl.Log.LogError($"Exception while respawning dead players {ex}");
            }

            ToRespawn.Clear();
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        private static void ClearOnBoot(StartOfRound __instance)
        {
            if (!__instance.IsServer)
                return;

            ToRespawn.Clear();
        }
        
    }
}