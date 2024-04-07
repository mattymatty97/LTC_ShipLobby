using System.Collections.Generic;
using System.Text;

namespace LobbyControl.TerminalCommands
{
    internal class CommandManager
    {
        private static List<Command> commands = new List<Command>();
        public static Command awaitingConfirmationCommand;

        public static void Initialize()
        {
            commands = new List<Command>(){
                new LobbyCommand()
            };

            awaitingConfirmationCommand = null;
        }

        public static bool TryExecuteCommand(string[] array, out TerminalNode terminalNode)
        {
            terminalNode = null;

            string[] args = GetArgs(array, 3);

            if (awaitingConfirmationCommand != null)
            {
                Command _command = awaitingConfirmationCommand;
                terminalNode = _command.ExecuteConfirmation(args);
                _command.previousTerminalNode = terminalNode;
                return true;
            }

            Command command = GetCommand(args);
            if (command == null) return false;

            terminalNode = command.Execute(args);
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

        private static string[] GetArgs(string[] array, int length)
        {
            List<string> args = new List<string>();
            StringBuilder sb = new StringBuilder();
            int count = 0;

            foreach (string arg in array)
            {
                if (arg.Trim() == string.Empty) 
                    continue;
                count++;
                if (count < length)
                    args.Add(arg.Trim());
                else
                {
                    sb.Append(arg.Trim()).Append(" ");
                }
            }
            
            if (sb.Length > 0)
                args.Add(sb.ToString().Trim());

            if (args.Count > length) return args.ToArray();

            for (int i = 0; i < length - args.Count; i++)
            {
                args.Add(string.Empty);
            }

            return args.ToArray();
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