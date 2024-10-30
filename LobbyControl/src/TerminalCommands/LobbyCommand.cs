using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using LobbyControl.Patches;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyControl.TerminalCommands
{
    public class LobbyCommand : Command
    {
        private const string DefaultText =
            @"- status        : prints the current lobby status

Steam:
- open          : open the lobby
- close         : close the lobby
- private       : set lobby to Invite Only
- friend        : set lobby to Friends Only
- public        : set lobby to Public
- rename [name] : change the name of the lobby

Saving:
- autosave      : toggle autosave for the savefile
- save (name)   : save the lobby
- load (name)   : load a savefile
- switch (name) : swap savefile without loading it
- clear         : reset the current lobby to empty

Extra:
- dropall       : drop all items to the ground
";

        public override bool IsCommand(string[] args)
        {
            return GameNetworkManager.Instance.isHostingGame && args[0].Trim().ToLower() == "lobby";
        }

        public override TerminalNode Execute(string[] args)
        {
            var node = ScriptableObject.CreateInstance<TerminalNode>();
            try
            {
                if (args[1].IsNullOrWhiteSpace())
                    return HelpNode();

                var command = args[1];

                switch (command)
                {
                    case "status":
                    {
                        StatusCommand(ref node, args);
                        break;
                    }
                    case "open":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        OpenCommand(ref node, args);
                        break;
                    }
                    case "close":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        CloseCommand(ref node, args);
                        break;
                    }
                    case "private":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        PrivateCommand(ref node, args);
                        break;
                    }
                    case "friend":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        FriendCommand(ref node, args);
                        break;
                    }
                    case "public":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        PublicCommand(ref node, args);
                        break;
                    }
                    case "rename":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        RenameCommand(ref node, args);
                        break;
                    }
                    case "autosave":
                    {
                        AutoSaveCommand(ref node, args);
                        break;
                    }
                    case "save":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        SaveCommand(ref node, args);
                        break;
                    }
                    case "load":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        LoadCommand(ref node, args);
                        break;
                    }
                    case "switch":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        SwitchCommand(ref node, args);
                        break;
                    }
                    case "clear":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        ClearCommand(ref node, args);
                        break;
                    }
                    case "dropall":
                    {
                        if (PerformOrbitCheck(node, out var errorText))
                            return errorText;

                        if (!LobbyControl.CanModifyLobby)
                        {
                            node.displayText = "Lobby cannot be changed at the moment\n\n";
                            break;
                        }

                        DropAllCommand(ref node, args);
                        break;
                    }
                    case "help":
                    {
                        node = HelpNode();
                        break;
                    }
                    default:
                        node = HelpNode();
                        node.displayText = "Invalid Command, options:\n" + node.displayText;
                        break;
                }
            }
            catch (Exception ex)
            {
                LobbyControl.Log.LogError("Exception:\n\n" + ex);
                node.displayText = "Exception!\n\n";
            }

            return node;
        }

        private static bool StatusCommand(ref TerminalNode node, string[] args)
        {
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            var lobby = manager.currentLobby.Value;
            var status = LobbyPatcher.IsOpen(lobby);
            var visibility = LobbyPatcher.GetVisibility(lobby);
            var name = lobby.GetData("name");
            var autoSave = LobbyControl.CanSave;

            var builder = new StringBuilder("Lobby Status:");
            builder.Append("\n- File is '").Append(manager.currentSaveFileName).Append("'");
            builder.Append("\n- Name is '").Append(name).Append("'");
            builder.Append("\n- Status is ").Append(status ? "Open" : "Closed");
            builder.Append("\n- Visibility is ").Append(visibility.ToString());
            builder.Append("\n- Saving is ").Append(autoSave ? "Automatic" : "Manual");
            builder.Append("\n\n");
            node.displayText = builder.ToString();
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool OpenCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Reopening lobby, setting to joinable.");
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            manager.SetLobbyJoinable(true);

            // Restore the friend invite button in the ESC menu.
            Object.FindObjectOfType<QuickMenuManager>().inviteFriendsTextAlpha.alpha = 1f;

            var outText = "Lobby is now Open";

            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool CloseCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Closing lobby, setting to not joinable.");
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            manager.SetLobbyJoinable(false);

            // Hide the friend invite button in the ESC menu.
            Object.FindObjectOfType<QuickMenuManager>().inviteFriendsTextAlpha.alpha = 0f;

            var outText = "Lobby is now Closed";

            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool PrivateCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Locking lobby, setting to Private.");
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            manager.currentLobby.Value.SetPrivate();

            var outText = "Lobby is now Private";
            LobbyControl.Log.LogInfo(outText);
            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool FriendCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Locking lobby, setting to Friends Only.");
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            manager.currentLobby.Value.SetPrivate();
            manager.currentLobby.Value.SetFriendsOnly();

            var outText = "Lobby is now Friends Only";
            LobbyControl.Log.LogInfo(outText);
            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool PublicCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Unlocking lobby, setting to Public.");
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            manager.currentLobby.Value.SetPublic();
            var outText = "Lobby is now Public";
            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool RenameCommand(ref TerminalNode node, string[] args)
        {
            if (args[2].IsNullOrWhiteSpace())
            {
                node.displayText = "Lobby name cannot be null\n\n";
                return false;
            }

            var remaining = args[2];
            if (remaining.Length > 40)
                remaining = remaining.Substring(0, 40);

            LobbyControl.Log.LogDebug("Renaming lobby: \"" + remaining + "\"");
            var manager = GameNetworkManager.Instance;
            if (!manager.currentLobby.HasValue)
            {
                node.displayText = "Failed to fetch lobby ( was null )\n\n";
                return false;
            }

            manager.lobbyHostSettings.lobbyName = remaining;
            manager.currentLobby.Value.SetData("name", manager.lobbyHostSettings.lobbyName);
            manager.steamLobbyName = manager.currentLobby.Value.GetData("name");

            ES3.Save<string>("HostSettings_Name", manager.steamLobbyName, "LCGeneralSaveData");

            var outText = "Lobby renamed to \"" + remaining + "\"";
            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool AutoSaveCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Toggling AutoSave");

            LobbyControl.CanSave = LobbyControl.AutoSaveEnabled = !LobbyControl.AutoSaveEnabled;

            var outText = "AutoSaving is now " + (LobbyControl.CanSave ? "On" : "Off");

            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool SaveCommand(ref TerminalNode node, string[] args)
        {
            if (StartOfRound.Instance.isChallengeFile)
            {
                node.displayText = "Cannot Edit Challenge Save\n\n";
                return false;
            }

            LobbyControl.Log.LogDebug("Saving Lobby");
            var manager = GameNetworkManager.Instance;

            var oldSaveFileName = manager.currentSaveFileName;
            if (!args[2].IsNullOrWhiteSpace())
            {
                var remaining = args[2];
                if (remaining.Length > 20)
                    remaining = remaining.Substring(0, 20);
                manager.currentSaveFileName = remaining;
            }

            if (oldSaveFileName != manager.currentSaveFileName)
                ES3.CopyFile(oldSaveFileName, manager.currentSaveFileName);

            LobbyControl.CanSave = true;
            HUDManager.Instance.saveDataIconAnimatorB.SetTrigger("save");
            manager.SaveGameValues();
            var outText = "Lobby Saved to " + manager.currentSaveFileName;
            LobbyControl.CanSave = LobbyControl.AutoSaveEnabled;
            manager.currentSaveFileName = oldSaveFileName;

            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool LoadCommand(ref TerminalNode node, string[] args)
        {
            if (StartOfRound.Instance.isChallengeFile)
            {
                node.displayText = "Cannot Edit Challenge Save\n\n";
                return false;
            }

            LobbyControl.Log.LogDebug("Reloading Lobby");
            var manager = GameNetworkManager.Instance;

            if (!args[2].IsNullOrWhiteSpace())
            {
                var remaining = args[2];
                if (remaining.Length > 20)
                    remaining = remaining.Substring(0, 20);
                manager.currentSaveFileName = remaining;
            }

            var outText = "Lobby \'" + manager.currentSaveFileName + "\' loaded";

            StartOfRound.Instance.StartCoroutine(LoadLobbyCoroutine());
            LobbyControl.AutoSaveEnabled = LobbyControl.CanSave = ES3.Load("LC_SavingMethod",
                GameNetworkManager.Instance.currentSaveFileName, true);

            LobbyControl.Log.LogInfo(outText);

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }


        private static bool SwitchCommand(ref TerminalNode node, string[] args)
        {
            if (StartOfRound.Instance.isChallengeFile)
            {
                node.displayText = "Cannot Edit Challenge Save\n\n";
                return false;
            }

            LobbyControl.Log.LogDebug("Switching Lobby");
            var manager = GameNetworkManager.Instance;

            if (args[2].IsNullOrWhiteSpace())
            {
                node.displayText = "You need to specify a destination for the swap!";
                return false;
            }

            var remaining = args[2];
            if (remaining.Length > 20)
                remaining = remaining.Substring(0, 20);

            manager.currentSaveFileName = remaining;

            var outText = "Lobby is now saving to \'" + manager.currentSaveFileName + "\'";

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool ClearCommand(ref TerminalNode node, string[] args)
        {
            if (StartOfRound.Instance.isChallengeFile)
            {
                node.displayText = "Cannot Edit Challenge Save\n\n";
                return false;
            }

            LobbyControl.Log.LogDebug("Clearing Lobby");
            LobbyControl.CanSave = true;
            GameNetworkManager.Instance.ResetSavedGameValues();
            ES3.DeleteFile(GameNetworkManager.Instance.currentSaveFileName);
            LobbyControl.CanSave = LobbyControl.AutoSaveEnabled;
            var res = LoadCommand(ref node, new string[3]);
            if (res)
            {
                node.displayText = "Lobby is now Empty!";
            }

            return res;
        }

        private static bool DropAllCommand(ref TerminalNode node, string[] args)
        {
            LobbyControl.Log.LogDebug("Dropping all Items");

            var outText = "All Items Dropped";

            foreach (var playerScript in StartOfRound.Instance.allPlayerScripts)
            {
                playerScript.DropAllHeldItemsAndSync();
            }

            node.displayText = outText + "\n\n";
            node.maxCharactersToType = node.displayText.Length + 2;
            return true;
        }

        private static bool PerformOrbitCheck(TerminalNode terminalNode, out TerminalNode errorText)
        {
            if (!StartOfRound.Instance.inShipPhase)
            {
                terminalNode.displayText = "Can only be used while in Orbit\n\n";
                {
                    errorText = terminalNode;
                    return true;
                }
            }

            errorText = null;
            return false;
        }

        private static TerminalNode HelpNode()
        {
            var node = ScriptableObject.CreateInstance<TerminalNode>();
            node.displayText = DefaultText;
            node.clearPreviousText = true;
            node.maxCharactersToType = node.displayText.Length + 2;
            return node;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private static IEnumerator LoadLobbyCoroutine()
        {
            var startOfRound = StartOfRound.Instance;
            var terminal = Object.FindObjectOfType<Terminal>();
            //remove all items
            startOfRound.ResetShip();

            //Temporary measure until Zeekers Fix TODO: remove once fix is implemented
            var objectsByType = Object.FindObjectsOfType<VehicleController>();
            foreach (var vehicle in objectsByType)
            {
                if (vehicle.NetworkObject != null)
                {
                    vehicle.NetworkObject.Despawn(false);
                }
            }

            var mem = GameNetworkManager.Instance.gameHasStarted;
            GameNetworkManager.Instance.gameHasStarted = false;
            //remove remaining unlockables
            foreach (var unlockable in StartOfRound.Instance.SpawnedShipUnlockables.ToList())
            {
                var itemEntry = startOfRound.unlockablesList.unlockables[unlockable.Key];
                if (!itemEntry.alreadyUnlocked || !itemEntry.spawnPrefab)
                    continue;

                if (unlockable.Value == null)
                    continue;

                NetworkObject component = unlockable.Value.GetComponent<NetworkObject>();
                if (component != null && component.IsSpawned)
                    component.Despawn();
            }

            startOfRound.SpawnedShipUnlockables.Clear();
            startOfRound.suitsPlaced = 0;
            GameNetworkManager.Instance.ResetUnlockablesListValues();
            //wait
            yield return new WaitForSeconds(0.2f);
            //read new misc data
            startOfRound.SetTimeAndPlanetToSavedSettings();
            startOfRound.SetMapScreenInfoToCurrentLevel();
            //restart the terminal
            terminal.Start();
            //if there are any players
            if (startOfRound.connectedPlayersAmount >= 1)
            {
                //send the updated misc variables to the players
                RefreshLobby();
            }

            //spawn the new unlockables and update their position
            ReloadUnlockables();
            yield return new WaitForSeconds(0.2f);
            //spawn in car
            startOfRound.LoadAttachedVehicle();
            yield return new WaitForSeconds(0.1f);
            //spawn new items
            startOfRound.LoadShipGrabbableItems();
            yield return new WaitForSeconds(0.1f);
            //if there are any players
            if (startOfRound.connectedPlayersAmount >= 1)
            {
                //sync item variables
                startOfRound.SyncShipUnlockablesServerRpc();
            }

            GameNetworkManager.Instance.gameHasStarted = mem;
        }

        private static void ReloadUnlockables()
        {
            StartOfRound startOfRound = StartOfRound.Instance;
            startOfRound.LoadUnlockables();
            try
            {
                //7 - closet
                //8 - cabinet
                //15 - beds
                //16 - terminal
                for (var baseUnlockable = 0;
                     baseUnlockable < startOfRound.unlockablesList.unlockables.Count;
                     baseUnlockable++)
                {
                    var unlockable = startOfRound.unlockablesList.unlockables[baseUnlockable];
                    if (unlockable.alreadyUnlocked)
                    {
                        LobbyControl.Log.LogWarning($"{unlockable.unlockableName} starting");
                        if (!startOfRound.SpawnedShipUnlockables.ContainsKey(baseUnlockable))
                            startOfRound.SpawnUnlockable(baseUnlockable);
                        PlaceableShipObject shipObject = startOfRound.SpawnedShipUnlockables[baseUnlockable]
                            .GetComponent<PlaceableShipObject>();

                        if (shipObject is null)
                            shipObject = startOfRound.SpawnedShipUnlockables[baseUnlockable]
                                .GetComponentInChildren<PlaceableShipObject>();
                        if (shipObject is null)
                            continue;

                        LobbyControl.Log.LogWarning($"{unlockable.unlockableName} continuing");
                        var parentObject = shipObject.parentObject;
                        if (parentObject != null)
                        {
                            LobbyControl.Log.LogWarning($"{unlockable.unlockableName} parentObject");
                            if (unlockable.inStorage)
                                shipObject.parentObject.disableObject = true;

                            var offset = parentObject.positionOffset;
                            var localOffset = startOfRound.elevatorTransform.TransformPoint(offset);
                            var position = shipObject.mainMesh.transform.position;
                            var placementPosition = localOffset -
                                                    (shipObject.parentObject.transform.position - position) -
                                                    (position - shipObject.placeObjectCollider.transform.position);
                            unlockable.placedPosition = placementPosition;
                            var rotation = Quaternion.Euler(parentObject.rotationOffset);
                            var step1 = rotation * Quaternion.Inverse(parentObject.transform.rotation);
                            var step2 = step1 * shipObject.mainMesh.transform.rotation;
                            var placementRotation = step2.eulerAngles;
                            unlockable.placedRotation = placementRotation;
                        }
                        else
                        {
                            LobbyControl.Log.LogWarning($"{unlockable.unlockableName} parentObjectSecondary");
                            var parentObjectSecondary = shipObject.parentObjectSecondary;
                            var parentPos = parentObjectSecondary.position;
                            var transform = shipObject.mainMesh.transform;
                            var position = transform.position;
                            var placementPosition = parentPos -
                                                    ((parentObjectSecondary.transform.position - position) +
                                                     (position - shipObject.placeObjectCollider.transform.position));
                            unlockable.placedPosition = placementPosition;
                            var rotation = parentObjectSecondary.rotation;
                            var step1 = rotation * Quaternion.Inverse(transform.rotation);
                            var placementRotation = step1.eulerAngles;
                            unlockable.placedRotation = placementRotation;
                        }
                    }
                }

                if (startOfRound.connectedPlayersAmount >= 1)
                {
                    List<ulong> rpcList = startOfRound.ClientPlayerList.Keys.ToList();

                    ClientRpcParams clientRpcParams = new ClientRpcParams()
                    {
                        Send = new ClientRpcSendParams()
                        {
                            TargetClientIds = rpcList,
                        },
                    };

                    for (var baseUnlockable = 0;
                         baseUnlockable < startOfRound.unlockablesList.unlockables.Count;
                         baseUnlockable++)
                    {
                        var unlockable = startOfRound.unlockablesList.unlockables[baseUnlockable];
                        if (unlockable.alreadyUnlocked && !unlockable.inStorage)
                        {
                            FastBufferWriter bufferWriter =
                                startOfRound.__beginSendClientRpc(1076853239U, clientRpcParams, RpcDelivery.Reliable);
                            BytePacker.WriteValueBitPacked(bufferWriter, baseUnlockable);
                            startOfRound.__endSendClientRpc(ref bufferWriter, 1076853239U, clientRpcParams,
                                RpcDelivery.Reliable);
                        }
                    }

                    startOfRound.SyncShipUnlockablesServerRpc();
                }
            }
            catch (Exception ex)
            {
                LobbyControl.Log.LogError(ex);
            }
        }

        private static void RefreshLobby()
        {
            var startOfRound = StartOfRound.Instance;
            List<ulong> ulongList = new List<ulong>();
            List<ulong> rpcList = new List<ulong>();
            for (var index = 0; index < startOfRound.allPlayerObjects.Length; ++index)
            {
                var component = startOfRound.allPlayerObjects[index].GetComponent<NetworkObject>();
                if (!component.IsOwnedByServer)
                {
                    ulongList.Add(component.OwnerClientId);
                    rpcList.Add(component.OwnerClientId);
                }
                else if (index == 0)
                    ulongList.Add(NetworkManager.Singleton.LocalClientId);
                else
                    ulongList.Add(999UL);
            }

            ClientRpcParams clientRpcParams = new ClientRpcParams()
            {
                Send = new ClientRpcSendParams()
                {
                    TargetClientIds = rpcList,
                },
            };

            var groupCredits = Object.FindObjectOfType<Terminal>().groupCredits;
            var profitQuota = TimeOfDay.Instance.profitQuota;
            var quotaFulfilled = TimeOfDay.Instance.quotaFulfilled;
            var timeUntilDeadline = (int)TimeOfDay.Instance.timeUntilDeadline;
            var controller = StartOfRound.Instance.localPlayerController;

            FastBufferWriter bufferWriter =
                startOfRound.__beginSendClientRpc(886676601U, clientRpcParams, RpcDelivery.Reliable);
            BytePacker.WriteValueBitPacked(bufferWriter, controller.actualClientId);
            BytePacker.WriteValueBitPacked(bufferWriter, startOfRound.connectedPlayersAmount - 1);
            bufferWriter.WriteValueSafe<bool>(true);
            bufferWriter.WriteValueSafe<ulong>(ulongList.ToArray());
            BytePacker.WriteValueBitPacked(bufferWriter, startOfRound.ClientPlayerList[controller.actualClientId]);
            BytePacker.WriteValueBitPacked(bufferWriter, groupCredits);
            BytePacker.WriteValueBitPacked(bufferWriter, startOfRound.currentLevelID);
            BytePacker.WriteValueBitPacked(bufferWriter, profitQuota);
            BytePacker.WriteValueBitPacked(bufferWriter, timeUntilDeadline);
            BytePacker.WriteValueBitPacked(bufferWriter, quotaFulfilled);
            BytePacker.WriteValueBitPacked(bufferWriter, startOfRound.randomMapSeed);
            bufferWriter.WriteValueSafe<bool>(in startOfRound.isChallengeFile);
            startOfRound.__endSendClientRpc(ref bufferWriter, 886676601U, clientRpcParams, RpcDelivery.Reliable);
        }
    }
}