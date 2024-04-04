using System.Collections.Generic;
using HarmonyLib;
using LobbyControl.Dependency;
using Steamworks;
using Steamworks.Data;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class LobbyPatcher
    {
        private static readonly Dictionary<Lobby, LobbyType> Visibility = new Dictionary<Lobby, LobbyType>();
        private static readonly Dictionary<Lobby, bool> Open = new Dictionary<Lobby, bool>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Lobby), nameof(Lobby.SetJoinable))]
        private static void TrackOpenStatus(Lobby __instance, object[] __args, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            Open[__instance] = (bool)__args[0];
            
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Status", (bool)__args[0]?"open":"closed");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Lobby), nameof(Lobby.SetPublic))]
        private static void TrackPublicStatus(Lobby __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            Visibility[__instance] = LobbyType.Public;
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Visibility", "public");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Lobby), nameof(Lobby.SetPrivate))]
        private static void TrackPrivateStatus(Lobby __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            Visibility[__instance] = LobbyType.Private;
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Visibility", "private");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Lobby), nameof(Lobby.SetFriendsOnly))]
        private static void trackFriendsOnly(Lobby __instance, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            Visibility[__instance] = LobbyType.FriendsOnly;
            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Visibility", "friends");
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Lobby), nameof(Lobby.SetData))]
        private static void trackData(Lobby __instance, bool __runOriginal, string key, string value)
        {
            if (!__runOriginal)
                return;
            
            if (key == "name" && AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(LobbyControl.NAME, "Lobby.Name", value);
        }

        public static LobbyType GetVisibility(Lobby lobby)
        {
            return Visibility.ContainsKey(lobby) ? Visibility[lobby] : LobbyType.Private;
        }

        public static bool IsOpen(Lobby lobby)
        {
            return !Open.ContainsKey(lobby) || Open.GetValueSafe(lobby);
        }
    }
}