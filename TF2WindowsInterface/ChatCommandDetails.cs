using System.Collections.Generic;

namespace TF2WindowsInterface
{
    public class ChatCommandDetails
    {
        public delegate void ChatCommand(string arguments);

        public ChatCommandDetails(string command, ChatCommand commandAction, string helpText)
        {
            Command = command;
            Action = commandAction;
            Help = helpText;
            Aliases = new string[0];
        }

        public ChatCommand Action { get; set; }
        public string Help { get; set; }

        public string Command { get; set; }
        public IEnumerable<string> Aliases { get; internal set; }
    }

}