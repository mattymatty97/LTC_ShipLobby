using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using Mono.Cecil;
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


        private static uint? _killPlayerID;
        private static uint? _revivePlayerID;

        [HarmonyReversePatch(HarmonyReversePatchType.Original)]
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerClientRpc))]
        private static void StubKillPlayer()
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();

                var methodInfo = typeof(NetworkBehaviour).GetMethod(nameof(NetworkBehaviour.__beginSendClientRpc), BindingFlags.Instance | BindingFlags.NonPublic);
                
                var matcher = new CodeMatcher(codes);

                matcher.MatchForward(true, new CodeMatch(OpCodes.Call, methodInfo));

                if (matcher.IsInvalid)
                {
                    LobbyControl.Log.LogFatal("KillPlayerClientRpc match 1 failed!!");
                    return [];
                }

                matcher.MatchBack(true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldc_I4));
                
                if (matcher.IsInvalid)
                {
                    LobbyControl.Log.LogFatal($"KillPlayerClientRpc match 2 failed!!");
                    return [];
                }
                
                _killPlayerID = (uint)(int)matcher.Operand;
                LobbyControl.Log.LogWarning($"KillPlayerClientRpc ID found: {_killPlayerID}U");

                return [];
            }

            _ = Transpiler(null);
        }
        
        [HarmonyReversePatch(HarmonyReversePatchType.Original)]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Debug_ReviveAllPlayersClientRpc))]
        private static void StubRevivePlayers()
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();

                var methodInfo = typeof(NetworkBehaviour).GetMethod(nameof(NetworkBehaviour.__beginSendClientRpc), BindingFlags.Instance | BindingFlags.NonPublic);
                
                var matcher = new CodeMatcher(codes);

                matcher.MatchForward(true, new CodeMatch(OpCodes.Call, methodInfo));

                if (matcher.IsInvalid)
                {
                    LobbyControl.Log.LogFatal("Debug_ReviveAllPlayersClientRpc match 1 failed!!");
                    return [];
                }

                matcher.MatchBack(true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldc_I4));
                
                if (matcher.IsInvalid)
                {
                    LobbyControl.Log.LogFatal($"Debug_ReviveAllPlayersClientRpc match 2 failed!!");
                    return [];
                }
                
                _revivePlayerID = (uint)(int)matcher.Operand;
                LobbyControl.Log.LogWarning($"Debug_ReviveAllPlayersClientRpc ID found: {_revivePlayerID}U");

                return [];
            }

            _ = Transpiler(null);
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
            
            if (!StartOfRound.Instance.inShipPhase)
                return;
            
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
                var bufferWriter = controllerB.__beginSendClientRpc(_killPlayerID!.Value, clientRpcParams, RpcDelivery.Reliable);
                BytePacker.WriteValueBitPacked(bufferWriter, objectId);
                bufferWriter.WriteValueSafe(false);
                bufferWriter.WriteValueSafe(Vector3.zero);
                BytePacker.WriteValueBitPacked(bufferWriter, (int)CauseOfDeath.Unknown);
                BytePacker.WriteValueBitPacked(bufferWriter, 0);
                bufferWriter.WriteValueSafe(Vector3.zero);
                controllerB.__endSendClientRpc(ref bufferWriter, _killPlayerID!.Value, clientRpcParams, RpcDelivery.Reliable);
                LobbyControl.Log.LogInfo($"Player {controllerB.playerUsername} has been killed by host");
                
                //Client Respawn
                bufferWriter = startOfRound.__beginSendClientRpc(_revivePlayerID!.Value, clientRpcParams, RpcDelivery.Reliable);
                startOfRound.__endSendClientRpc(ref bufferWriter, _revivePlayerID!.Value, clientRpcParams, RpcDelivery.Reliable);
                LobbyControl.Log.LogInfo($"Player {controllerB.playerUsername} has been revived on other clients");
            }
            catch (Exception ex)
            {
                LobbyControl.Log.LogError($"Exception while respawning dead players {ex}");
            }

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