using System.Collections.Generic;

namespace LobbyControl.TerminalCommands
{
    internal class CommandManager
    {
        private static List<Command> commands = new List<Command>();
        public static Command awaitingConfirmationCommand;

        public static void Initialize()
        {
            commands = new List<Command>()
            {
                new LobbyCommand()
            };

            awaitingConfirmationCommand = null;
        }

        public static bool TryExecuteCommand(string[] array, out TerminalNode terminalNode)
        {
            terminalNode = null;

            if (awaitingConfirmationCommand != null)
            {
                Command _command = awaitingConfirmationCommand;
                terminalNode = _command.ExecuteConfirmation(array);
                _command.previousTerminalNode = terminalNode;
                return true;
            }

            Command command = GetCommand(array);
            if (command == null) return false;

            terminalNode = command.Execute(array);
            command.previousTerminalNode = terminalNode;
            return true;
        }

        public static void OnLocalDisconnect()
        {
            awaitingConfirmationCommand = null;
        }

        public static void OnTerminalQuit()
        {
            awaitingConfirmationCommand = null;
        }

        private static Command GetCommand(string[] args)
        {
            Command result = null;

            commands.ForEach(command =>
            {
                if (result != null) return;

                if (command.IsCommand(args))
                {
                    result = command;
                }
            });

            return result;
        }
    }
}