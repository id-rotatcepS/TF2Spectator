﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TF2FrameworkInterface
{
    // TODO looks like some further detail will require the -devmode stuff 
    // tf2_bot_detector: "Getting players in the current game is done via the tf_lobby_debug and status commands." - neither is devmode
    // there is also a tf_server_lobby_debug but that's "game" not "client"...and it does nothing apparently?
    // but how does he get kills/points?
    // vote - ncmd - game - Vote for an option in a currently running vote.Usage: vote<vote index> <option1-5>
    // callvote issue
    // listissues - cmd - game - List all the issues that can be voted on

    /* rcon tf_lobby_debug
CTFLobbyShared: ID:000245b178bebf3b  24 member(s), 0 pending
  Member[0] [U:1:117773695]  team = TF_GC_TEAM_INVADERS  type = MATCH_PLAYER
  Member[2] [U:1:925813503]  team = TF_GC_TEAM_INVADERS  type = MATCH_PLAYER
    ...
  Member[23] [U:1:123650837]  team = TF_GC_TEAM_INVADERS  type = MATCH_PLAYER
  Member[21] [U:1:1308235279]  team = TF_GC_TEAM_DEFENDERS  type = MATCH_PLAYER
    */
    /* OR (not connected to a VALVE server)
     * Failed to find lobby shared object
     */
    public class TFDebugLobbyCommandOutput
    {
        public TFDebugLobbyCommandOutput(TF2Instance tf2)
        {
            TF2 = tf2;
            Command = "tf_lobby_debug";
            Filter = TF2DebugLobby.Matcher;
        }
        private string Command { get; set; }
        private string CommandOutput { get; set; }

        private Regex Filter { get; set; }

        private TF2Instance TF2 { get; }

        public string LobbyID { get; private set; }
        /// <summary>
        /// LobbyID was updated, so previous lobby's status is outdated.
        /// </summary>
        public event LobbyEvent LobbyServerChanged;

        public IEnumerable<TF2DebugLobby> GetNewStatus()
        {
            RunCommand();
            return TF2DebugLobbyStatus;
        }

        public IEnumerable<TF2DebugLobby> TF2DebugLobbyStatus
            => (CommandResults ?? new List<string>())
            .Select(line => new TF2DebugLobby(Filter.Match(line)));

        protected IEnumerable<string> CommandResults
            => FilteredLines(CommandOutput);

        public IEnumerable<string> GetNewCommandResults()
        {
            RunCommand();
            return CommandResults;
        }

        // mainly to filter out the first line with "members" and "pending" counts
        private IEnumerable<string> FilteredLines(string text)
        {
            if (text == null)
                return null;

            string[] lines = ExtractCommandLines(text);

            UpdateLobbyID(lines);

            if (Filter == null)
                return lines;

            IEnumerable<string> filtered = lines.Where(line => Filter.IsMatch(line));

            return filtered;
        }

        private void UpdateLobbyID(string[] lines)
        {
            string lobbyHeader = lines.SingleOrDefault(line => TF2DebugLobby.ServerMatcher.IsMatch(line));
            if (lobbyHeader == null)
                return;

            LobbyID = TF2DebugLobby.ServerMatcher.Match(lobbyHeader).Groups["serverid"].Value;
            LobbyServerChanged?.Invoke(this);
        }

        private static string[] ExtractCommandLines(string text)
        {
            return text.Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        public delegate void LobbyEvent(TFDebugLobbyCommandOutput source);
        public event LobbyEvent LobbyUpdated;

        protected void RunCommand()
        {
            if (TF2 == null)
                return;
            if (string.IsNullOrWhiteSpace(Command))
                return;

            StringCommand cmd = new StringCommand(Command);

            Task afterTask = TF2.SendCommand(cmd,
                (result) =>
                {
                    CommandOutput = result;
                    LobbyUpdated?.DynamicInvoke(this);
                });
            bool completed = afterTask.Wait(TF2Instance.COMMAND_TIMEOUT);
        }
    }

    /// <summary>
    /// a row of data from the "status" tf2 console command.
    /// </summary>
    public class TF2DebugLobby
    {
        /// <summary>
        /// counter to how a/d and payload games work, "invaders" can mean RED team on 2nd round.
        /// </summary>
        public static readonly string TF_GC_TEAM_INVADERS = "TF_GC_TEAM_INVADERS";
        /// <summary>
        /// counter to how a/d and payload games work, "defenders" can mean BLU team on 2nd round.
        /// </summary>
        public static readonly string TF_GC_TEAM_DEFENDERS = "TF_GC_TEAM_DEFENDERS";
        public static Regex Matcher => new Regex(
            // tf_debug_lobby command format:
            //CTFLobbyShared: ID:000245b178bebf3b  24 member(s), 0 pending
            //  Member[0][U: 1:117773695]  team = TF_GC_TEAM_INVADERS  type = MATCH_PLAYER
            @"^\s*Member\[(?<member>\d+)\]\s+(?<uniqueid>\[U:\d+:\d+\])\s+team\s*=\s*(?<team>\w+)\s+type\s*=\s*(?<type>\w+).*"
            );
        public static Regex ServerMatcher => new Regex(
            //CTFLobbyShared: ID:000245b178bebf3b  24 member(s), 0 pending
            @"^\s*\w+:\s+ID:(?<serverid>[\da-f]+)\s+(?<count>\d+)\s+member\(s\),\s*(?<pending>\d+)\s*pending.*"
            );

        /// <summary>
        /// the lobby member number e.g. "73" for "Member[73]"
        /// not really useful - does not match the userid from status.
        /// </summary>
        public string MemberNumber { get; private set; }
        /// <summary>
        /// the lobby "team" e.g. TF_GC_TEAM_INVADERS
        /// <see cref="TF_GC_TEAM_DEFENDERS"/> <see cref="TF_GC_TEAM_INVADERS"/>
        /// </summary>
        public string Team { get; internal set; }
        /// <summary>
        /// uniqueid e.g. [U:1:132658037]
        /// </summary>
        public string SteamUniqueID { get; private set; }
        /// <summary>
        /// the lobby "type" in game status e.g. MATCH_PLAYER
        /// (tf2bd just enumerates: Player or InvalidPlayer)
        /// </summary>
        public string PlayerType { get; private set; }

        public TF2DebugLobby(Match match)
        {
            MemberNumber = match.Groups["member"].Value;
            SteamUniqueID = match.Groups["uniqueid"].Value;
            Team = match.Groups["team"].Value;
            PlayerType = match.Groups["type"].Value;
        }
    }

}
