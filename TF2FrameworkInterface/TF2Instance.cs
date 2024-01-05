using CoreRCON;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TF2FrameworkInterface
{
    /// <summary>
    /// The running TF2 instance through which commands can be sent.
    /// (old) Option 1: launches hl2.exe witih -hijack to send a command to running instance.
    /// (future) Option 2: Launches TF2 with settings to allow an rcon connection, then establishes that connection through which it can send commands.
    /// tf2_bot_detector: "Invoking commands in the game is done via passing rcon commands to your client. "
    /// </summary>
    public class TF2Instance
	{
		public static TF2Instance CreateCommunications()
		{
			return new TF2Instance();
		}

		public static ushort rconPort = 8383;
		public static string rconPassword = "test";
		private static System.Net.IPAddress host = System.Net.IPAddress.Loopback;//"127.0.0.1"

		public static string path = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2";

		private static string hl2 => Path.Combine(path, @"hl2.exe");

		public static void LaunchTF2()
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
				" -usercon" +
				" -high" + // TODO: make this an option
				" +developer 1 +alias developer" +
				" +contimes 0 +alias contimes" +   // the text in the top left when developer >= 1
				" +ip 0.0.0.0 +alias ip" +
				" +sv_rcon_whitelist_address " + host.ToString() + " +alias sv_rcon_whitelist_address" +
				" +sv_quota_stringcmdspersecond 1000000 +alias sv_quota_stringcmdspersecond" + // workaround for mastercomfig causing crashes on local servers
				" +rcon_password " + rconPassword + " +alias rcon_password" +
				" +hostport " + rconPort + " +alias hostport" +
				" +alias cl_reload_localization_files" + // This command reloads files in backwards order, so any customizations get overwritten by stuff from the base game
				" +net_start" +
				" +con_timestamp 1 +alias con_timestamp" +
				" -condebug" +
				" -conclearlog";

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

		private TF2Instance()
        {
            SetUpRCON();

            Task rconTask = TF2RCON.ConnectAsync();
			rconTask.Wait();
        }

        private void SetUpRCON()
        {
            //System.Net.IPAddress host = System.Net.IPAddress.Parse("127.0.0.1");
            System.Net.IPEndPoint endpoint = new System.Net.IPEndPoint(host, rconPort);

            //TF2RCON = new RCON(host, port, password);
            TF2RCON = new RCON(endpoint, rconPassword);
        }

        public RCON TF2RCON { get; private set; }

		public void SendCommand(TF2Command command, Action<string> result)
		{
			string consoleCommand = command.ConsoleString;
			//SendHijackCommand(consoleCommand);
			SendRCONCommand(consoleCommand, result);
		}

		private void SendRCONCommand(string consoleCommand, Action<string> result)
		{
			Task<string> rconTask = TF2RCON.SendCommandAsync(consoleCommand);
			//TODO
			_ = rconTask.ContinueWith(
				s => result(ProcessResult(s.Result))
				);
		}

		/// <summary>
		/// normally true to convert to 'value' from results like '"cl_variable_name" = "value" (def: "")'
		/// </summary>
		public bool ShouldProcessResultValues { get; set; } = true;

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
