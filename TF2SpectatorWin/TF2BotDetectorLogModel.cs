using AspenWin;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;

namespace TF2SpectatorWin
{
    /// <summary>
    /// watch and parse the tf2_bot_detector log to set viewmodel rcon values.
    /// </summary>
    internal class TF2BotDetectorLogModel
    {
        private TF2WindowsViewModel vm;

        private ASPEN.AspenLogging Log => ASPEN.Aspen.Log;

        public TF2BotDetectorLogModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.vm = tF2WindowsViewModel;
        }

        private static readonly string BotDetectorLogPattern = "*.log";
        private FileSystemWatcher watcher;
        /// <summary>
        /// watch tf2_bot_detector log folder for a new file, scan it for rcon port/password, and set those values.
        /// </summary>
        public void WatchLogFolder()
        {
            try
            {
                DisposeOldWatcher();
            }
            catch
            {
                // don't care, just make a new one.
            }

            if (string.IsNullOrEmpty(vm.BotDetectorLog))
                return;

            // First, process the most recent file, then watch for new ones.
            ParseMostRecentLogFile();

            StartNewWatcherOrLogWhy();
        }

        public void ParseMostRecentLogFile()
        {
            try
            {
                ParseLogRconValues(GetMostRecentLogFile());
            }
            catch (Exception ex)
            {
                Log.Error("Could not parse bot detector log yet: " + ex.Message);
                // processing most recent failed (maybe there wasn't one)... no problem, Watcher will process the next one that pops up.
            }
        }

        private ICommand _ParseCommand;
        /// <summary>
        /// Command to re-parse the log to load new rcon values.
        /// </summary>
        public ICommand ParseCommand => _ParseCommand
            ?? (_ParseCommand = new RelayCommand<object>(
                execute: (o) => ParseMostRecentLogFile(),
                canExecute: (o) => vm.IsUsingBotDetector));

        // [20:14:56] Processes.cpp(286):Launch: ShellExecute("S:\\Games\\SteamLibrary\\steamapps\\common\\Team Fortress 2\\tf\\..\\hl2.exe", -novid -nojoy -nosteamcontroller -nohltv -particles 1 -precachefontchars -noquicktime dummy -game tf -steam -secure -usercon -high +developer 1 +alias developer +contimes 0 +alias contimes +ip 0.0.0.0 +alias ip +sv_rcon_whitelist_address 127.0.0.1 +alias sv_rcon_whitelist_address +sv_quota_stringcmdspersecond 1000000 +alias sv_quota_stringcmdspersecond +rcon_password xBTQ69yZ61Rb719F +alias rcon_password +hostport 40537 +alias hostport +alias cl_reload_localization_files +net_start +con_timestamp 1 +alias con_timestamp -condebug -conclearlog) (elevated = false)
        // specifically "+rcon_password xBTQ69yZ61Rb719F +alias rcon_password +hostport 40537 +alias hostport"
        private static readonly Regex TBDRconMatcher = new Regex(@"\+rcon_password\s+(\S+)\s+.*\+hostport\s+(\d+)\s+");

        /// <summary>
        /// Try once to read the given log file (possibly still open for writing) for the Rcon configuration information.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <exception cref="InvalidOperationException">config line was not found in the file (so far)</exception>
        private void ParseLogRconValues(string fullPath)
        {
            // only do this one at a time.  FUTURE: add a custom lock field if we have other things to lock.
            lock (this)
            {
                // access the log file while it is being written.
                using (FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream))
                {
                    ParseLogRconValues(reader);
                }
            }
        }

        /// <summary>
        /// Try once to read the given stream reader for the Rcon configuration information.
        /// </summary>
        /// <param name="reader"></param>
        /// <exception cref="InvalidOperationException">config line was not found in the reader</exception>
        private void ParseLogRconValues(StreamReader reader)
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

                vm.RconPassword = pass;
                vm.RconPort = ushort.Parse(port);

                // yes, we found one, but it's the LAST one that matters, unfortunately, so keep going
                set = true;
            }
            if (!set)
                throw new InvalidOperationException("config not found");

            Log.Info("Loading bot detector Rcon settings: " + vm.RconPassword + " " + vm.RconPort);
        }

        private string GetMostRecentLogFile()
        {
            return Directory.EnumerateFiles(vm.BotDetectorLog, BotDetectorLogPattern)
                .OrderByDescending(s => File.GetCreationTime(
                    Path.Combine(vm.BotDetectorLog, s)))
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
            watcher = new FileSystemWatcher(vm.BotDetectorLog)
            {
                Filter = BotDetectorLogPattern,

                NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.CreationTime,
            };
            watcher.Created += ParseCreatedLogAndKeepTrying;
            watcher.Error += NotifyErrorAndRestartWatcher;

            watcher.EnableRaisingEvents = true;
        }

        private void ParseCreatedLogAndKeepTrying(object sender, FileSystemEventArgs e)
        {
            try
            {
                ParseLogRconValues(e.FullPath);
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
                        ParseCreatedLogAndKeepTrying(sender, e);
                    }));
            }
        }

        private void NotifyErrorAndRestartWatcher(object sender, ErrorEventArgs e)
        {
            // reset the watcher.
            //TODO prevent a constant error loop.
            Log.Error("Error watching TBD Logs: " + e.GetException()?.Message);
            WatchLogFolder();
        }
    }
}