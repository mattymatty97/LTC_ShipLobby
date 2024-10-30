using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace LobbyControl.Patches;

[HarmonyPatch]
internal class TransparentPlayerFix
{
    private static readonly HashSet<int> ToRespawn = [];


    private static uint? _killPlayerID;
    private static uint? _revivePlayerID;
    private static uint? _sendNewValuesID;

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
            var methodInfo =
                AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerClientRpc));

            Utils.TryGetRpcID(methodInfo, out var id);
            _killPlayerID = id;
        }

        if (!_revivePlayerID.HasValue)
        {
            var methodInfo =
                AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.Debug_ReviveAllPlayersClientRpc));

            Utils.TryGetRpcID(methodInfo, out var id);
            _revivePlayerID = id;
        }

        if (!_sendNewValuesID.HasValue)
        {
            var methodInfo = AccessTools.Method(typeof(PlayerControllerB),
                nameof(PlayerControllerB.SendNewPlayerValuesServerRpc));

            Utils.TryGetRpcID(methodInfo, out var id);
            _sendNewValuesID = id;
            var harmonyTarget = AccessTools.Method(typeof(PlayerControllerB), $"__rpc_handler_{id}");
            var harmonyPrefix = AccessTools.Method(typeof(TransparentPlayerFix), nameof(RespawnDcPlayer));
            LobbyControl._harmony.Patch(harmonyTarget, new HarmonyMethod(harmonyPrefix), null, null, null, null);
        }
    }

    //SendNewPlayerValuesServerRpc
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
            if (!GameNetworkManager.Instance.disableSteam && !NetworkManager.Singleton.IsServer)
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                SteamFriends.GetFriends().ToList();
            }

            var rpcList = startOfRound.ClientPlayerList.Keys.Where(id =>
            {
                if (GameNetworkManager.Instance.disableSteam || NetworkManager.Singleton.IsServer)
                    return true;

                if (startOfRound.ClientPlayerList.TryGetValue(id, out var index))
                {
                    var playerObject = startOfRound.allPlayerScripts[index];

                    var friend = new Friend(playerObject.playerSteamId);
                    if (friend.IsFriend)
                        return true;
                }

                return false;
            }).ToList();


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