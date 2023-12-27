using AspenWin;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using TF2FrameworkInterface;

namespace TF2SpectatorWin
{
    internal class TF2WindowsViewModel : INotifyPropertyChanged
    {
        public readonly static string ConfigFileName = "TF2Spectator.config.txt";
        public event PropertyChangedEventHandler PropertyChanged;
        public void ViewNotification(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const char CommandSeparator = '\t';

        public TF2WindowsViewModel()
        {
            InitFromConfig();

            // hide tf2 unless they've configured the launch button or haven't configured anything.
            TF2Expanded = !string.IsNullOrEmpty(TF2Path) 
                || string.IsNullOrEmpty(BotDetectorLog);
        }

        private void InitFromConfig()
        {
            string[] lines = new string[0];
            try
            {
                string filename = ConfigFileName;
                string content = File.ReadAllText(filename);

                lines = content.Split('\n');
            }
            catch (FileNotFoundException)
            {
                // expected
            }
            catch (Exception ex)
            {
                AddLog(ex.Message);
            }

            TwitchUsername = lines.Length > 0 ? lines[0] : "yourNameHere";
            AuthToken = lines.Length > 1 ? lines[1] : string.Empty;

            TF2Path = lines.Length > 2 ? lines[2] : @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2";

            RconPassword = lines.Length > 3 ? lines[3] : "test";
            try
            {
                RconPort = ushort.Parse(lines.Length > 4 ? lines[4] : "48000");
            }
            catch (Exception ex)
            {
                AddLog(ex.Message);
            }
            BotDetectorLog = lines.Length > 5 ? lines[5] : string.Empty;
        }

        private void SaveConfig()
        {
            string filename = ConfigFileName;
            StringBuilder content = new StringBuilder();
            content.AppendLine(TwitchUsername);
            content.AppendLine(AuthToken);

            content.AppendLine(TF2Path);

            content.AppendLine(RconPassword);
            content.AppendLine(RconPort.ToString());
            content.AppendLine(BotDetectorLog);

            try
            {
                File.WriteAllText(filename, content.ToString());
            }
            catch (Exception ex)
            {
                AddLog(ex.Message);
            }
        }

        private ICommand _SaveConfig;
        public ICommand SaveConfigCommand => _SaveConfig
            ?? (_SaveConfig = new RelayCommand<object>((o) => SaveConfig()));

        private static TF2Instance _tf2 = null;

        public bool IsTF2Connected => _tf2 != null;

        private TF2Instance TF2 => _tf2
            ?? SetTF2Instance();

        private TF2Instance SetTF2Instance()
        {
            try
            {
                return _tf2 = CreateTF2Instance();
            }
            finally
            {
                ViewNotification(nameof(IsTF2Connected));
            }
        }

        private TF2Instance CreateTF2Instance()
        {
            try
            {
                return TF2Instance.CreateCommunications();
            }
            catch (Exception e)
            {
                AddLog("TwitchInstance: " + e.Message);
                return null;
            }
        }

        public bool TF2Expanded { get; set; }

        private static TwitchInstance _twitch = null;

        public bool IsTwitchConnected => _twitch != null;

        private TwitchInstance Twitch => _twitch
            ?? SetTwitchInstance();

        private TwitchInstance SetTwitchInstance()
        {
            try
            {
                return _twitch = CreateTwitchInstance(TwitchUsername);
            }
            finally
            {
                ViewNotification(nameof(IsTwitchConnected));
            }
        }

        private TwitchInstance CreateTwitchInstance(string twitchUsername)
        {
            try
            {
                TwitchInstance twitch = new TwitchInstance(twitchUsername);
                ChatCommandDetails classSelection = new ChatCommandDetails(
                                            "tf2 class selection", RedeemClass,
                                            "Select a TF2 class with 1-9 or Scout, Soldier, Pyro, Demoman, Heavy, Engineer, Medic, Sniper, or Spy");
                twitch.AddCommand(classSelection);
                // reward id to make channel points work via messages because I can't get pubsub to work.
                twitch.AddCommand("cbabba18-d1ec-44ca-9e30-59303812a600", classSelection);

                LoadCommandConfiguration(twitch);

                WatchTBDLogFolder();
                
                return twitch;
            }
            // error handling is handled on launch command instead.
            //catch(Exception e)
            //{
            //    AddLog("TwitchInstance: " + e.Message);
            //    return null;
            //}
            finally
            {
                ViewNotification(nameof(AuthToken));
            }
        }

        #region command configuration

        private void LoadCommandConfiguration(TwitchInstance twitch)
        {
            foreach (string config in ReadCommandConfig().Split('\n'))
            {
                if (config.Trim().Length == 0)
                    continue;
                try
                {
                    ChatCommandDetails command = CreateCommandDetails(config);

                    twitch.AddCommand(command);
                    foreach (string alias in command.Aliases)
                        twitch.AddCommand(alias, command);

                    AddLog("configured command: " + command.Command);
                }
                catch (Exception)
                {
                    AddLog("bad command config: " + config);
                }
            }
        }

        private ChatCommandDetails CreateCommandDetails(string config)
        {
            string[] commandParts = config.Split(CommandSeparator);
            string namePart = commandParts[0];
            string commandFormat = commandParts[1];
            string commandHelp = commandParts.Length > 2 ? commandParts[2] : string.Empty;

            string[] names = namePart.Split('|');
            string name = names[0];
            ChatCommandDetails command = new ChatCommandDetails(name,
                (userDisplayName, args) => SendCommandExecute(string.Format(commandFormat, userDisplayName, CleanArgs(args))),
                commandHelp);

            if (names.Length > 1)
                command.Aliases = names.Where(alias => alias != name).ToList();
            return command;
        }

        private string ReadCommandConfig()
        {
            try
            {
                string filename = "TF2SpectatorCommands.tsv";
                return File.ReadAllText(filename);
            }
            catch (FileNotFoundException)
            {
                // expected
            }
            catch (Exception ex)
            {
                AddLog(ex.Message);
            }
            return commandConfig;
        }

        //read from a file, use this as backup
        private static readonly string commandConfig =
            "!vrmode" + CommandSeparator + "cl_first_person_uses_world_model 1;wait 20000;cl_first_person_uses_world_model 0" + CommandSeparator + "turns on VR mode for a few minutes\n" +
            "!burninggibs" + CommandSeparator + "cl_burninggibs 1;wait 20000;cl_burninggibs 0" + CommandSeparator + "turns on burning gibs for a few minutes\n" +
            "!showposition" + CommandSeparator + "cl_showpos 1;wait 20000;cl_showpos 0" + CommandSeparator + "turns on game position info for a few minutes\n" +

            // requires sv_cheats "!3rdperson" + CommandSeparator + "thirdperson;wait 20000;firstperson" + CommandSeparator + "turns on  for a few minutes\n" +
            // requires sv_cheats "!shake" + CommandSeparator + "shake" + CommandSeparator + "does the demoman charge screenshake\n" +
            "!crosshair" + CommandSeparator + "cl_crosshair_file {0};wait 20000;cl_crosshair_file" + CommandSeparator + "needs one argument (like crosshair1, crosshair2 ...) - changes the crosshair to the file argument value for a few minutes\n" +
            "die" + CommandSeparator + "kill" + CommandSeparator + "instant death in game\n" +
            "explode" + CommandSeparator + "explode" + CommandSeparator + "explosive instant death in game\n" +

            "!bigguns" + CommandSeparator + "tf_use_min_viewmodels 0;wait 20000;tf_use_min_viewmodels 1" + CommandSeparator + "turns off \"min viewmodels\" for a few minutes\n" +
            "!hiderate|hiderate" + CommandSeparator + "cl_showfps 0;wait 20000;cl_showfps 1" + CommandSeparator + "turns off the game fps display for a few minutes\n" +
            "!boring" + CommandSeparator + "cl_hud_playerclass_use_playermodel 0;wait 20000;cl_hud_playerclass_use_playermodel 1" + CommandSeparator + "turns off the 3d playermodel for a few minutes\n";

        private static readonly Regex scout = new Regex("scout|Jeremy|scunt|baby|1", RegexOptions.IgnoreCase);
        private static readonly Regex soldier = new Regex("soldier|Jane|Doe|solly|2", RegexOptions.IgnoreCase);
        private static readonly Regex pyro = new Regex("pyro|pybro|flyro|3", RegexOptions.IgnoreCase);
        private static readonly Regex demo = new Regex("demo|Tavish|DeGroot|explo|4", RegexOptions.IgnoreCase);
        private static readonly Regex heavy = new Regex("heavy|Mikhail|Misha|hoovy|fat|5", RegexOptions.IgnoreCase);
        private static readonly Regex engi = new Regex("engi|Dell|Conagher|6", RegexOptions.IgnoreCase);
        private static readonly Regex medic = new Regex("medic|Ludwig|Humboldt|7", RegexOptions.IgnoreCase);
        private static readonly Regex sniper = new Regex("sniper|Mick|Mundy|8", RegexOptions.IgnoreCase);
        private static readonly Regex spy = new Regex("spy|french|france|9", RegexOptions.IgnoreCase);
        private void RedeemClass(string userDisplayName, string arguments)
        {
            // in order of my preference - if they give me somethign ambiguous it gets the first one on this list.
            string joinas;
            if (soldier.IsMatch(arguments))
                joinas = "soldier";
            else if (demo.IsMatch(arguments))
                joinas = "demoman";
            else if (engi.IsMatch(arguments))
                joinas = "engineer";
            else if (medic.IsMatch(arguments))
                joinas = "medic";
            else if (spy.IsMatch(arguments))
                joinas = "spy";
            else if (pyro.IsMatch(arguments))
                joinas = "pyro";
            else if (heavy.IsMatch(arguments))
                joinas = "heavyweapons";
            else if (sniper.IsMatch(arguments))
                joinas = "sniper";
            else if (scout.IsMatch(arguments))
                joinas = "scout";
            else
                joinas = "demoman";

            Twitch.SendMessageWithWrapping(string.Format("Ok, {0}, we will switch to the class '{1}'", userDisplayName, joinas));
            string cmd = "join_class " + joinas;
            SendCommandExecute(cmd);
        }

        private string CleanArgs(string argumentsAsString)
        {
            if (string.IsNullOrEmpty(argumentsAsString))
                return argumentsAsString;

            return argumentsAsString
                .Replace("\"", "")
                .Replace(';', ',');
        }
        #endregion command configuration

        public string RconPassword
        {
            get => TF2Instance.rconPassword;
            set
            {
                TF2Instance.rconPassword = value?.Trim();
                _tf2 = null;
                ViewNotification(nameof(RconPassword));
                ViewNotification(nameof(IsTF2Connected));
            }
        }
        public ushort RconPort
        {
            get => TF2Instance.rconPort;
            set
            {
                TF2Instance.rconPort = value;
                _tf2 = null;
                ViewNotification(nameof(RconPort));
                ViewNotification(nameof(IsTF2Connected));
            }
        }
        public string TF2Path
        {
            get => TF2Instance.path;
            set
            {
                TF2Instance.path = value?.Trim();
                // no impact on rcon instance (no _tf2 = null;)
            }
        }

        #region bot detector log handler
        private string _logFolder = string.Empty;
        /// <summary>
        /// path to the folder containing the tf2_bot_detector general log files that include the launch parameters that contain the randomized password and port.
        /// </summary>
        public string BotDetectorLog
        {
            get => _logFolder;
            set
            {
                _logFolder = value?.Trim();
                ViewNotification(nameof(BotDetectorLog));
            }
        }

        private static readonly string BotDetectorLogPattern = "*.log";
        private FileSystemWatcher watcher;
        /// <summary>
        /// watch tf2_bot_detector log folder for a new file, scan it for rcon port/password, and set those values.
        /// </summary>
        private void WatchTBDLogFolder()
        {
            try
            {
                DisposeOldWatcher();
            }
            catch
            {
                // don't care, just make a new one.
            }

            if (string.IsNullOrEmpty(BotDetectorLog))
                return;

            // First, process the most recent file, then watch for new ones.
            try
            {
                ParseTBDLogRconValues(GetMostRecentTBDLogFile());
            }
            catch (Exception ex)
            {
                AddLog("Could not parse bot detector log yet: " + ex.Message);
                // processing most recent failed (maybe there wasn't one)... no problem, we'll just process the first one that pops up.
            }

            try
            {
                StartNewWatcher();
            }
            catch (Exception ex)
            {
                AddLog("Error trying to watch bot detector logs (will not automatically configure): " + ex.Message);
            }
        }

        // [20:14:56] Processes.cpp(286):Launch: ShellExecute("S:\\Games\\SteamLibrary\\steamapps\\common\\Team Fortress 2\\tf\\..\\hl2.exe", -novid -nojoy -nosteamcontroller -nohltv -particles 1 -precachefontchars -noquicktime dummy -game tf -steam -secure -usercon -high +developer 1 +alias developer +contimes 0 +alias contimes +ip 0.0.0.0 +alias ip +sv_rcon_whitelist_address 127.0.0.1 +alias sv_rcon_whitelist_address +sv_quota_stringcmdspersecond 1000000 +alias sv_quota_stringcmdspersecond +rcon_password xBTQ69yZ61Rb719F +alias rcon_password +hostport 40537 +alias hostport +alias cl_reload_localization_files +net_start +con_timestamp 1 +alias con_timestamp -condebug -conclearlog) (elevated = false)
        // specifically "+rcon_password xBTQ69yZ61Rb719F +alias rcon_password +hostport 40537 +alias hostport"
        private static readonly Regex TBDRconMatcher = new Regex(@"\+rcon_password\s+(\S+)\s+.*\+hostport\s+(\d+)\s+");
        /// <summary>
        /// Try once to read the given log file (possibly still open for writing) for the Rcon configuration information.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <exception cref="InvalidOperationException">config line was not found in the file (so far)</exception>
        private void ParseTBDLogRconValues(string fullPath)
        {
            // access the log file while it is being written.
            using (FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    Match rconMatch = TBDRconMatcher.Match(line);
                    if (!rconMatch.Success)
                        continue;

                    // Reminders:
                    // Groups contains the last $0 $1 etc.  Captures contains any earlier group matches before the last one.
                    // Group 0 is $0 which is the whole string that matched the regex.
                    string pass = rconMatch.Groups[1].Value;
                    string port = rconMatch.Groups[2].Value;

                    AddLog("Loading bot detector Rcon settings: " + pass + " " + port);
                    RconPassword = pass;
                    RconPort = ushort.Parse(port);

                    return;
                }
                throw new InvalidOperationException("config not found");
            }
        }

        private string GetMostRecentTBDLogFile()
        {
            return Directory.EnumerateFiles(BotDetectorLog, BotDetectorLogPattern)
                .OrderByDescending(s => File.GetCreationTime(
                    Path.Combine(BotDetectorLog, s)))
                .First();
        }

        private void DisposeOldWatcher()
        {
            if (watcher == null)
                return;

            // probably unnecessary
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        private void StartNewWatcher()
        {
            watcher = new FileSystemWatcher(BotDetectorLog)
            {
                Filter = BotDetectorLogPattern,

                NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.CreationTime,
            };
            watcher.Created += ParseCreatedTBDLogAndKeepTrying;
            watcher.Error += NotifyErrorAndRestartWatcher;

            watcher.EnableRaisingEvents = true;
        }

        private void ParseCreatedTBDLogAndKeepTrying(object sender, FileSystemEventArgs e)
        {
            try
            {
                ParseTBDLogRconValues(e.FullPath);
            }
            catch (Exception ex)
            {
                AddLog("Error parsing bot detector log: " + ex.Message);
                // retry
                // TODO prevent infinite loop?
                App.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        AddLog("trying again");
                        Thread.Sleep(1000); 
                        ParseCreatedTBDLogAndKeepTrying(sender, e);
                    }));
            }
        }

        private void NotifyErrorAndRestartWatcher(object sender, ErrorEventArgs e)
        {
            // reset the watcher.
            //TODO prevent a constant error loop.
            AddLog("Error watching TBD Logs: " + e.GetException()?.Message);
            WatchTBDLogFolder();
        }

        #endregion bot detector log handler

        private string _username = string.Empty;
        public string TwitchUsername
        {
            get => _username;
            set
            {
                string v = value?.Trim();
                if (!v.Equals(_username))
                {
                    _username = v;
                    _twitch = null;
                    ViewNotification(nameof(IsTwitchConnected));
                }
            }
        }
        public string AuthToken
        {
            get => TwitchInstance.AuthToken;
            set
            {
                TwitchInstance.AuthToken = value?.Trim();
                _twitch = null;
                ViewNotification(nameof(IsTwitchConnected));
            }
        }

        private ICommand _SendCommand;
        public ICommand SendCommand => _SendCommand
            ?? (_SendCommand = new RelayCommand<object>(SendCommandExecute));

        private void SendCommandExecute(object obj)
        {
            AddLog(obj?.ToString());
            if (TF2 == null)
            {
                AddLog("no TF2 connection");
                return;
            }
            TF2Command cmd = new StringCommand(obj?.ToString() ?? CommandString);
            TF2.SendCommand(cmd, s =>
            {
                OutputString = s;
                ViewNotification(nameof(OutputString));

                AddLog(cmd + ": " + s);
            });
        }

        private void AddLog(string msg)
        {
            CommandLog = msg + "\n" + CommandLog;
            ViewNotification(nameof(CommandLog));
        }

        private ICommand _LaunchCommand;
        public ICommand LaunchCommand => _LaunchCommand
            ?? (_LaunchCommand = new RelayCommand<object>(LaunchCommandExecute));

        private void LaunchCommandExecute(object obj)
        {
            TF2Instance.LaunchTF2();
        }

        private ICommand _LaunchTwitchCommand;
        public ICommand LaunchTwitchCommand => _LaunchTwitchCommand
            ?? (_LaunchTwitchCommand = new RelayCommand<object>(LaunchTwitchCommandExecute));

        private void LaunchTwitchCommandExecute(object obj)
        {
            try
            {
                if (IsTwitchConnected)
                {
                    _twitch = null;
                    AddLog("Disconnected Twitch");
                    ViewNotification(nameof(IsTwitchConnected));
                }
                else
                    AddLog("Connected Twitch: " + Twitch?.TwitchUsername);
            }
            catch (Exception e)
            {
                AddLog("Twitch Failed: " + e.Message);
            }
        }

        internal void ClosingHandler(object sender, CancelEventArgs e)
        {
            SaveConfig();
        }

        public string CommandString { get; set; }
        public string OutputString { get; set; }

        public string CommandLog { get; set; }
    }
}