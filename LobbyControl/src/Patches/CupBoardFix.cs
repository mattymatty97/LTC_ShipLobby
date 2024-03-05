﻿using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class CupBoardFix
    {
        private static readonly HashSet<GrabbableObject> NoGravityObjects = new HashSet<GrabbableObject>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
        private static void ObjectLoad(NetworkBehaviour __instance)
        {
            if (__instance is GrabbableObject grabbable)
            {
                if (!LobbyControl.PluginConfig.CupBoard.Enabled.Value)
                    return;

                var tolerance = LobbyControl.PluginConfig.CupBoard.Tolerance.Value;
                try
                {
                    var pos = grabbable.transform.position;

                    if (LobbyControl.PluginConfig.OutOfBounds.Enabled.Value &&
                        grabbable.itemProperties.itemSpawnsOnGround)
                        pos -= Vector3.up * LobbyControl.PluginConfig.OutOfBounds.VerticalOffset.Value;

                    var closet = GameObject.Find("/Environment/HangarShip/StorageCloset");
                    PlaceableObjectsSurface[] storageShelves =
                        closet.GetComponentsInChildren<PlaceableObjectsSurface>();
                    var collider = closet.GetComponent<MeshCollider>();
                    var distance = float.MaxValue;
                    PlaceableObjectsSurface found = null;
                    Vector3? closest = null;

                    if (collider.bounds.Contains(pos))
                    {
                        foreach (var shelf in storageShelves)
                        {
                            var hitPoint = shelf.GetComponent<Collider>().ClosestPoint(pos);
                            var tmp = pos.y - hitPoint.y;
                            LobbyControl.Log.LogDebug(
                                $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Shelve is {tmp} away!");
                            if (tmp >= 0 && tmp < distance)
                            {
                                found = shelf;
                                distance = tmp;
                                closest = hitPoint;
                            }
                        }

                        LobbyControl.Log.LogDebug(
                            $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Chosen Shelve is {distance} away!");
                        LobbyControl.Log.LogDebug(
                            $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - With hitpoint at {closest}!");
                    }

                    if (found != null && closest.HasValue)
                    {
                        var transform = grabbable.transform;
                        if (LobbyControl.PluginConfig.ItemClipping.Enabled.Value)
                        {
                            var newPos = ItemClippingPatch.FixPlacement(closest.Value, found.transform, grabbable);
                            transform.position = newPos;
                        }
                        else
                        {
                            transform.position =
                                closest.Value + Vector3.up * LobbyControl.PluginConfig.CupBoard.Shift.Value;
                        }

                        transform.parent = closet.transform;

                        NoGravityObjects.Add(grabbable);
                    }
                    else
                    {
                        //check if we're above the closet
                        var hitPoint = collider.bounds.ClosestPoint(pos);
                        var xDelta = hitPoint.x - pos.x;
                        var zDelta = hitPoint.z - pos.z;
                        var yDelta = pos.y - hitPoint.y;
                        if (Math.Abs(xDelta) < tolerance && Math.Abs(zDelta) < tolerance && yDelta > 0)
                        {
                            LobbyControl.Log.LogDebug(
                                $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Was above the Cupboard!");
                            grabbable.transform.position = pos;
                            
                            NoGravityObjects.Add(grabbable);

                            if (Math.Abs(xDelta) > 0)
                                grabbable.transform.position += new Vector3(xDelta, 0, 0);
                            if (Math.Abs(zDelta) > 0)
                                grabbable.transform.position += new Vector3(0, 0, zDelta);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LobbyControl.Log.LogError($"Exception while checking for Cupboard {ex}");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        private static void ObjectLoad2(GrabbableObject __instance, bool __runOriginal)
        {
            if (!LobbyControl.PluginConfig.CupBoard.Enabled.Value)
                return;
            
            if (!__runOriginal)
                return;
            
            if (!NoGravityObjects.Remove(__instance))
                return;
            
            __instance.fallTime = 1f;
            __instance.hasHitGround = true;
            __instance.reachedFloorTarget = true;
            __instance.targetFloorPosition = __instance.transform.localPosition;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables))]
        private static void CozyImprovementsFix(StartOfRound __instance)
        {
            var closet = GameObject.Find("/Environment/HangarShip/StorageCloset");
            if (closet == null)
                return;

            foreach (var light in closet.GetComponentsInChildren<Light>())
                if (light.gameObject.transform.name == "StorageClosetLight")
                    Object.Destroy(light.gameObject);
        }
    }
}