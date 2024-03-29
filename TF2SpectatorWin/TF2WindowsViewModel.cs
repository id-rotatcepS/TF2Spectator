﻿using AspenWin;

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

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
            ASPEN.Aspen.Log = new TF2SpectatorLog(this);

            // settings load/save to the config file.
            ASPEN.Aspen.Option = new TF2SpectatorSettings(this);
            // initialize primary source from loaded option
            TwitchInstance.AuthToken = Option.Get<string>(nameof(AuthToken));

            // hide tf2 unless they've configured the launch button or haven't configured anything.
            TF2Expanded = !string.IsNullOrEmpty(TF2Path)
                || string.IsNullOrEmpty(BotDetectorLog);

            //TODO consider making this part of TwitchInstance except for parts that CommandsEditorModel needs
            commandConfigModel = new CommandConfigModel(this);

            CommandsEditor = new CommandsEditorModel(this);

            botDetectorLogModel = new TF2BotDetectorLogModel(this);
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


        private CommandsEditorModel CommandsEditor;
        public ICommand OpenCommandsCommand => CommandsEditor.OpenCommandsCommand;


        //TODO I think I can move this to TwitchInstance and have the mapping go to that class... except I don't need twitch to read config. Maybe two classes - file & twitch registration.
        private CommandConfigModel commandConfigModel;
        internal string[] ReadCommandConfig() => commandConfigModel.ReadCommandConfig();


        private TF2BotDetectorLogModel botDetectorLogModel;
        public ICommand ParseTBDCommand => botDetectorLogModel.ParseCommand;


        private static TF2Instance _tf2 = null;

        public bool IsTF2Connected => _tf2 != null;

        private TF2Instance TF2 => _tf2
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
                return TF2Instance.CreateCommunications(RconPort, RconPassword);
            }
            catch (Exception e)
            {
                Log.Error("TwitchInstance: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// when it disconnects, clear our reference so we re-establish.
        /// </summary>
        private void TF2InstanceDisconnected()
        {
            _tf2 = null;
            Log.Warning("TwitchInstance: reconnecting");
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

            afterTask.Wait();
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
            ?? (_LaunchTwitchCommand = new RelayCommand<object>(LaunchTwitchCommandExecute, CanTwitchCommandExecute));

        private bool CanTwitchCommandExecute(object arg)
        {
            if (IsTwitchConnected) 
                return true;
         
            return !string.IsNullOrWhiteSpace(TwitchUsername)
                && TwitchUsername != TF2SpectatorSettings.DefaultUserName;
        }

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