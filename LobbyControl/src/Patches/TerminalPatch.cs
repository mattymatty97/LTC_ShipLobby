using HarmonyLib;
using LobbyControl.TerminalCommands;
using UnityEngine;

namespace LobbyControl.Patches
{
    [HarmonyPatch]
    internal class TerminalPatch
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
        [HarmonyPrefix]
        static void StartPatch(ref TerminalNodesList ___terminalNodes)
        {
            OverrideTerminalNodes(___terminalNodes);
        }

        private static void OverrideTerminalNodes(TerminalNodesList terminalNodes)
        {
            OverrideHelpTerminalNode(terminalNodes);
        }

        private const string COMMAND = "\n\n>LOBBY [command] (lobby name)\ntype lobby help for more info.\n\n";

        private static void OverrideHelpTerminalNode(TerminalNodesList terminalNodes)
        {
            TerminalNode node = null;
            foreach (var keyword in terminalNodes.allKeywords)
            {
                if (keyword.word == "other")
                    node = keyword.specialKeywordResult;
            }

            if (node is null)
                return;

            var defaultMessage = node.displayText;
            string message = defaultMessage.Replace(COMMAND, "").Trim();
            if (GameNetworkManager.Instance.isHostingGame)
                message += COMMAND;
            else
                message += "\n\n";
            node.displayText = message;
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.ParsePlayerSentence))]
        [HarmonyPrefix]
        private static bool ParsePlayerSentencePatch(ref Terminal __instance, ref TerminalNode __result,
            bool __runOriginal)
        {
            if (!__runOriginal)
                return false;

            string[] array = __instance.screenText.text
                .Substring(__instance.screenText.text.Length - __instance.textAdded).Split(' ', 3);

            if (CommandManager.TryExecuteCommand(array, out TerminalNode terminalNode))
            {
                if (terminalNode == null)
                {
                    __result = CreateTerminalNode("Error: terminalNode is null!\n\n");
                    return false;
                }

                __result = terminalNode;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.QuitTerminal))]
        static void QuitTerminalPatch()
        {
            CommandManager.OnTerminalQuit();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
        [HarmonyPrefix]
        static void OnLocalDisconnectPatch()
        {
            CommandManager.OnLocalDisconnect();
        }

        internal static TerminalNode CreateTerminalNode(string message, bool clearPreviousText = true)
        {
            TerminalNode terminalNode = ScriptableObject.CreateInstance<TerminalNode>();

            terminalNode.displayText = message;
            terminalNode.clearPreviousText = clearPreviousText;
            terminalNode.maxCharactersToType = 50;

            return terminalNode;
        }
    }
}