using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TF2SpectatorWin
{
    /// <summary>
    /// just sends strings to viewmodel's "AddLog" method
    /// </summary>
    internal class TF2SpectatorLog : ASPEN.AspenLogging
    {
        private TF2WindowsViewModel vm;

        public TF2SpectatorLog(TF2WindowsViewModel viewModel)
        {
            this.vm = viewModel;
        }

        private void AddLog(string msg)
        {
            vm.CommandLog = vm.CommandLog + "\n" + msg;
            vm.ViewNotification(nameof(vm.CommandLog));
        }

        public void Error(string msg)
        {
            AddLog(msg);
        }

        public void ErrorException(Exception exc, string msg)
        {
            AddLog(msg + "\n - " + GetMessages(exc));
        }

        private string GetMessages(Exception e)
        {
            string messages = e.Message;
            while (e.InnerException != null)
            {
                e = e.InnerException;
                messages += ";" + e.Message;
            }
            return messages;
        }

        public void Info(string msg)
        {
            AddLog(msg);
        }

        public void InfoException(Exception exc, string msg)
        {
            AddLog(msg + "\n" + GetMessages(exc));
        }

        public void Trace(string msg) => throw new NotImplementedException();

        public void Warning(string msg)
        {
            AddLog(msg);
        }

        public void WarningException(Exception exc, string msg)
        {
            AddLog(msg + "\n" + GetMessages(exc));
        }
    }

    /// <summary>
    /// Load/Save user settings keyed by the viewmodel's property names.
    /// </summary>
    internal class TF2SpectatorSettings : ASPEN.AspenUserSettings
    {
        internal const string DefaultUserName = "_yourNameHere";
        internal const string DefaultTF2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2";
        internal const string DefaultConnectMessage = "For TF2 Spectator commands, type !help";

        public readonly static string ConfigFilename = "TF2Spectator.config.txt";
        public readonly static string ConfigFilePath = TF2WindowsViewModel.GetConfigFilePath(ConfigFilename);

        public TF2SpectatorSettings(TF2WindowsViewModel vm)
        {
            LoadConfig();
            // refresh viewmodel with loaded values.
            vm.ViewNotification(nameof(TF2WindowsViewModel.TwitchUsername));
            vm.ViewNotification(nameof(TF2WindowsViewModel.AuthToken));
            vm.ViewNotification(nameof(TF2WindowsViewModel.TF2Path));
            vm.ViewNotification(nameof(TF2WindowsViewModel.RconPassword));
            vm.ViewNotification(nameof(TF2WindowsViewModel.RconPort));
            vm.ViewNotification(nameof(TF2WindowsViewModel.BotDetectorLog));
            vm.ViewNotification(nameof(TF2WindowsViewModel.TwitchConnectMessage));
        }

        /// <summary>
        /// this is a commonly needed value, but this may not be the best way to provide it.
        /// </summary>
        public string TF2Path
        {
            get => ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.TF2Path));
            set => options[nameof(TF2WindowsViewModel.TF2Path)] = value as string;
        }

        Dictionary<object, object> options = new Dictionary<object, object>();
        public T Get<T>(object key)
        {
            object value = options[key];
            if (value is T t)
                return t;
            throw new InvalidCastException();
        }

        public void Set<T>(object key, T value)
        {
            options[key] = value;
        }

        private void LoadConfig()
        {
            string[] lines = new string[0];
            try
            {
                string filename = ConfigFilePath;
                lines = File.ReadAllLines(filename);
            }
            catch (FileNotFoundException)
            {
                // expected
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.ErrorException(ex, "Load Config");
            }

            options[nameof(TF2WindowsViewModel.TwitchUsername)] = lines.Length > 0 ? lines[0] : DefaultUserName;
            options[nameof(TF2WindowsViewModel.AuthToken)] = lines.Length > 1 ? lines[1] : string.Empty;

            options[nameof(TF2WindowsViewModel.TF2Path)] = lines.Length > 2 ? lines[2] : DefaultTF2Path;
            options[nameof(TF2WindowsViewModel.RconPassword)] = lines.Length > 3 ? lines[3] : "test";
            ushort portDefault = 48000;
            try
            {
                options[nameof(TF2WindowsViewModel.RconPort)] = ushort.Parse(lines.Length > 4 ? lines[4] : portDefault.ToString());
            }
            catch (Exception ex)
            {
                options[nameof(TF2WindowsViewModel.RconPort)] = portDefault;
                ASPEN.Aspen.Log.ErrorException(ex, "parsing Rcon Port");
            }

            options[nameof(TF2WindowsViewModel.BotDetectorLog)] = lines.Length > 5 ? lines[5] : string.Empty;

            options[nameof(TF2WindowsViewModel.TwitchConnectMessage)] = lines.Length > 6 ? lines[6] : DefaultConnectMessage;
        }

        internal void SaveConfig()
        {
            string filename = ConfigFilePath;
            StringBuilder content = new StringBuilder();
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.TwitchUsername)));
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.AuthToken)));

            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.TF2Path)));

            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.RconPassword)));
            content.AppendLine(ASPEN.Aspen.Option.Get<ushort>(nameof(TF2WindowsViewModel.RconPort)).ToString());
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.BotDetectorLog)));
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.TwitchConnectMessage)));

            try
            {
                File.WriteAllText(filename, content.ToString());
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.ErrorException(ex, "Saving Config");
            }
        }

    }
}