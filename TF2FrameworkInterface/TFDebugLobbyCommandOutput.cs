using System.Collections.Generic;
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
        public string Command { get; set; }
        private string CommandOutput { get; set; }

        public Regex Filter { get; set; }

        private TF2Instance TF2 { get; }

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

            if (Filter == null)
                return lines;

            IEnumerable<string> filtered = lines.Where(line => Filter.IsMatch(line));

            return filtered;
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
        public static readonly string TF_GC_TEAM_INVADERS = "TF_GC_TEAM_INVADERS";
        public static readonly string TF_GC_TEAM_DEFENDERS = "TF_GC_TEAM_DEFENDERS";
        public static Regex Matcher => new Regex(
            // tf_debug_lobby command format:
            //CTFLobbyShared: ID:000245b178bebf3b  24 member(s), 0 pending
            //  Member[0][U: 1:117773695]  team = TF_GC_TEAM_INVADERS  type = MATCH_PLAYER
            @"^\s*Member\[(?<member>\d+)\]\s+(?<uniqueid>\[U:\d+:\d+\])\s+team\s*=\s*(?<team>\w+)\s+type\s*=\s*(?<type>\w+).*"
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
