using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using TF2FrameworkInterface;

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

        internal const string DefaultUserName = "_yourNameHere";
        internal const string DefaultTF2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2";
        internal const string DefaultConnectMessage = "For TF2 Spectator commands, type !help";

        public readonly static string ConfigFilename = "TF2Spectator.config.txt";
        public readonly static string ConfigFilePath = GetConfigFilePath(ConfigFilename);

        public readonly static string BotHandlingConfigFilename = "BotHandlingConfig.json";
        public readonly static string BotHandlingConfigFilePath = GetConfigFilePath(BotHandlingConfigFilename);

        public TF2SpectatorSettings()
        {
            LoadConfig();
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
            options[nameof(TF2WindowsViewModel.SteamUUID)] = lines.Length > 7 ? lines[7] : "[U:1:123456]";

            LoadBotHandlingConfig();
        }

        private void LoadBotHandlingConfig()
        {
            try
            {
                string json = File.ReadAllText(BotHandlingConfigFilePath);
                options[nameof(BotHandlingConfig)] = JsonConvert.DeserializeObject<BotHandlingConfig>(json);
                return;
            }
            catch (FileNotFoundException)
            {
                // expected
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.ErrorException(ex, "Loading BotHandlingConfig");
            }

            //TODO deal with versioning updates - deleting a property should be fine. Adding a property will require per-version population of defaults in new fields.

            // default
            options[nameof(BotHandlingConfig)] = new BotHandlingConfig()
            {
                IsSuggestingMuted = true,
                MutedMessage = ">I muted player '{0}' in the past - bot?    - deciding if I will {3} ('{1}') or not ('{2}')",
                IsSuggestingNames = true,
                NameMessage = ">'{0}' name matches past bots    - deciding if I will {3} ('{1}') or not ('{2}')",
                TwitchSuggestionMessage = ">twitch chat thinks '{0}' is a bot    - deciding if I will {3} ('{1}') or not ('{2}')",

                BotBind = "0",
                NoKickBind = "SEMICOLON",

                SuggestionSound = TF2Sound.SOUNDS.FirstOrDefault(s => s?.Description.StartsWith("HL warning beep") ?? false),
                KickingSound = TF2Sound.SOUNDS.FirstOrDefault(s => s?.Description.StartsWith("HL1 'acquired'") ?? false),
            };
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
            content.AppendLine(ASPEN.Aspen.Option.Get<string>(nameof(TF2WindowsViewModel.SteamUUID)));

            try
            {
                File.WriteAllText(filename, content.ToString());
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.ErrorException(ex, "Saving Config");
            }

            SaveBotHandlingConfig();
        }

        private void SaveBotHandlingConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(ASPEN.Aspen.Option.Get<BotHandlingConfig>(nameof(BotHandlingConfig)));
                File.WriteAllText(BotHandlingConfigFilePath, json);
            }
            catch (Exception ex)
            {
                ASPEN.Aspen.Log.ErrorException(ex, "Saving BotHandlingConfig");
            }
        }
    }
}