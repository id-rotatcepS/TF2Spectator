using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using System.Threading;

namespace TF2FrameworkInterface
{

    // tf_debug_lobby (on valve servers) gives regular "who's connected and on what team"
    // status populates detail of name (cache it) and connection time (extrapolate from it and time gathered) and I guess ping
    // voiceban populates detail of muted... hopefully updates real time.

    // "rota: you had ____ muted. press zero(0) in 30s to mark as a bot/cheater and kick, semicolon(;) to decline."
    //   (temporary binds set up to set variables, cleared in 30s)

    // "____ marked as a bot/cheater. press zero(0) to start a vote kick." & play a sound (options turn off the message).
    // can play sounds from built-in resources - suggested for tf2bd:
    //play player/cyoa_pda_beep4.wav(cheater joins your team)
    //play player/cyoa_pda_beep3.wav(cheater joins enemy team)
    //play replay/rendercomplete.wav(optionally, all cheaters have left)

    //   (temporary bind set up to start kick) - because the constant failed vote hiding thing is a feature I'm not adding.
    //   (attempt to append UID to voicebans)


    /// <summary>
    /// This class embodies a Re-usable rcon sequence that will include output via a log file.
    /// Turns out to be a little difficult - have to wait on tf2 to flush the new file out odd intervals, 
    /// and have to convince it to flush access to the file after clearing the filename.
    /// 
    /// con_filter_text "[U:";con_filter_enable 1;con_logfile "myoutput.txt"; status; con_logfile "";con_filter_enable 0; con_filter_text ""
    /// unfortunately filters don't apply to the log, so just
    /// clear file then
    /// con_logfile "myoutput.txt"; status; con_logfile "";
    /// we can do our own filtering.  Maybe even accept filter in definition but do it manually instead of from con_filter
    /// </summary>
    public class CommandLogOutput
    {
        public CommandLogOutput(TF2Instance tf2, string tf2Path)
        {
            TF2 = tf2;
            TF2Path = tf2Path;
        }

        public string Command { get; set; }

        /// <summary>
        /// Filter is important because other text from game events WILL APPEAR in the middle of your output
        /// </summary>
        public Regex Filter { get; set; }

        public string LogFileName { get; set; } = "TF2SpectatorCommandLogOutput.txt";//.ToLower(); // apparently it ignores case when you set log file name?

        protected string LogFileContent { get; set; }

        protected string LogFilePath
            => Path.Combine(TF2Path, "tf", LogFileName);

        //public delegate void StatusEvent(StatusCommandLogOutput source);
        //public event StatusEvent StatusUpdated;

        private IEnumerable<string> LastLogResultsFiltered = null;
        protected IEnumerable<string> LogResultsFiltered
        {
            get
            {
                IEnumerable<string> filtered = FilteredLines(LogFileContent);
                if (filtered != null && filtered.Any())
                {
                    LastLogResultsFiltered = filtered;
                    //TODO results in an update stack overflow.  But now I'm not getting notifications when I have status.
                    //StatusUpdated?.DynamicInvoke(this);
                }

                return LastLogResultsFiltered;
            }
        }

        private TF2Instance TF2 { get; }
        private string TF2Path { get; }

        public IEnumerable<string> GetNewLogResults()
        {
            RunLoggedCommand();
            return LogResultsFiltered;
        }

        private IEnumerable<string> FilteredLines(string text)
        {
            if (text == null)
                return null;

            string[] lines = ExtractLogLines(text);

            if (Filter == null)
                return lines;

            IEnumerable<string> filtered = lines.Where(
                line =>
                Filter.IsMatch(line)
                );

            return filtered;
        }

        private static string[] ExtractLogLines(string text)
        {
            return text.Split(new string[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        protected void RunLoggedCommand()
        {
            if (string.IsNullOrWhiteSpace(Command))
                return;
            if (LogFileName == null)
                return;

            //LoggedCommand loggedCommand = new LoggedCommand(Command, LogFileName);

            try
            {
                File.Delete(LogFilePath);

                bool completed =
                // have to set log file in a separate line for it to actually start logging.
                TF2.SendCommand(new StringCommand($";con_logfile \"\";echo end;con_logfile \"{LogFileName}\""
                    ), (result) => { })
                    .Wait(TF2Instance.COMMAND_TIMEOUT);

                if (completed) completed = TF2.SendCommand(new StringCommand(
                    "wait 30" // let the new logfile setting set in.
                    + ";" +
                    Command
                //    ),
                //    (result) => { }) // not interested - the log is what's interesting.
                //    .Wait(TF2Instance.COMMAND_TIMEOUT);
                +
                    //TF2.SendCommand(new StringCommand(
                    ";echo end;wait 30;" +// let the command write before closing the log file.
                    $"con_logfile \"\"" +
                    $";wait 30;echo end;"// force log to drop reference to the file.
                    ), (result) => { })
                    .Wait(TF2Instance.COMMAND_TIMEOUT);
                //TF2.SendCommand(loggedCommand,
                //    (result) => { }) // not interested - the log is what's interesting.
                //    .Wait(TF2Instance.COMMAND_TIMEOUT);

                // give the file time to appear
                if (completed)
                {
                    for (int i = 0; i < 5 && !File.Exists(LogFilePath); ++i)
                    {
                        Thread.Sleep(1000);
                    }
                    LogFileContent = File.ReadAllText(LogFilePath);
                }
            }
            catch
            {
                // file may be locked still, so we can't delete... try again later after another attempt to change filename
                // also maybe file could not exist for reading because...I dunno, slow flush I guess.
                return;
            }
            finally
            {
                _ = TF2.SendCommand(new StringCommand($"con_logfile \"\""), (result) => { });
            }
        }
    }

    //internal class LoggedCommand : TF2Command
    //{
    //    public LoggedCommand(string command, string logFileName)
    //    {
    //        ConsoleString =
    //            $"con_logfile \"{logFileName}\" ; {command} ; con_logfile \"\";";
    //    }
    //}

    /// <summary>
    /// Re-usable Log-based command where command is "status" with a filter that only includes lines of user status.
    /// </summary>
    public class StatusCommandLogOutput : CommandLogOutput
    {
        public StatusCommandLogOutput(TF2Instance tf2, string tf2Path)
            :base(tf2, tf2Path)
        {
            Command = "status";
            Filter = TF2Status.Matcher;
        }

        public IEnumerable<TF2Status> GetNewStatus()
        {
            RunLoggedCommand();
            return LogStatus;
        }

        public IEnumerable<TF2Status> LogStatus 
            => (LogResultsFiltered ?? new List<string>())
            .Select(line => new TF2Status(Filter.Match(line)));
    }

    /// <summary>
    /// a row of data from the "status" tf2 console command.
    /// </summary>
    public class TF2Status
    {
        public static Regex Matcher => new Regex(
            // status command format:
            //# userid name                uniqueid            connected ping loss state
            //#    744 "rotatcepS ⚙"     [U:1:123605865]     02:31       95    0 active
            // turns out connected could have an hour part... just in case allowing for empty minute part
            @"^#\s+(?<userid>\d+)\s+\""(?<name>.*)\""\s+(?<uniqueid>\[U:\d+:\d+\])\s+(?:(?:(?<connected_hr>\d+):)?(?<connected_min>\d+):)?(?<connected_sec>\d?\d)\s+(?<ping>\d+)\s+(?<loss>\d+)\s+(?<state>.*)"
            );

        /// <summary>
        /// "userid" in game status.  e.g. 744
        /// </summary>
        public string GameUserID { get; private set; }
        /// <summary>
        /// "name" in game status.
        /// </summary>
        public string UserName { get; private set; }
        /// <summary>
        /// "uniqueid" in game status. e.g. [U:1:132658037]
        /// </summary>
        public string SteamUniqueID { get; private set; }
        /// <summary>
        /// "connected" in game status. e.g. 02:31 is returned here as 151
        /// </summary>
        public int ConnectedSeconds { get; private set; }
        /// <summary>
        /// "ping" in game status.
        /// </summary>
        public int Ping { get; private set; }
        /// <summary>
        /// "loss" in game status. Usually 0
        /// </summary>
        public int Loss { get; private set; }
        /// <summary>
        /// "state" in game status e.g. "active" or "spawning"
        /// (per tf2bd: Invalid, Challenging, Connecting, Spawning, Active)
        /// </summary>
        public string UserState { get; private set; }

        public TF2Status(Match match)
        {
            GameUserID = match.Groups["userid"].Value;
            UserName = match.Groups["name"].Value;
            SteamUniqueID = match.Groups["uniqueid"].Value;

            int hrs = GetOptionalInt(match, "connected_hr");
            int min = GetOptionalInt(match, "connected_min");
            //min += hrs * 60;
            int sec = GetOptionalInt(match, "connected_sec");
            //sec += min * 60;
            ConnectedSeconds = sec + (min * 60) + (hrs * 60 * 60);

            Ping = GetInt(match.Groups["ping"].Value);
            Loss = GetInt(match.Groups["loss"].Value);
            UserState = match.Groups["state"].Value;
        }

        private int GetOptionalInt(Match match, string groupname)
        {
            int val = 0;
            if (match.Groups[groupname].Success)
                val = GetInt(match.Groups[groupname].Value);
            return val;
        }

        private int GetInt(string value)
        {
            try
            {
                return int.Parse(value);
            }
            catch
            {
                return 0;
            }
        }

        public bool IsDifferent(TF2Status lastGoodStatus)
        {
            if (lastGoodStatus == null)
                return true;

            return SteamUniqueID != lastGoodStatus.SteamUniqueID
                || ConnectedSeconds != lastGoodStatus.ConnectedSeconds
                || UserName != lastGoodStatus.UserName
                || UserState != lastGoodStatus.UserState
                || GameUserID != lastGoodStatus.GameUserID
                ;
        }

    }

}
