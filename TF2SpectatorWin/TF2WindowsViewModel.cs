using AspenWin;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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
                TwitchInstance twitch = new TwitchInstance(twitchUsername)
                {
                    ChatCommands = new Dictionary<string, ChatCommandDetails>()
                    {
                        ["tf2 class selection"] = new ChatCommandDetails(
                            "tf2 class selection", RedeemClass,
                            "Select a TF2 class with 1-9 or Scout, Soldier, Pyro, Demoman, Heavy, Engineer, Medic, Sniper, or Spy")
                    }
                };

                LoadCommandConfiguration(twitch.ChatCommands);
                
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

        private void LoadCommandConfiguration(Dictionary<string, ChatCommandDetails> chatCommands)
        {
            foreach (string config in ReadCommandConfig().Split('\n'))
            {
                if (config.Trim().Length == 0)
                    continue;
                try
                {
                    ChatCommandDetails command = CreateCommandDetails(config);

                    chatCommands[command.Command] = command;
                    foreach (string alias in command.Aliases)
                        chatCommands[alias] = command;

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
                (s) => SendCommandExecute(string.Format(commandFormat, CleanArgs(s))),
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
        string commandConfig =
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

        private void RedeemClass(string arguments)
        {
            // TODO scan args for 1/scout etc and attempt to invoke that class change.
        }

        private string CleanArgs(string argumentsAsString)
        {
            if (string.IsNullOrEmpty(argumentsAsString))
                return argumentsAsString;
            return argumentsAsString
                .Replace("\"", "")
                .Replace(';', ',');
        }

        public string RconPassword
        {
            get => TF2Instance.rconPassword;
            set
            {
                TF2Instance.rconPassword = value?.Trim();
                _tf2 = null;
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