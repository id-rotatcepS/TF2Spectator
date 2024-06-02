using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TF2SpectatorWin
{
    /// <summary>
    /// string.Format but if it sees {0|value:newvalue|value2:newvalue2} it maps the arg to new values if found, 
    /// and if it it sees {commmandtext} it runs the command, expects a string result and uses that string result, 
    /// optionally using the same mapping format to further change that result.
    /// </summary>
    public class CustomCommandFormat
    {
        private Action<string, Action<string>> SendCommandAndProcessResponse;

        public CustomCommandFormat(Action<string, Action<string>> commandRunner)
        {
            this.SendCommandAndProcessResponse = commandRunner;
        }

        private static readonly Regex randomCommandRegex = new Regex("{random(?:\\|(?<mapping>[^}]+))}");
        private static readonly Regex mappedCommandRegex = new Regex("{(?<command>\\D[^|}]*)(?:\\|(?<mapping>[^}]+))?}");
        private static readonly Regex mappedArgRegex = new Regex("{(?<numarg>\\d+)\\|(?<mapping>[^}]+)}");

        public string Format(string commandFormat, params object[] args)
        {
            // deal with {random|option|option|option} to replace with a random one of the options
            string randomsReplaced = randomCommandRegex.Replace(
                commandFormat,
                (match) => GetRandom(match.Groups["mapping"])
                );

            // deal with {commandname} to replace arg with running command and getting its result (usually a variable output)
            // deal with {commmandname|value:mapped|value2:mapped2} to do both command and mapping together
            string mappedCommands = mappedCommandRegex.Replace(
                randomsReplaced,
                (match) => GetMappedCommandResult(
                    match.Groups["command"],
                    match.Groups["mapping"])
                );

            // deal with {0|value:mapped|value2:mapped2} to convert arg 0 via map and then format.
            string mappedCommandsAndArgs = mappedArgRegex.Replace(
                mappedCommands,
                (match) => GetMappedIndexedArg(
                    args,
                    match.Groups["numarg"],
                    match.Groups["mapping"])
                );

            // remaining {0} normal format args
            return string.Format(mappedCommandsAndArgs, args);
        }

        private Random randomSource = new Random();
        private string GetRandom(Group mappingGroup)
        {
            CaptureCollection options = mappingGroup.Captures;
            if (options.Count > 0)
            {
                // [0-Count)
                int rdmIndex = randomSource.Next(options.Count);
                return options[rdmIndex].Value;
            }
            return "random";
        }

        private string GetMappedCommandResult(Group commandGroup, Group mappingGroup)
        {
            string command = commandGroup.Value;
            string result = "ERROR";
            try
            {
                SendCommandAndProcessResponse.Invoke(command, (commandResult) =>
                    result = MapResult(commandResult, mappingGroup)
                );
            }
            catch (Exception ex)
            {
                result = "ERROR:" + ex.Message;
            }
            return result;
        }

        private string MapResult(string commandResult, Group mappingGroup)
        {
            if (mappingGroup.Captures.Count > 0)
            {
                Dictionary<string, string> mapping = GetDictionary(mappingGroup.Value);
                if (mapping.ContainsKey(commandResult))
                    return mapping[commandResult];
            }
            return commandResult;
        }

        private Dictionary<string, string> GetDictionary(string value)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            foreach (string entry in value.Split('|'))
            {
                string[] keyValue = entry.Split(':');
                if (keyValue.Length != 2)
                    continue;

                dict[keyValue[0]] = keyValue[1];
            }

            return dict;
        }

        private string GetMappedIndexedArg(object[] args, Group argGroup, Group mappingGroup)
        {
            try
            {
                int argIndex = int.Parse(argGroup.Value);
                object arg = args[argIndex];

                return GetMappedArg(arg, mappingGroup);
            }
            catch
            {
                // parse error or bad index fall through
            }
            return "{" + argGroup.Value + "}";
        }

        private string GetMappedArg(object arg, Group mappingGroup)
        {
            if (arg == null)
                return null;

            string str = arg.ToString();

            Dictionary<string, string> mapping = GetDictionary(mappingGroup.Value);
            if (mapping.ContainsKey(str))
                return mapping[str];

            return str;
        }
    }
}