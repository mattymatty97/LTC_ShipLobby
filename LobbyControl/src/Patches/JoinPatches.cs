using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using GameNetcodeStuff;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Unity.Netcode;
using Object = UnityEngine.Object;

namespace LobbyControl.Patches;

[HarmonyPatch]
internal class JoinPatches
{
    internal static bool isLanding = false;

    //Do not check for gameHasStarted.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ConnectionApproval))]
    private static IEnumerable<CodeInstruction> FixConnectionApprovalPrefix(
        IEnumerable<CodeInstruction> instructions)
    {
        var gameStartedField = AccessTools.Field(typeof(GameNetworkManager), nameof(GameNetworkManager.gameHasStarted));
        List<CodeInstruction> code = instructions.ToList();

        for (var index = 0; index < code.Count; index++)
        {
            var curr = code[index];
            if (curr.LoadsField(gameStartedField))
            {
                var next = code[index + 1];
                var prec = code[index - 1];
                if (next.Branches(out Label? dest))
                {
                    code[index - 1] = new CodeInstruction(OpCodes.Nop)
                    {
                        labels = prec.labels,
                        blocks = prec.blocks
                    };
                    code[index] = new CodeInstruction(OpCodes.Nop)
                    {
                        labels = curr.labels,
                        blocks = curr.blocks
                    };
                    code[index + 1] = new CodeInstruction(OpCodes.Br, dest)
                    {
                        labels = next.labels,
                        blocks = next.blocks
                    };
                    LobbyControl.Log.LogDebug("Patched ConnectionApproval!!");
                    break;
                }
            }
        }

        return code;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ConnectionApproval))]
    [HarmonyPriority(10)]
    private static Exception ThrottleApprovals(
        GameNetworkManager __instance,
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response,
        Exception __exception)
    {
        if (__exception != null)
            return __exception;

        if (!response.Approved)
            return null;

        //if we're already landing
        if (isLanding)
        {
            LobbyControl.Log.LogDebug("connection refused ( ship was landed ).");
            response.Reason = "Ship has already landed!";
            response.Approved = false;
            return null;
        }

        //if lobby is closed
        if (!__instance.disableSteam &&
            (!__instance.currentLobby.HasValue || !LobbyPatcher.IsOpen(__instance.currentLobby.Value)))
        {
            LobbyControl.Log.LogDebug("connection refused ( lobby was closed ).");
            response.Reason = "Lobby has been closed!";
            response.Approved = false;
            return null;
        }

        //log late joins
        if (__instance.gameHasStarted)
        {
            LobbyControl.Log.LogDebug("Incoming late connection.");
        }

        if (!LobbyControl.PluginConfig.JoinQueue.Enabled.Value)
            return null;

        response.Pending = true;
        ConnectionQueue.Enqueue(response);
        LobbyControl.Log.LogWarning($"Connection request Enqueued! count:{ConnectionQueue.Count}");
        return null;
    }

    private static readonly ConcurrentQueue<NetworkManager.ConnectionApprovalResponse> ConnectionQueue = new();

    private static readonly object Lock = new();
    private static ulong? _currentConnectingPlayer;
    private static ulong _currentConnectingExpiration;
    private static readonly bool[] CurrentConnectingPlayerConfirmations = [false, false];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientConnect))]
    private static void OnClientConnect(StartOfRound __instance, ulong clientId)
    {
        lock (Lock)
        {
            if (__instance.IsServer && LobbyControl.PluginConfig.JoinQueue.Enabled.Value)
            {
                CurrentConnectingPlayerConfirmations[0] = false;
                CurrentConnectingPlayerConfirmations[1] = false;
                _currentConnectingPlayer = clientId;
                _currentConnectingExpiration = (ulong)(Environment.TickCount +
                                                       LobbyControl.PluginConfig.JoinQueue.ConnectionTimeout.Value);
            }
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerDC))]
    private static void ResetForDc(StartOfRound __instance, int playerObjectNumber)
    {
        var playerObject = __instance.allPlayerObjects[playerObjectNumber];
        var playerScript = __instance.allPlayerScripts[playerObjectNumber];
        playerObject.transform.parent = __instance.playersContainer;
        playerScript.justConnected = true;
        playerScript.isCameraDisabled = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Singleton_OnClientDisconnectCallback))]
    private static void OnClientDisconnect(GameNetworkManager __instance, ulong clientId)
    {
        if (!__instance.isHostingGame)
            return;

        LobbyControl.Log.LogInfo($"{clientId} disconnected");
        lock (Lock)
        {
            if (_currentConnectingPlayer != clientId)
                return;

            _currentConnectingPlayer = null;
            _currentConnectingExpiration = 0;
        }
    }


    internal static void Init()
    {
        var methodInfo =
            AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.SyncAlreadyHeldObjectsServerRpc));

        if (Utils.TryGetRpcID(methodInfo, out var id))
        {
            var harmonyTarget = AccessTools.Method(typeof(StartOfRound), $"__rpc_handler_{id}");
            var harmonyFinalizer = AccessTools.Method(typeof(JoinPatches), nameof(ClientConnectionCompleted1));
            LobbyControl._harmony.Patch(harmonyTarget, null, null, null, new HarmonyMethod(harmonyFinalizer), null);
        }


        methodInfo =
            AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.SendNewPlayerValuesServerRpc));

        if (Utils.TryGetRpcID(methodInfo, out id))
        {
            var harmonyTarget = AccessTools.Method(typeof(PlayerControllerB), $"__rpc_handler_{id}");
            var harmonyFinalizer = AccessTools.Method(typeof(JoinPatches), nameof(ClientConnectionCompleted2));
            LobbyControl._harmony.Patch(harmonyTarget, null, null, null, new HarmonyMethod(harmonyFinalizer), null);
        }

        var monoModTarget = AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.StartGame));

        if (monoModTarget != null)
            LobbyControl.Hooks.Add(new Hook(monoModTarget, CheckValidStart, new HookConfig { Priority = 999 }));
        else
            LobbyControl.Log.LogFatal(
                $"Cannot apply patch to StartGame monoModTarget:{monoModTarget} monoModWrapper:{nameof(CheckValidStart)}");
    }

    private static void ClientConnectionCompleted1(
        NetworkBehaviour target,
        __RpcParams rpcParams)
    {
        var startOfRound = (StartOfRound)target;
        if (!startOfRound.IsServer)
            return;

        var clientId = rpcParams.Server.Receive.SenderClientId;

        lock (Lock)
        {
            if (_currentConnectingPlayer != clientId)
                return;

            CurrentConnectingPlayerConfirmations[0] = true;

            if (CurrentConnectingPlayerConfirmations[1] || GameNetworkManager.Instance.disableSteam)
            {
                LobbyControl.Log.LogWarning($"{clientId} completed the connection");
                _currentConnectingPlayer = null;
                _currentConnectingExpiration = (ulong)(Environment.TickCount +
                                                       LobbyControl.PluginConfig.JoinQueue.ConnectionDelay.Value);
            }
            else
            {
                LobbyControl.Log.LogWarning($"{clientId} is waiting to synchronize the name");
            }
        }
    }

    private static void ClientConnectionCompleted2(
        NetworkBehaviour target, __RpcParams rpcParams)
    {
        var playerControllerB = (PlayerControllerB)target;
        if (!playerControllerB.IsServer)
            return;

        var clientId = rpcParams.Server.Receive.SenderClientId;

        lock (Lock)
        {
            if (_currentConnectingPlayer != clientId)
                return;

            CurrentConnectingPlayerConfirmations[1] = true;

            if (CurrentConnectingPlayerConfirmations[0])
            {
                LobbyControl.Log.LogWarning($"{clientId} completed the connection");
                _currentConnectingPlayer = null;
                _currentConnectingExpiration = (ulong)(Environment.TickCount +
                                                       LobbyControl.PluginConfig.JoinQueue.ConnectionDelay.Value);
            }
            else
            {
                LobbyControl.Log.LogWarning($"{clientId} is waiting to synchronize the held items");
            }
        }
    }


    [HarmonyFinalizer]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LateUpdate))]
    private static void ProcessConnectionQueue(StartOfRound __instance)
    {
        if (!__instance.IsServer)
            return;

        if (!Monitor.TryEnter(Lock))
            return;

        try
        {
            if (_currentConnectingPlayer.HasValue)
            {
                if ((ulong)Environment.TickCount < _currentConnectingExpiration)
                    return;

                if (_currentConnectingPlayer.Value != 0L)
                {
                    LobbyControl.Log.LogWarning(
                        $"Connection to {_currentConnectingPlayer.Value} expired, Disconnecting!");
                    try
                    {
                        NetworkManager.Singleton.DisconnectClient(_currentConnectingPlayer.Value);
                    }
                    catch (Exception ex)
                    {
                        LobbyControl.Log.LogError(ex);
                    }
                }

                _currentConnectingPlayer = null;
                _currentConnectingExpiration = 0;
            }
            else
            {
                if (__instance.inShipPhase)
                {
                    if ((ulong)Environment.TickCount < _currentConnectingExpiration)
                        return;

                    if (!ConnectionQueue.TryDequeue(out var response))
                        return;

                    LobbyControl.Log.LogWarning(
                        $"Connection request Resumed! remaining: {ConnectionQueue.Count}");
                    response.Pending = false;
                    if (!response.Approved)
                        return;
                    CurrentConnectingPlayerConfirmations[0] = false;
                    CurrentConnectingPlayerConfirmations[1] = false;
                    _currentConnectingPlayer = 0L;
                    _currentConnectingExpiration = (ulong)Environment.TickCount + 1000UL;
                }
                else
                {
                    if (ConnectionQueue.IsEmpty)
                        return;

                    foreach (var approvalResponse in ConnectionQueue)
                    {
                        approvalResponse.Approved = false;
                        approvalResponse.Reason = "ship has landed!";
                        approvalResponse.Pending = false;
                    }

                    ConnectionQueue.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            LobbyControl.Log.LogError(ex);
        }
        finally
        {
            Monitor.Exit(Lock);
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
    private static void FlushConnectionQueue()
    {
        lock (Lock)
        {
            CurrentConnectingPlayerConfirmations[0] = false;
            CurrentConnectingPlayerConfirmations[1] = false;
            _currentConnectingPlayer = null;
            _currentConnectingExpiration = 0UL;
            if (ConnectionQueue.Count > 0)
            {
                LobbyControl.Log.LogWarning(
                    $"Disconnecting with {ConnectionQueue.Count} pending connection, Flushing!");
            }

            while (ConnectionQueue.TryDequeue(out var response))
            {
                response.Reason = "Host has disconnected!";
                response.Approved = false;
                response.Pending = false;
            }
        }
    }


    private static bool CanStartGame()
    {
        return !_currentConnectingPlayer.HasValue && ConnectionQueue.IsEmpty;
    }

    private static void CheckValidStart(Action<StartOfRound> orig, StartOfRound @this)
    {
        if (!@this.IsServer || !@this.inShipPhase)
        {
            orig(@this);
            return;
        }

        if (!CanStartGame())
        {
            var count = ConnectionQueue.Count + (_currentConnectingPlayer.HasValue ? 1 : 0);

            var leverScript = Object.FindAnyObjectByType<StartMatchLever>();

            leverScript.CancelStartGame();
            leverScript.CancelStartGameClientRpc();

            HUDManager.Instance.DisplayTip(
                "GAME START CANCELLED",
                $"{count} Players Connecting!!",
                true);

            HUDManager.Instance.AddTextMessageServerRpc(
                $"there are still {count} Players connecting!!\n");
            return;
        }

        isLanding = true;

        orig(@this);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SetShipReadyToLand))]
    private static void OnReadyToLand(StartOfRound __instance)
    {
        isLanding = false;
    }


    //----------------RADAR NAMES----------------


    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
    private static IEnumerable<CodeInstruction> FixRadarNames(IEnumerable<CodeInstruction> instructions)
    {
        if (!LobbyControl.PluginConfig.SteamLobby.RadarFix.Value)
            return instructions;

        var codes = instructions.ToList();

        var fieldInfo = typeof(TransformAndName).GetField(nameof(TransformAndName.name));
        var methodInfo = typeof(JoinPatches).GetMethod(nameof(SetNewName),
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);

        for (var i = 0; i < codes.Count; i++)
        {
            var curr = codes[i];

            if (curr.StoresField(fieldInfo))
            {
                for (var index = i - 6; index < i; index++)
                {
                    var iterator = codes[index];
                    if (!iterator.IsLdloc())
                        codes[index] = new CodeInstruction(OpCodes.Nop)
                        {
                            blocks = iterator.blocks,
                            labels = iterator.labels
                        };
                }

                codes[i] = new CodeInstruction(OpCodes.Call, methodInfo)
                {
                    blocks = curr.blocks,
                    labels = curr.labels
                };
                LobbyControl.Log.LogDebug("SendNewPlayerValuesClientRpc patched!");
            }
        }

        return codes;
    }


    private static void SetNewName(int index, string name)
    {
        var startOfRound = StartOfRound.Instance;
        var playerObject = startOfRound.allPlayerObjects[index];
        startOfRound.mapScreen.ChangeNameOfTargetTransform(playerObject.transform, name);
    }
}