using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class LimitPatcher
    {
        
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncShipUnlockablesClientRpc))]
        private static IEnumerable<CodeInstruction> PacketSizePatch(IEnumerable<CodeInstruction> instructions)
        {
            if (!LobbyControl.PluginConfig.SaveLimit.Enabled.Value)
                return instructions;
            
            var methodInfo = typeof(NetworkBehaviour).GetMethod(nameof(NetworkBehaviour.__beginSendClientRpc), BindingFlags.Instance | BindingFlags.NonPublic);
            var contructorInfo = typeof(FastBufferWriter).GetConstructor([typeof(int),typeof(Allocator),typeof(int)]);

            var codes = instructions.ToList();
            
            var matcher = new CodeMatcher(codes);

            matcher.MatchForward(true, new CodeMatch(OpCodes.Call, methodInfo));
            
            if (matcher.IsInvalid)
            {
                LobbyControl.Log.LogWarning("PacketSize patch failed 1!!");
                LobbyControl.Log.LogDebug(string.Join("\n", matcher.Instructions()));
                return codes;
            }

            matcher.Advance(1);

            matcher.Insert(
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldc_I4, 1024),
                new CodeInstruction(OpCodes.Ldc_I4_2),
                new CodeInstruction(OpCodes.Ldc_I4, int.MaxValue),
                new CodeInstruction(OpCodes.Newobj, contructorInfo)
                );
            
            LobbyControl.Log.LogDebug($"Patched PacketSize!");

            return matcher.Instructions();
        }
        
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncShipUnlockablesServerRpc))]
        private static IEnumerable<CodeInstruction> SyncUnlockablesPatch(IEnumerable<CodeInstruction> instructions)
        {
            if (!LobbyControl.PluginConfig.SaveLimit.Enabled.Value)
                return instructions;

            var methodInfo = typeof(GrabbableObject).GetMethod(nameof(GrabbableObject.GetItemDataToSave));

            var codes = instructions.ToList();
            
            var matcher = new CodeMatcher(codes);
            
            matcher.End();
            matcher.MatchBack(false,
                new CodeMatch(OpCodes.Callvirt, methodInfo)
            );

            if (matcher.IsInvalid)
            {
                LobbyControl.Log.LogWarning("SyncShipUnlockablesServerRpc patch failed 1!!");
                LobbyControl.Log.LogDebug(string.Join("\n", matcher.Instructions()));
                return codes;
            }

            matcher.MatchBack(false,
                new CodeMatch(OpCodes.Ldloc_S),
                new CodeMatch(OpCodes.Ldc_I4),
                new CodeMatch(OpCodes.Ble)
                );

            if (matcher.IsInvalid)
            {
                LobbyControl.Log.LogWarning("SyncShipUnlockablesServerRpc patch failed 2!!");
                LobbyControl.Log.LogDebug(string.Join("\n", matcher.Instructions()));
                return codes;
            }

            var labels = matcher.Labels;

            matcher.RemoveInstructions(6);

            matcher.AddLabels(labels);
            
            LobbyControl.Log.LogDebug("Patched SyncShipUnlockablesServerRpc!!");
            
            return matcher.Instructions();
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
        private static IEnumerable<CodeInstruction> SaveItemsInShipPatch(IEnumerable<CodeInstruction> instructions)
        {
            if (!LobbyControl.PluginConfig.SaveLimit.Enabled.Value)
                return instructions;

            var fieldInfo = typeof(StartOfRound).GetField(nameof(StartOfRound.maxShipItemCapacity));
            var methodInfo = typeof(StartOfRound).GetProperty(nameof(StartOfRound.Instance))!.GetMethod;
            
            var codes = instructions.ToList();
            
            var matcher = new CodeMatcher(codes);

            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Ldloc_S),
                new CodeMatch(OpCodes.Call, methodInfo),
                new CodeMatch(OpCodes.Ldfld, fieldInfo),
                new CodeMatch(OpCodes.Bgt)
            );
            
            if (matcher.IsInvalid)
            {
                LobbyControl.Log.LogWarning("SaveItemsInShip patch failed 1!!");
                LobbyControl.Log.LogDebug(string.Join("\n", matcher.Instructions()));
                return codes;
            }
            
            var labels = matcher.Labels;

            matcher.RemoveInstructions(4);

            matcher.AddLabels(labels);
            
            LobbyControl.Log.LogDebug("Patched SaveItemsInShip!!");

            return matcher.Instructions();
        }
    }
}