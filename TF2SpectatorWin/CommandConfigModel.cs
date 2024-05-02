using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TF2SpectatorWin
{
    /// <summary>
    /// Custom and Configured command configurations to load into the viewmodel's twitch instance.
    /// </summary>
    internal class CommandConfigModel
    {
        private TF2WindowsViewModel vm;

        private ASPEN.AspenLogging Log => ASPEN.Aspen.Log;

        public CommandConfigModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.vm = tF2WindowsViewModel;
        }

        public void LoadSpecialCommands(TwitchInstance twitch)
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

        public void LoadCommandConfiguration(TwitchInstance twitch)
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
            return (userDisplayName, args, messageID) => vm.SendCommandAndProcessResponse(

                CustomFormat(commandFormat, userDisplayName, args),

                (response) =>
                {
                    if (vm.Twitch == null)
                        return;

                    string chat = CustomFormat(responseFormat, userDisplayName, response);
                    if (!string.IsNullOrWhiteSpace(chat))
                        if (string.IsNullOrEmpty(messageID))
                            vm.Twitch.SendMessageWithWrapping(chat);
                        else
                            vm.Twitch.SendReplyWithWrapping(messageID, chat);

                });
        }

        private string CustomFormat(string commandFormat, params string[] args)
        {
            return new CustomCommandFormat(vm.SendCommandAndProcessResponse)
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
                Log.ErrorException(ex, "Error Reading Commands");
            }
            return commandConfig.Split('\n');
        }

        //read from a file, use this as backup
        private static readonly string commandConfig =
            //toggle x // is same as incrementvar x 0 1 1 // is same as toggle x 0 1 // toggle can get a list of values to toggle through as additional args.

            "!voteMapA\tnext_map_vote 0\tEnd-of-Round vote for the 1st map option (next_map_vote 0)\t\r\n" +
            "!voteMapB\tnext_map_vote 1\tEnd-of-Round vote for the 2nd map option (next_map_vote 1)\t\r\n" +
            "!voteMapC\tnext_map_vote 2\tEnd-of-Round vote for the 3rd map option (next_map_vote 2)\t\r\n" +
            "!vote|vote next map...\tnext_map_vote {1|a:0|b:1|c:2|first:0|second:1|third:2|current:0|stay:0|zero:0|one:1|two:2}\tEnd-of-Round vote for map choice 0, 1, or 2 (A, B, or C; first, second, or third)\t\r\n" +

            "!hitSound\ttf_dingalingaling_effect\tWhat hit sound is in use?\tCurrent hit sound is {1|0:0 (Default)|1:1 (Electro)|2:2 (Notes)|3:3 (Percussion)|4:4 (Retro)|5:5 (Space)|6:6 (Beepo)|7:7 (Vortex)|8:8 (Squasher)}\r\n" +

            "VR mode\tcl_first_person_uses_world_model 1;tf_taunt_first_person 1;wait 20000;cl_first_person_uses_world_model 0;tf_taunt_first_person 0\tturns on VR mode for a few minutes\t\r\n" +
            //"tf2 die\tkill;wait 1000;kill;wait 1000;kill\tinstant death in game\t\r\n" +
            "tf2 explode\texplode;wait 1000;explode;wait 1000;explode\texplosive instant death in game\t\r\n" +
            //requires script setup "attempt a Taunt Kill\ttaunt_kill\tattempt to do a killing taunt if the right weapon is equipped.\t\r\n" +
            "Big Guns\ttoggle tf_use_min_viewmodels;wait 20000;toggle tf_use_min_viewmodels\ttoggles \"min viewmodels\" for a few minutes\tGuns are {tf_use_min_viewmodels|1:small|0:big} for a few minutes\r\n" +
            "HIDERATE!\tcl_showfps 0;wait 20000;cl_showfps 1\tturns off the game fps display for a few minutes\t\r\n" +
            "boring HUD\ttoggle cl_hud_playerclass_use_playermodel;wait 20000;toggle cl_hud_playerclass_use_playermodel\ttoggles the 3d playermodel for a few minutes... kinda boring when off...like jpuck always has it\t3d player class model is {cl_hud_playerclass_use_playermodel|1:on|0:off} for a few minutes\r\n" +

            "Black & White\tmat_color_projection 4;wait 20000;mat_color_projection 0\tChanges the game to Black & White for a few minutes\t\r\n" +
            "Pixelated\tmat_viewportscale 0.1;wait 20000;mat_viewportscale 1\tChanges the game to giant pixels for a few minutes\t\r\n" +
            "Dream Mode\tmat_bloom_scalefactor_scalar 50;wait 20000;mat_bloom_scalefactor_scalar 1\tGives a lighting bloom effect like we're playing in a glowy dream\t\r\n" +
            "No Guns\ttoggle r_drawviewmodel;wait 20000;toggle r_drawviewmodel\ttoggles \"draw viewmodel\" for a few minutes\tGuns are {r_drawviewmodel|1:on|0:off} for a few minutes\r\n" +
            "Long Arms\tviewmodel_fov 160;wait 20000;viewmodel_fov 54\tmaxes out \"viewmodel FOV\" for a few minutes\t\r\n" +
            //" extreme looking:\r\n cl_pitchup/down 200 (def 89) to back/forward flip with mouse\r\n  (less than 89 to keep me from looking up/down)\r\n" +
            //"mat_viewportscale 0.001563; mat_viewportupscale 0\r\n" +

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
            "pointless!showPosition\ttoggle cl_showpos;wait 20000;toggle cl_showpos\ttoggles game position info for a few minutes\t\r\n" +
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

            vm.Twitch.SendReplyWithWrapping(messageID, string.Format("Ok, {0}, we will switch to the class '{1}'", userDisplayName, joinas));
            string cmd = "join_class " + joinas;
            vm.SendCommandAndNoResponse(cmd);
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
            vm.SendCommandAndNoResponse(string.Format("cl_crosshair_red {0};cl_crosshair_green {1};cl_crosshair_blue {2};", r, g, b));
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

    }
}