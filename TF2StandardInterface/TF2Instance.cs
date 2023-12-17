using System;
using System.Diagnostics;
using System.IO;

namespace TF2StandardInterface
{
	/// <summary>
	/// The running TF2 instance through which commands can be sent.
	/// (old) Option 1: launches hl2.exe witih -hijack to send a command to running instance.
	/// (future) Option 2: Launches TF2 with settings to allow an rcon connection, then establishes that connection through which it can send commands.
	/// tf2_bot_detector: "Invoking commands in the game is done via passing rcon commands to your client. "
	/// </summary>
	public class TF2Instance
	{
		public static TF2Instance LaunchTF2Instance()
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
			return new TF2Instance();
		}

		public void SendCommand(TF2Command command)
		{
			string consoleCommand = command.ConsoleString;
			SendHijackCommand(consoleCommand);
		}

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

		private void SendHijackCommand(string consoleCommand)
		{
			string path = @"C:\";
			string hl2 = Path.Combine(path, @"hl2.exe");

			string hl2Argument = string.Format("-game tf -hijack {0}", consoleCommand);

			using (Process hl2Hijack = new Process())
			{
                hl2Hijack.StartInfo = new ProcessStartInfo()
				{
					FileName = hl2,
					Arguments = hl2Argument,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
				};

				hl2Hijack.Start();
				//string output = pProcess.StandardOutput.ReadToEnd(); //The output result
				hl2Hijack.WaitForExit();
				_ = hl2Hijack.ExitCode;
			}

			throw new NotImplementedException();
		}
	}
}
