using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using GameNetcodeStuff;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using LobbyControl.Dependency;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Unity.Netcode;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class JoinPatches
    {
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ConnectionApproval))]
        [HarmonyPriority(10)]
        private static Exception ThrottleApprovals(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response,
            Exception __exception)
        {
            if (__exception != null)
                return __exception;

            if (!response.Approved)
                return null;

            if (!StartOfRound.Instance.inShipPhase)
            {
                response.Approved = false;
                response.Reason = "Ship already landed";
                return null;
            }

            if (!LobbyControl.PluginConfig.JoinQueue.Enabled.Value)
                return null;

            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Player.Queue",
                    $"Player Enqueued {Encoding.ASCII.GetString(request.Payload)} remaining:{ConnectionQueue.Count}");

            response.Pending = true;
            ConnectionQueue.Enqueue(response);
            LobbyControl.Log.LogWarning($"Connection request Enqueued! count:{ConnectionQueue.Count}");
            return null;
        }

        private static readonly ConcurrentQueue<NetworkManager.ConnectionApprovalResponse> ConnectionQueue =
            new ConcurrentQueue<NetworkManager.ConnectionApprovalResponse>();

        private static readonly object _lock = new object();
        private static ulong? _currentConnectingPlayer = null;
        private static ulong _currentConnectingExpiration = 0;
        private static readonly bool[] _currentConnectingPlayerConfirmations = [false, false];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientConnect))]
        private static void OnClientConnect(StartOfRound __instance, ulong clientId)
        {
            lock (_lock)
            {
                if (__instance.IsServer && LobbyControl.PluginConfig.JoinQueue.Enabled.Value)
                {
                    _currentConnectingPlayerConfirmations[0] = false;
                    _currentConnectingPlayerConfirmations[1] = false;
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
            lock (_lock)
            {
                if (_currentConnectingPlayer != clientId)
                    return;

                if (AsyncLoggerProxy.Enabled)
                    AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Player.Queue", $"Player Disconnected!");

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
            var monoModWrapper = AccessTools.Method(typeof(JoinPatches), nameof(CheckValidStart));
            
            if (monoModTarget != null && monoModWrapper != null)
                LobbyControl.Hooks.Add(new Hook(monoModTarget, monoModWrapper, new HookConfig { Priority = 999 }));
            else
                LobbyControl.Log.LogFatal($"Cannot apply patch to StartGame monoModTarget:{monoModTarget} monoModWrapper:{monoModWrapper}");
        }

        private static void ClientConnectionCompleted1(
            NetworkBehaviour target,
            __RpcParams rpcParams)
        {
            var startOfRound = (StartOfRound)target;
            if (!startOfRound.IsServer)
                return;

            var clientId = rpcParams.Server.Receive.SenderClientId;

            lock (_lock)
            {
                if (_currentConnectingPlayer != clientId)
                    return;

                _currentConnectingPlayerConfirmations[0] = true;

                if (_currentConnectingPlayerConfirmations[1] || GameNetworkManager.Instance.disableSteam)
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

            lock (_lock)
            {
                if (_currentConnectingPlayer != clientId)
                    return;

                _currentConnectingPlayerConfirmations[1] = true;

                if (_currentConnectingPlayerConfirmations[0])
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

            if (!Monitor.TryEnter(_lock))
                return;

            try
            {
                if (_currentConnectingPlayer.HasValue)
                {
                    if ((ulong)Environment.TickCount < _currentConnectingExpiration)
                        return;

                    if (_currentConnectingPlayer.Value != 0L)
                    {
                        if (AsyncLoggerProxy.Enabled)
                            AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Player.Queue", $"Player Timeout!");

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

                        if (AsyncLoggerProxy.Enabled)
                            AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Player.Queue",
                                $"Player Dequeued remaining:{ConnectionQueue.Count}");
                        LobbyControl.Log.LogWarning(
                            $"Connection request Resumed! remaining: {ConnectionQueue.Count}");
                        response.Pending = false;
                        if (!response.Approved)
                            return;
                        _currentConnectingPlayerConfirmations[0] = false;
                        _currentConnectingPlayerConfirmations[1] = false;
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
                Monitor.Exit(_lock);
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
        private static void FlushConnectionQueue()
        {
            lock (_lock)
            {
                _currentConnectingPlayerConfirmations[0] = false;
                _currentConnectingPlayerConfirmations[1] = false;
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

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
        private static IEnumerable<CodeInstruction> FixRadarNames(IEnumerable<CodeInstruction> instructions)
        {
            if (!LobbyControl.PluginConfig.SteamLobby.RadarFix.Value)
                return instructions;

            var codes = instructions.ToList();

            var fieldInfo = typeof(TransformAndName).GetField(nameof(TransformAndName.name));
            var methodInfo = typeof(JoinPatches).GetMethod(nameof(JoinPatches.SetNewName),
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
        
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGameServerRpc))]
        private static bool CheckValidStart(StartOfRound __instance)
        {
            var networkManager = __instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return true;
            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Server ||
                !networkManager.IsServer && !networkManager.IsHost)
                return true;
            if (CanStartGame())
                return true;
            var count = ConnectionQueue.Count + (_currentConnectingPlayer.HasValue ? 1 : 0);
            Object.FindAnyObjectByType<StartMatchLever>().CancelStartGameClientRpc();
            HUDManager.Instance.DisplayTip(
                "GAME START CANCELLED",
                $"{count} Players Connecting!!",
                true);
            HUDManager.Instance.AddTextMessageServerRpc(
                $"there are still {count} Players connecting!!\n");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartMatchLever), nameof(StartMatchLever.StartGame))]
        private static bool CheckValidStart(StartMatchLever __instance)
        {
            if (!__instance.IsServer)
                return true;

            if (__instance.playersManager.travellingToNewLevel || !__instance.playersManager.inShipPhase ||
                __instance.playersManager.connectedPlayersAmount + 1 <= 1 && !__instance.singlePlayerEnabled)
                return true;

            if (__instance.playersManager.fullyLoadedPlayers.Count <
                __instance.playersManager.connectedPlayersAmount + 1)
                return true;

            if (CanStartGame())
                return true;

            var count = ConnectionQueue.Count + (_currentConnectingPlayer.HasValue ? 1 : 0);
            __instance.CancelStartGame();
            HUDManager.Instance.DisplayTip(
                "GAME START CANCELLED",
                $"{count} Players Connecting!!",
                true);
            HUDManager.Instance.AddTextMessageServerRpc(
                $"there are still {count} Players connecting!!\n");
            return false;
        }*/
        
        private static void CheckValidStart(Action<StartOfRound> orig, StartOfRound @this)
        {
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

            orig(@this);
        }

        private static bool CanStartGame()
        {
            return !_currentConnectingPlayer.HasValue && ConnectionQueue.IsEmpty;
        }

        private static void SetNewName(int index, string name)
        {
            var startOfRound = StartOfRound.Instance;
            var playerObject = startOfRound.allPlayerObjects[index];
            startOfRound.mapScreen.ChangeNameOfTargetTransform(playerObject.transform, name);
        }
    }
}