using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

using ASPEN;

using AspenWin;

using TF2FrameworkInterface;

namespace TF2SpectatorWin
{
    internal class TF2WindowsViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Get the path for this file, 
        /// trying for the ApplicationData (roaming, %appdata%) or else LocalApplicationData folder
        /// in the AssemblyTitle subfolder.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetConfigFilePath(string file)
        {
            string configPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(configPath))
                configPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return GetFilePath(configPath, file);
        }

        private static string GetFilePath(string configPath, string file)
        {
            string title = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;

            string folder = Path.Combine(configPath, title);

            if (!Directory.Exists(folder))
                _ = Directory.CreateDirectory(folder);

            return Path.Combine(folder, file);
        }

        /// <summary>
        /// Always in the Local (non-roaming) path. otherwise the same as <see cref="GetConfigFilePath(string)"/>
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetBackupFilePath(string file)
        {
            string configPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return GetFilePath(configPath, file);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void ViewNotification(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TF2WindowsViewModel()
        {
            // Using ASPEN for common needs
            // logging just goes to the Log view textbox
            Aspen.Log = new TF2SpectatorLog(this);

            // settings load/save to the config file.
            Aspen.Option = new TF2SpectatorSettings(this);

            Aspen.Text = new Text();
            //Aspen.Show = text-based dialogs: new DefaultDialogUtility();
            //Aspen.Track = new DefaultCommandContextUtility();
            //Aspen.Track.Start(Aspen.Track.CreateContextFor("TF2 Spectator"));

            // initialize primary source from loaded option
            TwitchInstance.AuthToken = Option.Get<string>(nameof(AuthToken));

            // hide tf2 unless they haven't configured anything.
            TF2Expanded = string.IsNullOrWhiteSpace(TF2Path)
                && string.IsNullOrWhiteSpace(BotDetectorLog);

            //TODO consider making this part of TwitchInstance except for parts that CommandsEditorModel needs
            commandConfigModel = new CommandConfigModel(this);

            CommandsEditor = new CommandsEditorModel(this);

            botDetectorLogModel = new TF2BotDetectorLogModel(this);

            LobbyTrackerModel = new TF2LobbyTrackerModel(this);
        }

        private AspenLogging Log => Aspen.Log;

        private AspenUserSettings Option => Aspen.Option;

        private void SaveConfig()
        {
            //TODO AspenUserSettings does not include a "save" method.
            ((TF2SpectatorSettings)Option).SaveConfig();
        }

        private ICommand _SaveConfig;
        public ICommand SaveConfigCommand => _SaveConfig
            ?? (_SaveConfig = new RelayCommand<object>((o) => SaveConfig()));


        private CommandsEditorModel CommandsEditor;
        public ICommand OpenCommandsCommand => CommandsEditor.OpenCommandsCommand;


        //TODO I think I can move this to TwitchInstance and have the mapping go to that class... except I don't need twitch to read config. Maybe two classes - file & twitch registration.
        private CommandConfigModel commandConfigModel;
        internal string[] ReadCommandConfig() => commandConfigModel.ReadCommandConfig();


        private TF2BotDetectorLogModel botDetectorLogModel;
        public ICommand ParseTBDCommand => botDetectorLogModel.ParseCommand;


        public TF2LobbyTrackerModel LobbyTrackerModel { get; set; }
        public ICommand OpenLobbyCommand => LobbyTrackerModel.OpenLobbyCommand;
        public ICommand InstallVoteEraserCommand => LobbyTrackerModel.InstallVoteEraserCommand;
        public void SuggestLobbyBotName(string botName) => LobbyTrackerModel?.AddTwitchBotSuggestion(botName);
        public string GetLobbyBots() => LobbyTrackerModel?.GetBotInformation();


        private static TF2Instance _tf2 = null;

        public bool IsTF2Connected => _tf2 != null;

        internal TF2Instance TF2 => _tf2
            ?? SetTF2Instance();

        private TF2Instance SetTF2Instance()
        {
            try
            {
                // make sure we have the latest settings
                if (IsUsingBotDetector)
                    botDetectorLogModel.ParseMostRecentLogFile();

                _tf2 = CreateTF2Instance();
                // make sure we deal with the connection gone bad
                _tf2?.SetOnDisconnected(TF2InstanceDisconnected);

                return _tf2;
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
                // prepare the cfg script the user must exec from the tf2 console that matches this communications instance.
                if (string.IsNullOrWhiteSpace(TF2Path))
                    Log.Warning("TF2 Path is not set - Rcon configuration script not updated");
                else
                    TF2Instance.WriteRconConfigFile(TF2Path, RconPort, RconPassword);

                return TF2Instance.CreateCommunications(RconPort, RconPassword);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "TF2 failed");
                return null;
            }
        }

        /// <summary>
        /// when it disconnects, clear our reference so we re-establish.
        /// </summary>
        private void TF2InstanceDisconnected()
        {
            _tf2 = null;
            Log.Warning("TF2: reconnecting");
        }

        public bool TF2Expanded { get; set; }

        private static TwitchInstance _twitch = null;

        public bool IsTwitchConnected => _twitch != null;

        internal TwitchInstance Twitch => _twitch
            ?? SetTwitchInstance();

        internal TwitchInstance SetTwitchInstance()
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
                TwitchInstance twitch = new TwitchInstance(twitchUsername)
                {
                    ConnectMessage = TwitchConnectMessage
                };
                // instantiating TwitchInstance initializes AuthToken if it wasn't already set. Record it in view/options.
                AuthToken = TwitchInstance.AuthToken;

                LoadSpecialAndConfiguredCommands(twitch);

                botDetectorLogModel.WatchLogFolder();

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

        private void LoadSpecialAndConfiguredCommands(TwitchInstance twitch)
        {
            commandConfigModel.LoadSpecialCommands(twitch);

            commandConfigModel.LoadCommandConfiguration(twitch);
        }

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

        /// <summary>
        /// ...\steamapps\common\Team Fortress 2
        /// </summary>
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

        public string RconConfigFileBase => TF2Instance.RconConfigFileBaseName;

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

        public bool IsUsingBotDetector => Directory.Exists(BotDetectorLog);


        public string TwitchConnectMessage
        {
            get => Option.Get<string>(nameof(TwitchConnectMessage));
            set
            {
                Option.Set(nameof(TwitchConnectMessage), value?.Trim());
                ViewNotification(nameof(TwitchConnectMessage));
            }
        }

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

        public string SteamUUID
        {
            get => Option.Get<string>(nameof(SteamUUID));
            set
            {
                string v = value?.Trim();
                if (!v.Equals(Option.Get<string>(nameof(SteamUUID))))
                {
                    Option.Set(nameof(SteamUUID), v);
                    //TODO reset lobby model
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

        private ICommand _AutoExecCommand;
        public ICommand AutoExecCommand => _AutoExecCommand
            ?? (_AutoExecCommand = new RelayCommand<object>(AutoExecCommandExecute, CanAutoExecCommandExecute));

        private void AutoExecCommandExecute(object obj)
        {
            string cfgPath = GetAutoexecCfgPath();

            File.AppendAllLines(cfgPath,
                new[] {
                    string.Empty, // in case file ends without a newline.
                    "// Perform configuration for TF2 Spectator:",
                    AutoExecLine
                });
        }

        private static string AutoExecLine = string.Format("exec {0}", TF2Instance.RconConfigFileBaseName);

        private bool CanAutoExecCommandExecute(object arg)
        {
            // need path, and either no autoexec, or autoexec doesn't contain our "exec "
            if (string.IsNullOrWhiteSpace(TF2Path))
                return false;

            string cfgPath = GetAutoexecCfgPath();

            if (!File.Exists(cfgPath))
                return true;

            return !File.ReadAllLines(cfgPath).Contains(AutoExecLine);
        }

        private string GetAutoexecCfgPath()
        {
            if (IsUsingMastercomfig())
            {
                string MasterComfigUserPath = Path.Combine(TF2Path, @"tf\cfg\user");
                string AutoexecCfgPathMastercomfig = Path.Combine(MasterComfigUserPath, "autoexec.cfg");
                return AutoexecCfgPathMastercomfig;
            }

            string AutoexecCfgPath = Path.Combine(TF2Path, @"tf\cfg", "autoexec.cfg");
            return AutoexecCfgPath;
        }

        private bool IsUsingMastercomfig()
        {
            if (string.IsNullOrWhiteSpace(TF2Path))
                return false;

            // /tf/custom/mastercomfig*.vpk
            string path = Path.Combine(TF2Path, @"tf\custom");
            return Directory.EnumerateFiles(path).Any(
                n => Path.GetFileName(n).ToLower().StartsWith("mastercomfig")
                && Path.GetExtension(n).ToLower() == ".vpk");
        }

        private ICommand _PlaySoundCommand;
        public ICommand PlaySoundCommand => _PlaySoundCommand
            ?? (_PlaySoundCommand = new RelayCommand<object>(PlaySoundExecute,
                (o) => !string.IsNullOrEmpty(TestSound?.File)));

        private void PlaySoundExecute(object obj)
        {
            TF2Sound sound = (obj as TF2Sound) ?? TestSound;
            if (sound == null) return;

            string consoleCommand = "play " + sound.File;
            SendCommandAndProcessResponse(consoleCommand,
                null);
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
        internal void SendCommandAndProcessResponse(string consoleCommand, Action<string> afterCommand)
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

            bool completed = afterTask.Wait(TF2Instance.COMMAND_TIMEOUT);
        }
        internal void SendCommandAndNoResponse(string consoleCommand)
        {
            SendCommandAndProcessResponse(consoleCommand, null);
        }

        private void SetOutputString(string response)
        {
            OutputString = response;
            ViewNotification(nameof(OutputString));
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
            ?? (_LaunchTwitchCommand = new RelayCommand<object>(ToggleLaunchTwitchCommandExecute, CanTwitchCommandExecute));

        private bool CanTwitchCommandExecute(object arg)
        {
            if (IsTwitchConnected)
                return true;

            return !string.IsNullOrWhiteSpace(TwitchUsername)
                && TwitchUsername != TF2SpectatorSettings.DefaultUserName;
        }

        private void ToggleLaunchTwitchCommandExecute(object obj)
        {
            try
            {
                if (IsTwitchConnected)
                    DisconnectTwitchExecute();
                else
                    ConnectTwitchExecute();
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "Twitch Failed");
            }
        }

        private void DisconnectTwitchExecute()
        {
            DisconnectTwitch();
            Log.Info("Disconnected Twitch");
        }

        private void ConnectTwitchExecute()
        {
            Log.Info("Connected Twitch: " + Twitch?.TwitchUsername);
        }

        internal void ClosingHandler(object sender, CancelEventArgs e)
        {
            SaveConfig();
        }

        public TF2Sound TestSound { get; set; }

        public string CommandString { get; set; }
        public string OutputString { get; set; }

        public string CommandLog { get; set; }
    }

    internal class Text : AspenUserMessages
    {
        public string Formatted(object key, params object[] args)
        {
            // return string.Format(resourceManager.GetString(key), args);
            return string.Format(key as string, args);
        }

        public string Translated(object key)
        {
            throw new NotImplementedException();
        }
    }
}