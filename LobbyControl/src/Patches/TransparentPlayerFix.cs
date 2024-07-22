using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using Unity.Netcode;
using UnityEngine;

namespace LobbyControl.Patches;

[HarmonyPatch]
internal class TransparentPlayerFix
{
    private static readonly HashSet<int> ToRespawn = [];


    private static uint? _killPlayerID;
    private static uint? _revivePlayerID;

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

    internal static void Init()
    {
        if (!_killPlayerID.HasValue)
        {
            var methodInfo = typeof(PlayerControllerB).GetMethod("KillPlayerClientRpc",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var instructions = methodInfo.GetMethodPatcher().CopyOriginal().Definition.Body.Instructions;

            uint tmp = 0;
            for (var i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Ldc_I4 && instructions[i - 1].OpCode == OpCodes.Ldarg_0)
                    tmp = (uint)(int)instructions[i].Operand;

                if (instructions[i].OpCode != OpCodes.Call ||
                    instructions[i].Operand is not MethodReference operand ||
                    !operand.Is(typeof(NetworkBehaviour), nameof(NetworkBehaviour.__beginSendClientRpc)
                    ))
                    continue;

                _killPlayerID = tmp;
                LobbyControl.Log.LogDebug($"KillPlayerClientRpc Id found: {_killPlayerID}U");
                break;
            }
        }


        if (!_revivePlayerID.HasValue)
        {
            var methodInfo =
                typeof(StartOfRound).GetMethod("Debug_ReviveAllPlayersClientRpc", BindingFlags.Instance | BindingFlags.Public);

            var instructions = methodInfo.GetMethodPatcher().CopyOriginal().Definition.Body.Instructions;

            uint tmp = 0;
            for (var i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Ldc_I4 && instructions[i - 1].OpCode == OpCodes.Ldarg_0)
                    tmp = (uint)(int)instructions[i].Operand;

                if (instructions[i].OpCode != OpCodes.Call ||
                    instructions[i].Operand is not MethodReference operand ||
                    !operand.Is(typeof(NetworkBehaviour), nameof(NetworkBehaviour.__beginSendClientRpc)
                    ))
                    continue;

                _revivePlayerID = tmp;
                LobbyControl.Log.LogDebug($"Debug_ReviveAllPlayersClientRpc Id found: {_revivePlayerID}U");
                break;
            }
        }
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

        if (!startOfRound.IsServer || !ToRespawn.Contains(objectId))
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
            var bufferWriter =
                controllerB.__beginSendClientRpc(_killPlayerID!.Value, clientRpcParams, RpcDelivery.Reliable);
            BytePacker.WriteValueBitPacked(bufferWriter, objectId);
            bufferWriter.WriteValueSafe(false);
            bufferWriter.WriteValueSafe(Vector3.zero);
            BytePacker.WriteValueBitPacked(bufferWriter, (int)CauseOfDeath.Unknown);
            BytePacker.WriteValueBitPacked(bufferWriter, 0);
            bufferWriter.WriteValueSafe(Vector3.zero);
            controllerB.__endSendClientRpc(ref bufferWriter, _killPlayerID!.Value, clientRpcParams,
                RpcDelivery.Reliable);
            LobbyControl.Log.LogInfo($"Player {controllerB.playerUsername} has been killed by host");

            //Client Respawn
            bufferWriter =
                startOfRound.__beginSendClientRpc(_revivePlayerID!.Value, clientRpcParams, RpcDelivery.Reliable);
            startOfRound.__endSendClientRpc(ref bufferWriter, _revivePlayerID!.Value, clientRpcParams,
                RpcDelivery.Reliable);
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