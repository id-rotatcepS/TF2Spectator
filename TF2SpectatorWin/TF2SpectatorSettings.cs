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

        public void ErrorException(Exception exc, string msg) => throw new NotImplementedException();

        public void Info(string msg)
        {
            AddLog(msg);
        }

        public void InfoException(Exception exc, string msg) => throw new NotImplementedException();

        public void Trace(string msg) => throw new NotImplementedException();

        public void Warning(string msg)
        {
            AddLog(msg);
        }

        public void WarningException(Exception exc, string msg) => throw new NotImplementedException();
    }

    /// <summary>
    /// Load/Save user settings keyed by the viewmodel's property names.
    /// </summary>
    internal class TF2SpectatorSettings : ASPEN.AspenUserSettings
    {
        public readonly static string ConfigFileName = "TF2Spectator.config.txt";

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
                string filename = ConfigFileName;
                lines = File.ReadAllLines(filename);
            }
            catch (FileNotFoundException)
            {
                // expected
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.Error(ex.Message);
            }

            options[nameof(TF2WindowsViewModel.TwitchUsername)] = lines.Length > 0 ? lines[0] : "_yourNameHere";
            options[nameof(TF2WindowsViewModel.AuthToken)] = lines.Length > 1 ? lines[1] : string.Empty;

            options[nameof(TF2WindowsViewModel.TF2Path)] = lines.Length > 2 ? lines[2] : @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2";
            options[nameof(TF2WindowsViewModel.RconPassword)] = lines.Length > 3 ? lines[3] : "test";
            ushort portDefault = 48000;
            try
            {
                options[nameof(TF2WindowsViewModel.RconPort)] = ushort.Parse(lines.Length > 4 ? lines[4] : portDefault.ToString());
            }
            catch (Exception ex)
            {
                options[nameof(TF2WindowsViewModel.RconPort)] = portDefault;
                ASPEN.Aspen.Log.Error(ex.Message);
            }

            options[nameof(TF2WindowsViewModel.BotDetectorLog)] = lines.Length > 5 ? lines[5] : string.Empty;
        }

        internal void SaveConfig()
        {
            string filename = ConfigFileName;
            StringBuilder content = new StringBuilder();
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.TwitchUsername)));
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.AuthToken)));

            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.TF2Path)));

            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.RconPassword)));
            content.AppendLine(ASPEN.Aspen.Option.Get<ushort>(nameof(TF2WindowsViewModel.RconPort)).ToString());
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.BotDetectorLog)));

            try
            {
                File.WriteAllText(filename, content.ToString());
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.Error(ex.Message);
            }
        }

    }
}