using CoreRCON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TF2FrameworkInterface
{
    /// <summary>
    /// The running TF2 instance through which commands can be sent.
    /// (old) Option 1: launches hl2.exe witih -hijack to send a command to running instance.
    /// (recent) Option 2: Launches TF2 with settings to allow an rcon connection, then establishes that connection through which it can send commands.
	/// (new) Option 3: Launches TF2 (optional) with partial settings for an rcon connection, the rest written to <see cref="RconConfigFileBaseName"/>
	/// cfg file that must be exec'd to finisih setup.
    /// </summary>
    public class TF2Instance
	{
        // tf2_bot_detector: "Invoking commands in the game is done via passing rcon commands to your client. "

        public static TF2Instance CreateCommunications(ushort rconPort, string rconPassword)
		{
			return new TF2Instance(rconPort, rconPassword);
		}

		private static System.Net.IPAddress host = System.Net.IPAddress.Loopback;//"127.0.0.1"

        /// <summary>
        /// Prepares and launches TF2 with Rcon settings.  
		/// autoexec.cfg or user must still "exec TF2SpectatorRCON" <see cref="RconConfigFileBaseName"/>
        /// </summary>
        /// <param name="path">TF2 path e.g. "C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2"</param>
        /// <param name="rconPort">e.g. 42000 - the value TF2 Spectator will connect with</param>
        /// <param name="rconPassword">e.g. MyPassword123 - the value TF2 Spectator will connect with</param>
        public static void LaunchTF2(string path, ushort rconPort, string rconPassword)
        {
            // https://github.com/PazerOP/tf2_bot_detector/blob/master/tf2_bot_detector/SetupFlow/TF2CommandLinePage.cpp#L206
            /*
				" dummy" // Dummy option in case user has mismatched command line args in their steam config
				" -game tf"
				" -steam -secure"  // One or both of these is needed when launching the game directly
				" -usercon"
				" -high" // TODO: make this an option
				" +developer 1 +alias developer"
				" +contimes 0 +alias contimes"   // the text in the top left when developer >= 1
				" +ip 0.0.0.0 +alias ip"
				" +sv_rcon_whitelist_address 127.0.0.1 +alias sv_rcon_whitelist_address"
				" +sv_quota_stringcmdspersecond 1000000 +alias sv_quota_stringcmdspersecond" // workaround for mastercomfig causing crashes on local servers
				" +rcon_password " << rconPassword << " +alias rcon_password"
				" +hostport " << rconPort << " +alias hostport"
				" +alias cl_reload_localization_files" // This command reloads files in backwards order, so any customizations get overwritten by stuff from the base game
				" +net_start"
				" +con_timestamp 1 +alias con_timestamp"
				" -condebug"
				" -conclearlog"
				;
			 */
            string hl2Argument =
				" dummy" + // Dummy option in case user has mismatched command line args in their steam config
				" -game tf" +
				" -steam -secure" +  // One or both of these is needed when launching the game directly
				" -usercon" + // critical for rcon
				" -high" + // TODO: make this an option
				// all "+" commands handled below.
				" -condebug" +
				" -conclearlog";


			// exe changed from "hl2.exe" on April 18, 2024
            string hl2 = Path.Combine(path, @"tf_win64.exe");// 32 bit: @"tf.exe"
            // ... and it stopped accepting "+" console commands/variable settings from the command line.
            bool commandLineConsoleCommandsSupported = false;
			if (commandLineConsoleCommandsSupported)
			{
				// command line commands are in the form "+variable value"
				foreach (string command in GetRconSetupCommands(rconPassword, rconPort))
					hl2Argument += " +" + command;
			}
			else
            {
                WriteRconConfigFile(path, rconPort, rconPassword);
            }

            //TODO: running this should just add args to user's current tf2 config and don't need hl2 location: steam://rungameid/440/+my +extra +args 

            //// double trailing slash is required to separate from added args.
            ////hl2 = "steam://rungameid/440//" + hl2Argument;
            //// looks like rungameid doesn't accept args (any more?) per https://developer.valvesoftware.com/wiki/Steam_browser_protocol
            ////hl2 = "steam://run/440//" + Uri.EscapeDataString(hl2Argument) +"/";
            //// but that still didn't work at all?
            //// out of desparation - try launch
            //hl2 = "steam://launch/440//" + Uri.EscapeDataString(hl2Argument) + "/";
            // ... everything claims one of those should work yet none are working for me, even from a command line.
            //     My theory is this is a bug in the "steam family sharing beta" that I am using.
            //_ = System.Diagnostics.Process.Start(hl2);

            //TODO "using" may kill process.
            using (Process tf2Launch = new Process())
			{
				tf2Launch.StartInfo = new ProcessStartInfo()
				{
					FileName = hl2,
					Arguments = hl2Argument,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
				};

				tf2Launch.Start();
				//string output = pProcess.StandardOutput.ReadToEnd(); //The output result
				//tf2Launch.WaitForExit();
				//_ = tf2Launch.ExitCode;
			}
		}

        private static List<string> GetRconSetupCommands(string rconPassword, ushort rconPort)
		{
			// currently disabling the features tf2_bot_detector uses... no reason to think I need any of them,
			// and I DO want keep password/port settings enabled in case they're changed mid-session.
			return new List<string>()
			{
				// all the commands that tf2_bot_detector sets up (and disables many).  Only 4 are needed to make rcon work locally.

				//"developer 1",
				//	"alias developer",
				//"contimes 0",
				//	"alias contimes",   // the text in the top left when developer >= 1
				
				"ip 0.0.0.0",  // critical for rcon
					//"alias ip",

				"sv_rcon_whitelist_address " + host.ToString(),
					//"alias sv_rcon_whitelist_address",

				//"sv_quota_stringcmdspersecond 1000000",
				//	"alias sv_quota_stringcmdspersecond", // workaround for mastercomfig causing crashes on local servers

				"rcon_password " + rconPassword,  // critical for rcon
					//"alias rcon_password",
				"hostport " + rconPort,  // critical for rcon
					//"alias hostport",

				//"alias cl_reload_localization_files", // This command reloads files in backwards order, so any customizations get overwritten by stuff from the base game
				
				"net_start", // critical for rcon
            };
		}

        /// <summary>
		/// After <see cref="WriteRconConfigFile(string, ushort, string)"/> the user must run in the console: "exec " + RconConfigFileBaseName
        /// "exec - Execute config file from the tf/cfg folder." and exec autoconfig loads the autoconfig.cfg, so no suffix.
        /// </summary>
        public static readonly string RconConfigFileBaseName = "TF2SpectatorRCON";

        public static void WriteRconConfigFile(string tfPath, ushort rconPort, string rconPassword)
        {
			if (string.IsNullOrWhiteSpace(tfPath))
				return;

            string configSuffix = ".cfg";
            string configCommandsFilepath = Path.Combine(tfPath, "tf", "cfg", RconConfigFileBaseName + configSuffix);
            using (StreamWriter writer = new StreamWriter(configCommandsFilepath, false))
            {
                writer.WriteLine("// auto-generated commands to configure Rcon for the most recent settings of TF2 Spectator");
                foreach (string command in GetRconSetupCommands(rconPassword, rconPort))
                    writer.WriteLine(command);
            }
        }

		private ushort rconPort;
		private string rconPassword;

		private TF2Instance(ushort rconPort, string rconPassword)
        {
			this.rconPort = rconPort;
			this.rconPassword = rconPassword;

            SetUpRCON();

            Task rconTask = TF2RCON.ConnectAsync();
			rconTask.Wait();
        }

        private void SetUpRCON()
        {
            System.Net.IPEndPoint endpoint = new System.Net.IPEndPoint(host, rconPort);

            TF2RCON = new RCON(endpoint, rconPassword);
        }

        public RCON TF2RCON { get; private set; }

		/// <summary>
		/// add to the RCON disconnect event
		/// </summary>
		/// <param name="a"></param>
		public void SetOnDisconnected(Action a)
			=> TF2RCON.OnDisconnected += a;

		/// <summary>
		/// runs the command and an action to process its result.  
		/// Returns a task governing the execution of the result processing.
		/// Just call .Wait() if you want to be synchronous with the result execution.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public Task SendCommand(TF2Command command, Action<string> result)
		{
			string consoleCommand = command.ConsoleString;
			//SendHijackCommand(consoleCommand);
			return SendRCONCommand(consoleCommand, result);
		}

		private Task SendRCONCommand(string consoleCommand, Action<string> result)
		{
			lock (this)
			{
				Task<string> rconTask = TF2RCON.SendCommandAsync(consoleCommand);

				return rconTask.ContinueWith(
					s => result(ProcessResult(s.Result))
					);
			}
		}

		/// <summary>
		/// normally true to convert to 'value' from results like '"cl_variable_name" = "value" (def: "")'
		/// </summary>
		public bool ShouldProcessResultValues { get; set; } = true;

        // handle output that could be like this:
        // "cl_crosshair_file" = "crosshair1" ( def. "" )
        // client archive
        // - help text
        private static readonly Regex variableMatch = new Regex(
			".*\"(?<variable>[^\"]+)\"\\s*=\\s*\"(?<value>[^\"]+)\".*"
			);
        /// <summary>
        /// convert some odd results into something more useful
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private string ProcessResult(string result)
        {
			if (result == null) 
				return result;
            if (!ShouldProcessResultValues)
                return result;

            Match matcher = variableMatch.Match(result);
            if (!matcher.Success)
                return result;

            string variable = matcher.Groups["variable"].Value;
            string value = matcher.Groups["value"].Value;

            return value;
        }

        #region HiJack

        /* tf2bd:
bool HijackActionManager::SendHijackCommand(std::string cmd)
{
	if (cmd.empty())
		return true;

	if (!FindWindowA("Valve001", nullptr))
	{
		DebugLogWarning("Attempted to send command \""s << cmd << "\" to game, but game is not running");
		return false;
	}

	const std::filesystem::path hl2Dir = m_Settings->GetTFDir() / "..";
	const std::filesystem::path hl2ExePath = hl2Dir / "hl2.exe";

	std::wstring cmdLine = mh::format(L"{} -game tf -hijack {}", hl2ExePath, mh::change_encoding<wchar_t>(cmd));

	STARTUPINFOW si{};
	si.cb = sizeof(si);

	PROCESS_INFORMATION pi{};

	auto result = CreateProcessW(
		nullptr,             // application/module name
		cmdLine.data(),      // command line
		nullptr,             // process attributes
		nullptr,             // thread attributes
		FALSE,               // inherit handles
		IDLE_PRIORITY_CLASS, // creation flags
		nullptr,             // environment
		hl2Dir.c_str(),      // working directory
		&si,                 // STARTUPINFO
		&pi                  // PROCESS_INFORMATION
	);

	if (!result)
	{
		const auto error = GetLastError();
		LogError("Failed to send command to hl2.exe: CreateProcess returned "s
			<< result << ", GetLastError returned " << error << ": " << std::error_code(error, std::system_category()).message());

		return false;
	}

	if (m_Settings->m_Unsaved.m_DebugShowCommands)
		DebugLog("Game command: {}", std::quoted(cmd));

	m_RunningCommands.emplace_back(pi.hProcess, std::move(cmd));

	if (!CloseHandle(pi.hThread))
		LogError(MH_SOURCE_LOCATION_CURRENT(), "Failed to close process thread");

	return true;
}
		 */

        //private void SendHijackCommand(string consoleCommand)
        //{
        //	string hl2Argument = string.Format("-game tf -hijack {0}", consoleCommand);

        //	using (Process hl2Hijack = new Process())
        //	{
        //		hl2Hijack.StartInfo = new ProcessStartInfo()
        //		{
        //			FileName = hl2,
        //			Arguments = hl2Argument,
        //			UseShellExecute = false,
        //			RedirectStandardOutput = true,
        //			WindowStyle = ProcessWindowStyle.Hidden,
        //			CreateNoWindow = true,
        //		};

        //		hl2Hijack.Start();
        //		//string output = pProcess.StandardOutput.ReadToEnd(); //The output result
        //		hl2Hijack.WaitForExit();
        //		_ = hl2Hijack.ExitCode;
        //	}
        //}
        #endregion HiJack
    }
}
