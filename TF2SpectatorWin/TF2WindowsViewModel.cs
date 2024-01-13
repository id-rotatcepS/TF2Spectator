using AspenWin;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using TF2FrameworkInterface;

namespace TF2SpectatorWin
{
    internal class TF2WindowsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void ViewNotification(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private const char CommandSeparator = '\t';

        public TF2WindowsViewModel()
        {
            // Using ASPEN for common needs
            // logging just goes to the Log view textbox
            ASPEN.Aspen.Log = new TF2SpectatorLog(this);

            // settings load/save to the config file.
            ASPEN.Aspen.Option = new TF2SpectatorSettings(this);
            // initialize primary source from loaded option
            TwitchInstance.AuthToken = Option.Get<string>(nameof(AuthToken));

            // hide tf2 unless they've configured the launch button or haven't configured anything.
            TF2Expanded = !string.IsNullOrEmpty(TF2Path)
                || string.IsNullOrEmpty(BotDetectorLog);
        }

        private ASPEN.AspenLogging Log => ASPEN.Aspen.Log;

        private ASPEN.AspenUserSettings Option => ASPEN.Aspen.Option;

        private void SaveConfig()
        {
            //TODO AspenUserSettings does not include a "save" method.
            ((TF2SpectatorSettings)Option).SaveConfig();
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
                return TF2Instance.CreateCommunications(RconPort, RconPassword);
            }
            catch (Exception e)
            {
                Log.Error("TwitchInstance: " + e.Message);
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
                // instantiating TwitchInstance initializes AuthToken if it wasn't already set. Record it in view/options.
                AuthToken = TwitchInstance.AuthToken;

                ChatCommandDetails classSelection = new ChatCommandDetails(
                                            "tf2 class selection", RedeemClass,
                                            "Select a TF2 class with 1-9 or Scout, Soldier, Pyro, Demoman, Heavy, Engineer, Medic, Sniper, or Spy");
                twitch.AddCommand(classSelection);
                // my reward id in case title changes
                twitch.AddCommand("cbabba18-d1ec-44ca-9e30-59303812a600", classSelection);

                ChatCommandDetails colorSelection = new ChatCommandDetails(
                                            "crosshair aim color...", RedeemColor,
                                            "set my crosshair color by color name or by RGB (0-255, 0-255, 0-255 or #xxxxxx)");
                twitch.AddCommand(colorSelection);
                //TODO add support for a command that just has another command as its action to create aliases in config file.
                // my reward id in case title changes
                //twitch.AddCommand("", colorSelection);
                twitch.AddCommand("!aimColor", colorSelection);

                LoadCommandConfiguration(twitch);

                WatchTBDLogFolder();

                return twitch;
            }
            // error handling is handled on launch command instead.
            //catch(Exception e)
            //{
            //    Log.Error("TwitchInstance: " + e.Message);
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
            foreach (string config in ReadCommandConfig())
            {
                if (config.Trim().Length == 0)
                    continue;
                try
                {
                    ChatCommandDetails command = CreateCommandDetails(config);

                    twitch.AddCommand(command);
                    foreach (string alias in command.Aliases)
                        twitch.AddCommand(alias, command);

                    Log.Info("configured command: " + command.Command);
                }
                catch (Exception)
                {
                    Log.Error("bad command config: " + config);
                }
            }
        }

        private ChatCommandDetails CreateCommandDetails(string config)
        {
            string[] commandParts = config.Split(CommandSeparator);
            string namePart = commandParts[0];
            string commandFormat = commandParts[1];
            string commandHelp = commandParts.Length > 2 ? commandParts[2] : string.Empty;
            string responseFormat = commandParts.Length > 3 ? commandParts[3] : string.Empty;

            string[] names = namePart.Split('|');
            string name = names[0];
            ChatCommandDetails command = new ChatCommandDetails(name,
                CreateChatCommand(commandFormat, responseFormat),
                commandHelp);

            if (names.Length > 1)
                command.Aliases = names.Where(alias => alias != name).ToList();
            return command;
        }

        private ChatCommandDetails.ChatCommand CreateChatCommand(string commandFormat, string responseFormat)
        {
            return (userDisplayName, args) => SendCommandAndProcessResponse(

                CustomFormat(commandFormat, userDisplayName, args),

                (response) =>
                {
                    string chat = CustomFormat(responseFormat, userDisplayName, response);
                    if (!string.IsNullOrWhiteSpace(chat))
                        Twitch?.SendMessageWithWrapping(chat);
                });
        }

        private string CustomFormat(string commandFormat, params string[] args)
        {
            return new CustomCommandFormat(this.SendCommandAndProcessResponse)
                .Format(commandFormat, args);
        }

        private string[] ReadCommandConfig()
        {
            try
            {
                string filename = "TF2SpectatorCommands.tsv";
                return File.ReadAllLines(filename);
            }
            catch (FileNotFoundException)
            {
                // expected
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return commandConfig.Split('\n');
        }

        //read from a file, use this as backup
        private static readonly string commandConfig =
            "!vrmode" + CommandSeparator + "cl_first_person_uses_world_model 1;wait 20000;cl_first_person_uses_world_model 0" + CommandSeparator + "turns on VR mode for a few minutes\n" +
            "!burninggibs" + CommandSeparator + "cl_burninggibs 1;wait 20000;cl_burninggibs 0" + CommandSeparator + "turns on burning gibs for a few minutes\n" +
            "!showposition" + CommandSeparator + "cl_showpos 1;wait 20000;cl_showpos 0" + CommandSeparator + "turns on game position info for a few minutes\n" +

            // requires sv_cheats "!3rdperson" + CommandSeparator + "thirdperson;wait 20000;firstperson" + CommandSeparator + "turns on  for a few minutes\n" +
            // requires sv_cheats "!shake" + CommandSeparator + "shake" + CommandSeparator + "does the demoman charge screenshake\n" +
            "!crosshair" + CommandSeparator + "cl_crosshair_file {0};wait 20000;cl_crosshair_file \"\"" + CommandSeparator + "needs one argument (like crosshair1, crosshair2 ...) - changes the crosshair to the file argument value for a few minutes\n" +
            "die" + CommandSeparator + "kill" + CommandSeparator + "instant death in game\n" +
            "explode" + CommandSeparator + "explode" + CommandSeparator + "explosive instant death in game\n" +

            "!hitSound" + CommandSeparator + "tf_dingalingaling_effect" + CommandSeparator + "What hit sound is in use?" + CommandSeparator + "Current hit sound is {1|0:0 (Default)|1:1 (Electro)|2:2 (Notes)|3:3 (Percussion)|4:4 (Retro)|5:5 (Space)|6:6 (Beepo)|7:7 (Vortex)|8:8 (Squasher)}\n" +

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
            SendCommandAndProcessResponse(
                cmd,
                afterCommand: null);
        }

        private static readonly Regex rgb = new Regex(@".*(\d{1,3})\D+(\d{1,3})\D+(\d{1,3}).*", RegexOptions.IgnoreCase);
        private static readonly Regex hrgb = new Regex(@".*([\dA-F]{2})[^\dA-F]*([\dA-F]{2})[^\dA-F]*([\dA-F]{2}).*", RegexOptions.IgnoreCase);
        private void RedeemColor(string userDisplayName, string arguments)
        {
            try
            {
                //TODO I think ConvertFromString's colors are like "SlateGray" which is not very kind.  Would prefer to be better or use a better tool
                //System.Drawing.Color.FromName(arguments);
                //System.Drawing.ColorTranslator.FromHtml(arguments); // also does #FFFFFF

                // handle color names.
                // This also handles eg. #FFFFFF so we do this first and do our own version if it fails.
                System.Windows.Media.Color clr = (System.Windows.Media.Color)System.Windows.Media.
                    ColorConverter.ConvertFromString(arguments);
                SetColor(clr.R, clr.G, clr.B);
                return;
            }
            catch (FormatException)
            {
                Match rgbMatch = rgb.Match(arguments);
                if (rgbMatch.Success)
                {
                    try
                    {
                        SetColor(GetByte(rgbMatch.Groups[1]), GetByte(rgbMatch.Groups[2]), GetByte(rgbMatch.Groups[3]));
                        return;
                    }
                    catch (Exception)
                    {
                        // values over 255 would do a formatexception or maybe overflow
                    }
                }

                Match hexMatch = hrgb.Match(arguments);
                if (hexMatch.Success)
                {
                    try
                    {
                        SetColor(GetHex(hexMatch.Groups[1]), GetHex(hexMatch.Groups[2]), GetHex(hexMatch.Groups[3]));
                        return;
                    }
                    catch (Exception) { }
                }

                // failure... ideally refund the redeem.
            }
        }

        private void SetColor(byte v1, byte v2, byte v3)
        {
            //cl_crosshair_blue 0;cl_crosshair_green 0;cl_crosshair_red 255
            //aim color is now using {cl_crosshair_red} Red, {cl_crosshair_green} Green, and {cl_crosshair_blue} Blue
            SendCommandAndProcessResponse(string.Format("cl_crosshair_red {0};cl_crosshair_green {1};cl_crosshair_blue {2};", v1, v2, v3),
                afterCommand: null);
        }
        private byte GetByte(Group group)
        {
            return byte.Parse(group.Value, System.Globalization.NumberStyles.Integer);
        }
        private byte GetHex(Group group)
        {
            return byte.Parse(group.Value, System.Globalization.NumberStyles.HexNumber);
        }

        #endregion command configuration

        public string RconPassword
        {
            get => Option.Get<string>(nameof(RconPassword));
            set
            {
                Option.Set(nameof(RconPassword), value?.Trim());
                _tf2 = null;
                ViewNotification(nameof(RconPassword));
                ViewNotification(nameof(IsTF2Connected));
            }
        }

        public ushort RconPort
        {
            get => Option.Get<ushort>(nameof(RconPort));
            set
            {
                Option.Set(nameof(RconPort), value);
                _tf2 = null;
                ViewNotification(nameof(RconPort));
                ViewNotification(nameof(IsTF2Connected));
            }
        }

        public string TF2Path
        {
            get => Option.Get<string>(nameof(TF2Path));
            set
            {
                Option.Set(nameof(TF2Path), value?.Trim());
                // no impact on rcon instance (no _tf2 = null;)
                ViewNotification(nameof(TF2Path));
            }
        }

        #region bot detector log handler
        /// <summary>
        /// path to the folder containing the tf2_bot_detector general log files that include the launch parameters that contain the randomized password and port.
        /// </summary>
        public string BotDetectorLog
        {
            get => Option.Get<string>(nameof(BotDetectorLog));
            set
            {
                Option.Set(nameof(BotDetectorLog), value?.Trim());
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
            ParseMostRecentTBDLogFile();

            StartNewWatcherOrLogWhy();
        }

        private void ParseMostRecentTBDLogFile()
        {
            try
            {
                ParseTBDLogRconValues(GetMostRecentTBDLogFile());
            }
            catch (Exception ex)
            {
                Log.Error("Could not parse bot detector log yet: " + ex.Message);
                // processing most recent failed (maybe there wasn't one)... no problem, Watcher will process the next one that pops up.
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
            // only do this one at a time.  FUTURE: add a custom lock field if we have other things to lock.
            lock (this)
            {
                // access the log file while it is being written.
                using (FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream))
                {
                    ParseTBDLogRconValues(reader);
                }
            }
        }

        /// <summary>
        /// Try once to read the given stream reader for the Rcon configuration information.
        /// </summary>
        /// <param name="reader"></param>
        /// <exception cref="InvalidOperationException">config line was not found in the reader</exception>
        private void ParseTBDLogRconValues(StreamReader reader)
        {
            bool set = false;
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

                RconPassword = pass;
                RconPort = ushort.Parse(port);

                // yes, we found one, but it's the LAST one that matters, unfortunately, so keep going
                set = true;
            }
            if (!set)
                throw new InvalidOperationException("config not found");

            Log.Info("Loading bot detector Rcon settings: " + RconPassword + " " + RconPort);
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

        private void StartNewWatcherOrLogWhy()
        {
            try
            {
                StartNewWatcher();
            }
            catch (Exception ex)
            {
                Log.Error("Error trying to watch bot detector logs (will not automatically configure): " + ex.Message);
            }
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
                Log.Error("Error parsing bot detector log: " + ex.Message);
                // retry
                // TODO prevent infinite loop?
                _ = App.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        Log.Info("trying again");
                        Thread.Sleep(1000); 
                        ParseCreatedTBDLogAndKeepTrying(sender, e);
                    }));
            }
        }

        private void NotifyErrorAndRestartWatcher(object sender, ErrorEventArgs e)
        {
            // reset the watcher.
            //TODO prevent a constant error loop.
            Log.Error("Error watching TBD Logs: " + e.GetException()?.Message);
            WatchTBDLogFolder();
        }
        
        #endregion bot detector log handler

        public string TwitchUsername
        {
            get => Option.Get<string>(nameof(TwitchUsername));
            set
            {
                string v = value?.Trim();
                if (!v.Equals(Option.Get<string>(nameof(TwitchUsername))))
                {
                    Option.Set(nameof(TwitchUsername), v);
                    DisconnectTwitch();
                }
            }
        }

        private void DisconnectTwitch()
        {
            _twitch?.Dispose();
            _twitch = null;
            ViewNotification(nameof(IsTwitchConnected));
        }

        // TwitchInstance is primary source, but need to keep Options up to date.
        public string AuthToken
        {
            get => TwitchInstance.AuthToken;
            set
            {
                TwitchInstance.AuthToken = value?.Trim();
                Option.Set(nameof(AuthToken), TwitchInstance.AuthToken);
                DisconnectTwitch();
            }
        }

        private ICommand _SendCommand;
        public ICommand SendCommand => _SendCommand
            ?? (_SendCommand = new RelayCommand<object>(SendCommandExecute));

        private void SendCommandExecute(object obj)
        {
            string consoleCommand = obj?.ToString() ?? CommandString;

            SendCommandAndProcessResponse(consoleCommand, 
                SetOutputString);
        }


        /// <summary>
        /// runs the TF2 rcon command, and runs an action afterward using the Rcon response.
        /// Note, the Rcon response is the output of all commands in the sequence PRIOR TO a wait command, 
        /// and the response comes back immediately regardless of wait times.
        /// In other words "echo one;echo two;wait 200;echo three" will return "one\ntwo"
        /// Waits for the afterCommand to complete.
        /// </summary>
        /// <param name="consoleCommand"></param>
        /// <param name="afterCommand"></param>
        private void SendCommandAndProcessResponse(string consoleCommand, Action<string> afterCommand)
        {
            Log.Info(consoleCommand);

            if (TF2 == null)
            {
                Log.Warning("no TF2 connection");
                return;
            }

            TF2Command cmd = new StringCommand(consoleCommand);
            Task afterTask = TF2.SendCommand(cmd, s =>
            {
                afterCommand?.Invoke(s);
                Log.Info(cmd + ": " + s);
            });

            afterTask.Wait();
        }

        private void SetOutputString(string response)
        {
            OutputString = response;
            ViewNotification(nameof(OutputString));
        }

        private ICommand _ParseTBDCommand;
        public ICommand ParseTBDCommand => _ParseTBDCommand
            ?? (_ParseTBDCommand = new RelayCommand<object>(ParseTBDCommandExecute));

        private void ParseTBDCommandExecute(object obj)
        {
            ParseMostRecentTBDLogFile();
        }

        private ICommand _LaunchCommand;
        public ICommand LaunchCommand => _LaunchCommand
            ?? (_LaunchCommand = new RelayCommand<object>(LaunchCommandExecute));

        private void LaunchCommandExecute(object obj)
        {
            TF2Instance.LaunchTF2(TF2Path, RconPort, RconPassword);
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
                    DisconnectTwitch();
                    Log.Info("Disconnected Twitch");
                }
                else
                    Log.Info("Connected Twitch: " + Twitch?.TwitchUsername);
            }
            catch (Exception e)
            {
                Log.Error("Twitch Failed: " + e.Message);
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