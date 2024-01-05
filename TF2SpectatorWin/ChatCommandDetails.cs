using System.Collections.Generic;

namespace TF2SpectatorWin
{
    public class ChatCommandDetails
    {
        public delegate void ChatCommand(string userDisplayName, string arguments);

        public ChatCommandDetails(string command, ChatCommand commandAction, string helpText)
        {
            Command = command;
            Action = commandAction;
            Help = helpText;
            Aliases = new string[0];
        }

        private ChatCommand Action { get; set; }
        public string Help { get; set; }

        public string Command { get; set; }
        public IEnumerable<string> Aliases { get; internal set; }

        public void InvokeCommand(string userName, string userInput)
        {
            Action?.Invoke(
                userName,
                CleanArgs(userInput));
        }

        private string CleanArgs(string argumentsAsString)
        {
            if (string.IsNullOrEmpty(argumentsAsString))
                return argumentsAsString;

            return argumentsAsString
                .Replace("\"", "")
                .Replace(';', ',');
        }

    }

}