using AspenWin;

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
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

            CommandsEditor = new CommandsEditorModel(this);
        }

        internal ASPEN.AspenLogging Log => ASPEN.Aspen.Log;

        internal ASPEN.AspenUserSettings Option => ASPEN.Aspen.Option;

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

                LoadSpecialCommands(twitch);

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

        private void LoadSpecialCommands(TwitchInstance twitch)
        {
            ChatCommandDetails classSelection = new ChatCommandDetails(
                                        "tf2 class selection", RedeemClass,
                                        "Select a TF2 class with 1-9 or Scout, Soldier, Pyro, Demoman, Heavy, Engineer, Medic, Sniper, or Spy");
            twitch.AddCommand(classSelection);

            ChatCommandDetails colorSelection = new ChatCommandDetails(
                                        "crosshair aim color...", RedeemColor,
                                        "set my crosshair color by color name (Teal, Azure, SlateGray...) or by RGB (0-255, 0-255, 0-255 or #xxxxxx)");
            twitch.AddCommand(colorSelection);
        }

        private void LoadCommandConfiguration(TwitchInstance twitch)
        {
            foreach (string config in ReadCommandConfig())
            {
                if (config.Trim().Length == 0)
                    continue;
                try
                {
                    Config configobj = new Config(config);

                    // support for: newAlias existingCommandName unusedHelp  unused
                    if (configobj.Names.Length > 0 && twitch.HasCommand(configobj.CommandFormat))
                    {
                        foreach (string name in configobj.Names)
                            twitch.AddAlias(name, configobj.CommandFormat);
                    }
                    else
                    {
                        ChatCommandDetails command = CreateCommandDetails(configobj);

                        twitch.AddCommand(command);

                        foreach (string alias in command.Aliases)
                            twitch.AddAlias(alias, command.Command);

                        Log.Info("configured command: " + command.Command);
                    }
                }
                catch (Exception)
                {
                    Log.Error("bad command config: " + config);
                }
            }
        }

        private ChatCommandDetails CreateCommandDetails(Config config)
        {
            string name = config.Names[0];
            ChatCommandDetails command = new ChatCommandDetails(name,
                CreateChatCommand(config.CommandFormat, config.ResponseFormat),
                config.CommandHelp);

            if (config.Names.Length > 1)
                command.Aliases = config.Names.Where(alias => alias != name).ToList();
            return command;
        }

        private ChatCommandDetails.ChatCommand CreateChatCommand(string commandFormat, string responseFormat)
        {
            return (userDisplayName, args, messageID) => SendCommandAndProcessResponse(

                CustomFormat(commandFormat, userDisplayName, args),

                (response) =>
                {
                    if (Twitch == null) 
                        return;

                    string chat = CustomFormat(responseFormat, userDisplayName, response);
                    if (!string.IsNullOrWhiteSpace(chat))
                        if (string.IsNullOrEmpty(messageID))
                            Twitch.SendMessageWithWrapping(chat);
                        else
                            Twitch.SendReplyWithWrapping(messageID, chat);

                });
        }

        private string CustomFormat(string commandFormat, params string[] args)
        {
            return new CustomCommandFormat(this.SendCommandAndProcessResponse)
                .Format(commandFormat, args);
        }

        internal string[] ReadCommandConfig()
        {
            try
            {
                string filename = CommandsEditorModel.ConfigFilePath;
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
            "!voteMapA\tnext_map_vote 0\tEnd-of-Round vote for the 1st map option (next_map_vote 0)\t\r\n" +
            "!voteMapB\tnext_map_vote 1\tEnd-of-Round vote for the 2nd map option (next_map_vote 1)\t\r\n" +
            "!voteMapC\tnext_map_vote 2\tEnd-of-Round vote for the 3rd map option (next_map_vote 2)\t\r\n" +
            "!vote|vote next map...\tnext_map_vote {1|a:0|b:1|c:2|first:0|second:1|third:2|current:0|stay:0|zero:0|one:1|two:2}\tEnd-of-Round vote for map choice 0, 1, or 2 (A, B, or C; first, second, or third)\t\r\n" +

            "!hitSound\ttf_dingalingaling_effect\tWhat hit sound is in use?\tCurrent hit sound is {1|0:0 (Default)|1:1 (Electro)|2:2 (Notes)|3:3 (Percussion)|4:4 (Retro)|5:5 (Space)|6:6 (Beepo)|7:7 (Vortex)|8:8 (Squasher)}\r\n" +

            "VR mode\tcl_first_person_uses_world_model 1;tf_taunt_first_person 1;wait 20000;cl_first_person_uses_world_model 0;tf_taunt_first_person 0\tturns on VR mode for a few minutes\t\r\n" +
            //"tf2 die\tkill;wait 1000;kill;wait 1000;kill\tinstant death in game\t\r\n" +
            "tf2 explode\texplode;wait 1000;explode;wait 1000;explode\texplosive instant death in game\t\r\n" +
            //requires script setup "attempt a Taunt Kill\ttaunt_kill\tattempt to do a killing taunt if the right weapon is equipped.\t\r\n" +
            "Big Guns\ttf_use_min_viewmodels 0;wait 20000;tf_use_min_viewmodels 1\tturns off \"min viewmodels\" for a few minutes\t\r\n" +
            "HIDERATE!\tcl_showfps 0;wait 20000;cl_showfps 1\tturns off the game fps display for a few minutes\t\r\n" +
            "boring HUD\tcl_hud_playerclass_use_playermodel 0;wait 20000;cl_hud_playerclass_use_playermodel 1\tturns off the 3d playermodel for a few minutes... kinda boring...like jpuck always has on\t\r\n" +
            "Inspect Item\t+inspect;wait 60;-inspect\tinspect other player items or my held weapon\t\r\n" +
            //requires script setup "SEASONAL!noisemaker|TF2 Noisemaker\tactionLoopToggle\tturn on/off my noisemaker spam if the season/loadout/server allows\t\r\n" +
            "tf2 party chat...\tsay_party {0} in twitch says: '{1}'\te.g. \"!sayParty Hi\" says Hi to my party in tf2\t\r\n" +
            
            "crosshair aim reset\tvoicemenu 0 1;cl_crosshair_file \"\";cl_crosshair_blue 200;cl_crosshair_green 200;cl_crosshair_red 200;cl_crosshair_scale 32;cl_crosshairalpha 200\tgives me normal crosshair settings.\t\r\n" +
            "yes!|!yes\tvoicemenu 0 6;cl_crosshair_file crosshair3;cl_crosshair_blue 0;cl_crosshair_green 255;cl_crosshair_red 0;cl_crosshair_scale 3000;wait 500;cl_crosshair_file \"\";cl_crosshair_blue 200;cl_crosshair_green 200;cl_crosshair_red 200;cl_crosshair_scale 32;cl_crosshairalpha 200\tvisually notify me of a positive indication\t\r\n" +
            "no!|!no\tvoicemenu 0 7;cl_crosshair_file crosshair4;cl_crosshair_blue 0;cl_crosshair_green 0;cl_crosshair_red 255;cl_crosshair_scale 2000;wait 500;cl_crosshair_file \"\";cl_crosshair_blue 200;cl_crosshair_green 200;cl_crosshair_red 200;cl_crosshair_scale 32;cl_crosshairalpha 200\tvisually notify me of a negative indication\t\r\n" +
            "scope in|!scopeIn\tvoicemenu 2 6;cl_crosshair_file crosshair3;cl_crosshair_blue 0;cl_crosshair_green 0;cl_crosshair_red 0;cl_crosshair_scale 3000;wait 20000;cl_crosshair_file \"\";cl_crosshair_blue 200;cl_crosshair_green 200;cl_crosshair_red 200;cl_crosshair_scale 32;cl_crosshairalpha 200\tmake every class stare down the sniper scope for a few minutes.\t\r\n" +
            "cataracts\tvoicemenu 2 5;cl_crosshair_file crosshair5;cl_crosshair_blue 255;cl_crosshair_green 255;cl_crosshair_red 255;cl_crosshair_scale 3000;wait 10000; cl_crosshair_file \"\";cl_crosshair_blue 200;cl_crosshair_green 200;cl_crosshair_red 200;cl_crosshair_scale 32;cl_crosshairalpha 200;\tsimulation of turning 44\t\r\n" +
            "macular degeneration\tvoicemenu 2 5;cl_crosshair_file crosshair5;cl_crosshair_blue 0;cl_crosshair_green 0;cl_crosshair_red 0;cl_crosshair_scale 100;cl_crosshairalpha 200;wait 1000;cl_crosshair_scale 200;wait 1000;cl_crosshair_scale 400;wait 1000;cl_crosshair_scale 800;wait 1000;cl_crosshair_scale 1600;wait 1000;cl_crosshair_scale 3200; wait 5000; cl_crosshair_file \"\";cl_crosshair_blue 200;cl_crosshair_green 200;cl_crosshair_red 200;cl_crosshair_scale 32;cl_crosshairalpha 200;\tsimulation of macular degeneration over time\tWhile there is no cure for macular degeneration, quitting smoking, or never starting, is an important way to prevent AMD.\r\n" +

            "crosshair aim...\tcl_crosshair_file {1|1:crosshair1|2:crosshair2|3:crosshair3|4:crosshair4|5:crosshair5|6:crosshair6|7:crosshair7|reset:|normal:|(.):default|():default|Stock:default|PlusDot:crosshair1|T:crosshair2|TeeDot:crosshair2|o:crosshair3|Circle:crosshair3|x:crosshair4|Ex:crosshair4|.:crosshair5|Dot:crosshair5|PlusOpen:crosshair6|+:crosshair7|Plus:crosshair7}\tneeds one argument (like crosshair1, crosshair2 ...) - changes the crosshair to that file\t\r\n" +
            "!aimPlusDot\tcl_crosshair_file crosshair1\t-!-\t\r\n" +
            "!aimTeeDot\tcl_crosshair_file crosshair2\t-,-\t\r\n" +
            "!aimCircle\tcl_crosshair_file crosshair3\to - a little like the projectile open circle crosshair\t\r\n" +
            "!aimEx\tcl_crosshair_file crosshair4\tx\t\r\n" +
            "!aimDot\tcl_crosshair_file crosshair5\t.\t\r\n" +
            "!aimPlusOpen\tcl_crosshair_file crosshair6\t-:-\t\r\n" +
            "!aimPlus\tcl_crosshair_file crosshair7\t+ - a little like the melee wide cross crosshair \t\r\n" +
            "!aimStock\tcl_crosshair_file default\t(.) - like the weapon-spread crosshair\t\r\n" +
            "!aimBrrr\tcl_crosshair_file notafile\t!%%!\t\r\n" +
            
            "crosshair aim size...\tcl_crosshair_scale {1|default:32|normal:32|giant:3000|big:100}\tneeds number argument - changes crosshair size from default scale size of 32\t\r\n" +
            "crosshair aim red...\tcl_crosshair_red {1}\tset the crosshair red\taim color is now using {cl_crosshair_red} Red, {cl_crosshair_green} Green, and {cl_crosshair_blue} Blue\r\n" +
            "crosshair aim green...\tcl_crosshair_green {1}\tset the crosshair green\taim color is now using {cl_crosshair_red} Red, {cl_crosshair_green} Green, and {cl_crosshair_blue} Blue\r\n" +
            "crosshair aim blue...\tcl_crosshair_blue {1}\tset the crosshair blue\taim color is now using {cl_crosshair_red} Red, {cl_crosshair_green} Green, and {cl_crosshair_blue} Blue\r\n" +
            //requires script setup "Aim Rainbow\talias renderLogo rainbowLogo;logoToggle;wait 20000;logoToggle\trainbow of crosshair colors for a few minutes.\t\r\n" +
            "!aimSlate\tcl_crosshair_red 47;cl_crosshair_green 79;cl_crosshair_blue 79\ta color similar to slate 47 79 79\taim color is now using {cl_crosshair_red} Red, {cl_crosshair_green} Green, and {cl_crosshair_blue} Blue\r\n" +
            
            "hit Default\ttf_dingalingaling_effect \"0\"\t\t\r\n" +
            "hit Electro\ttf_dingalingaling_effect \"1\"\t\t\r\n" +
            "hit Notes\ttf_dingalingaling_effect \"2\"\t\t\r\n" +
            "hit Percussion\ttf_dingalingaling_effect \"3\"\t\t\r\n" +
            "hit Retro\ttf_dingalingaling_effect \"4\"\t\t\r\n" +
            "hit Space\ttf_dingalingaling_effect \"5\"\t\t\r\n" +
            "hit Beepo\ttf_dingalingaling_effect \"6\"\t\t\r\n" +
            "hit Vortex\ttf_dingalingaling_effect \"7\"\t\t\r\n" +
            "hit Squasher\ttf_dingalingaling_effect \"8\"\t\t\r\n" +
            "kill Default\ttf_dingalingaling_last_effect \"0\"\t\t\r\n" +

            //"WHAT DEFAULT\tothertesting\tviewmodel_fov {1};wait 20000;viewmodel_fov 85\t\r\n" +
            //"HUH 75-90\tfov_desired {1};wait 20000;fov_desired 90\t\t\r\n" +
            //"useless?!stopWeather\ttf_particles_disable_weather 0;wait 20000;tf_particles_disable_weather 1\tturns off weather effects for a few minutes? didn't work on carrier's snow.\t\r\n" +
            //"doesntwork!aimOpacity\tcl_crosshairalpha {1}\tneeds a number from 0-255 - changes how opaque the crosshair is from default 200\t\r\n" +
            "pointless!showPosition\tcl_showpos 1;wait 20000;cl_showpos 0\tturns on game position info for a few minutes\t\r\n" +
            "mehdefaultclass\tcl_class\tcurrent default class\tCurrent default class is {1}\r\n" +
            //"DoesntWork!users\tusers\tlist users on server\t{1}\r\n" +
            "tooAnnoyingToUse!tauntByName\ttaunt_by_name {1}\tIf it's equipped (and you got the name right), then it happens! (\"!tauntByName Taunt: The Schadenfreude\")\t\r\n" +
            "test\tcl_allowdownload\ttest of output as arg 1\t{0} result: {1}\r\n" +
            "test2\techo one;echo two;wait 200;echo three\ttest 2 shows result \"two\"\t{0} result: {1}\r\n" +
            "!aimColor\tcrosshair aim color...\t(just an alias)\t\r\n" +
            //requires script setup "!aimRainbow\tAim Rainbow\t\t\r\n" +
            "!aimSize\tcrosshair aim size...\t(just an alias)\t\r\n" +
            "!resetAim\tcrosshair aim reset\t(just an alias)\t\r\n";

        #region TF2ClassHandling

        private static readonly Regex scout = new Regex("scout|Jeremy|scunt|baby|1", RegexOptions.IgnoreCase);
        private static readonly Regex soldier = new Regex("soldier|Jane|Doe|solly|2", RegexOptions.IgnoreCase);
        private static readonly Regex pyro = new Regex("pyro|pybro|flyro|3", RegexOptions.IgnoreCase);
        private static readonly Regex demo = new Regex("demo|Tavish|DeGroot|explo|4", RegexOptions.IgnoreCase);
        private static readonly Regex heavy = new Regex("heavy|Mikhail|Misha|hoovy|fat|5", RegexOptions.IgnoreCase);
        private static readonly Regex engi = new Regex("engi|Dell|Conagher|6", RegexOptions.IgnoreCase);
        private static readonly Regex medic = new Regex("medic|Ludwig|Humboldt|7", RegexOptions.IgnoreCase);
        private static readonly Regex sniper = new Regex("sniper|Mick|Mundy|8", RegexOptions.IgnoreCase);
        private static readonly Regex spy = new Regex("spy|french|france|9", RegexOptions.IgnoreCase);
        private void RedeemClass(string userDisplayName, string arguments, string messageID)
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

            Twitch.SendReplyWithWrapping(messageID, string.Format("Ok, {0}, we will switch to the class '{1}'", userDisplayName, joinas));
            string cmd = "join_class " + joinas;
            SendCommandAndNoResponse(cmd);
        }
        #endregion TF2ClassHandling

        #region ColorHandling
        private void RedeemColor(string userDisplayName, string arguments, string messageID)
        {
            try
            {
                SetColor(arguments);
            }
            catch (Exception)
            {
                // failure... ideally refund the redeem.
            }
        }

        private void SetColor(string arguments)
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
            //catch (FormatException)
            catch (Exception)
            {
            }

            // second chance
            SetColorFromNumbers(arguments);
        }

        private void SetColor(byte r, byte g, byte b)
        {
            //cl_crosshair_blue 0;cl_crosshair_green 0;cl_crosshair_red 255
            //aim color is now using {cl_crosshair_red} Red, {cl_crosshair_green} Green, and {cl_crosshair_blue} Blue
            SendCommandAndNoResponse(string.Format("cl_crosshair_red {0};cl_crosshair_green {1};cl_crosshair_blue {2};", r, g, b));
        }

        private static readonly Regex rgb = new Regex(@".*(\d{1,3})\D+(\d{1,3})\D+(\d{1,3}).*", RegexOptions.IgnoreCase);
        private static readonly Regex hrgb = new Regex(@".*([\dA-F]{2})[^\dA-F]*([\dA-F]{2})[^\dA-F]*([\dA-F]{2}).*", RegexOptions.IgnoreCase);
        private void SetColorFromNumbers(string arguments)
        {
            try
            {
                Match rgbMatch = rgb.Match(arguments);
                if (rgbMatch.Success)
                {
                    SetColor(GetByte(rgbMatch.Groups[1]), GetByte(rgbMatch.Groups[2]), GetByte(rgbMatch.Groups[3]));
                    return;
                }
            }
            catch (Exception)
            {
                // values over 255 would do a formatexception or maybe overflow
            }

            try
            {
                Match hexMatch = hrgb.Match(arguments);
                if (hexMatch.Success)
                {
                    SetColor(GetHex(hexMatch.Groups[1]), GetHex(hexMatch.Groups[2]), GetHex(hexMatch.Groups[3]));
                    return;
                }
            }
            catch (Exception)
            {
                // use the common exception
            }

            throw new FormatException("could not parse a color from " + arguments);
        }
        private byte GetByte(Group group)
        {
            return byte.Parse(group.Value, System.Globalization.NumberStyles.Integer);
        }
        private byte GetHex(Group group)
        {
            return byte.Parse(group.Value, System.Globalization.NumberStyles.HexNumber);
        }
        #endregion ColorHandling

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
        private void SendCommandAndNoResponse(string consoleCommand)
        {
            SendCommandAndProcessResponse(consoleCommand, null);
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