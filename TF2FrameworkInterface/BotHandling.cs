using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace TF2FrameworkInterface
{
    public class Bot
    {
        /// <summary>
        /// The status id needed for voting... a number like 1-9999
        /// </summary>
        public string GameID { get; internal set; }
        /// <summary>
        /// The unique id (steam id) in the format [U:1:123123123]
        /// </summary>
        public string SteamUniqueID { get; internal set; }
        public string Name { get; internal set; }
        public string Team { get; internal set; }
        public TF2Status Status { get; internal set; }
        public bool IsBanned { get; internal set; }
    }

    public class BotHandlingConfig
    {
        public string TF2Path;
        public string PlayerlistPath;
        public string UserID;
    }
    public class BotHandling
    {
        public BotHandling(TF2Instance TF2, BotHandlingConfig config)
        {
            this.TF2 = TF2;

            MySteamUniqueID = config.UserID;
            
            Muted = new TF2VoiceBanFile(config.TF2Path);
            Muted.Load();

            // TODO retain ALL of the user's file info, not just the stuff I care about.
            // first file is the user's file
            Banned = new TF2BDFiles(config.PlayerlistPath);
            //"description": "Official player blacklist for TF2 Bot Detector.",
            //"title": "Official player blacklist",
            Banned.AddURLFile("https://raw.githubusercontent.com/PazerOP/tf2_bot_detector/master/staging/cfg/playerlist.official.json");
            // roto trusted list
            Banned.AddURLFile("https://trusted.roto.lol/v1/steamids");

            Lobby = new TFDebugLobbyCommandOutput(TF2);
            Lobby.LobbyUpdated += (l) => {
                
                    //RefreshPlayers();
               
                GameLobbyUpdated?.DynamicInvoke(this); 
            };

            ServerDetail = new StatusCommandLogOutput(TF2, config.TF2Path);
            //ServerDetail.StatusUpdated += (s) => GameLobbyUpdated?.DynamicInvoke(this);
            
            RefreshBotList();
        }

        public delegate void GameLobbyEvent(BotHandling source);
        public event GameLobbyEvent GameLobbyUpdated;

        /// <summary>
        /// loosely, combines tf2_debug_lobby command with the IsBot test. Excludes self.
        /// </summary>
        private void RefreshBotList()
        {
            // merge lists so we keep existing populated bot instances.

            List<TF2DebugLobby> currentConnections = new List<TF2DebugLobby>(Lobby.GetNewStatus());

            List<Bot> stillConnectedBots = new List<Bot>();
            if (Bots != null)
                stillConnectedBots = new List<Bot>(Bots);
            _ = stillConnectedBots.RemoveAll(b => !currentConnections.Any(l => l.SteamUniqueID == b.SteamUniqueID));
            // user may have unmarked a bot.
            _ = stillConnectedBots.RemoveAll(b => !IsBot(b));

            List<TF2DebugLobby> newConnections = new List<TF2DebugLobby>(currentConnections);
            newConnections.RemoveAll(l => stillConnectedBots.Any(b => b.SteamUniqueID == l.SteamUniqueID));

            IEnumerable<Bot> newlyConnectedBots = newConnections
                .Where(IsBot)
                .Where(l => l.SteamUniqueID != MySteamUniqueID)
                .Select(CreateBot);

            Bots = newlyConnectedBots.Union(stillConnectedBots);
        }

        private Bot CreateBot(TF2DebugLobby l)
        {
            return new Bot
            {
                Name = "?",
                SteamUniqueID = l.SteamUniqueID,
                Team = l.Team,
                //= l.PlayerType,
                //= l.MemberNumber // not the same as Status GameID used for voting.
                IsBanned = Banned.GetCheaterIDs().Contains(l.SteamUniqueID),
            };
        }

        private bool IsBot(TF2DebugLobby player) => IsBot(player.SteamUniqueID);

        private bool IsBot(string steamUniqueID)
        {
            bool isMuted = Muted.UIDs.Contains(steamUniqueID);
            if (isMuted) 
                return true;

            bool isCheater = IsBannedID(steamUniqueID);
            return isCheater;
        }

        private bool IsBot(Bot player) => IsBot(player.SteamUniqueID);

        public bool IsBannedID(string steamID)
        {
            return Banned.GetCheaterIDs().Contains(steamID);
        }

        public bool IsMutedID(string steamID)
        {
            return Muted.UIDs.Contains(steamID);
        }

        public bool IsUserBannedID(string steamID)
        {
            return Banned.GetUserCheaterIDs().Contains(steamID);
        }

        public TF2Instance TF2 { get; }
        private TF2VoiceBanFile Muted { get; }
        private TF2BDFiles Banned { get; }
        public Bot VotingOnThisBot { get; private set; }
        public TFDebugLobbyCommandOutput Lobby { get; }
        public StatusCommandLogOutput ServerDetail { get; private set; }
        public IEnumerable<Bot> Bots { get; private set; }
        public List<string> SkippedList { get; } = new List<string>();

        public string MyTeam => Lobby.TF2DebugLobbyStatus
            .Where(l => MySteamUniqueID == l.SteamUniqueID)
            .FirstOrDefault()?.Team;

        public string MySteamUniqueID { get; }

        public ObservableCollection<LobbyPlayer> _Players;
        public ObservableCollection<LobbyPlayer> Players => _Players
            ?? (_Players = new ObservableCollection<LobbyPlayer>(
                Lobby.TF2DebugLobbyStatus.Select(
                    l => new LobbyPlayer(l, ServerDetail))
                ));

        public void RefreshPlayers()
        {
            // merge lists so we keep existing populated bot instances.

            List<TF2DebugLobby> currentConnections = new List<TF2DebugLobby>(Lobby.TF2DebugLobbyStatus);

            List<TF2DebugLobby> newConnections = new List<TF2DebugLobby>(currentConnections);
            if (_Players != null)
            {
                //_ = _Players.RemoveAll(b => !currentConnections.Any(l => l.SteamUniqueID == b.SteamID));
                IEnumerable<LobbyPlayer> gone = _Players.Where(
                    b => !currentConnections.Any(l => l.SteamUniqueID == b.SteamID))
                    .ToList();
                foreach (LobbyPlayer p in gone)
                    RemovePlayer(p);
                NotRemovedPlayers(_Players.Except(gone));

                List<LobbyPlayer> stillConnectedBots = new List<LobbyPlayer>(_Players);
                _ = newConnections.RemoveAll(l => stillConnectedBots.Any(b => b.SteamID == l.SteamUniqueID));

                // and update teams
                foreach (LobbyPlayer p in stillConnectedBots)
                {
                    TF2DebugLobby lobbyInfo = currentConnections.FirstOrDefault(l => l.SteamUniqueID == p.SteamID);
                    if (lobbyInfo != null)
                        p.Update(lobbyInfo);
                    p.IsBanned = IsBannedID(p.SteamID);
                    p.IsUserBanned = IsUserBannedID(p.SteamID);
                    p.IsMe = MySteamUniqueID == p.SteamID;
                    p.IsMuted = IsMutedID(p.SteamID);
                }
            }

            IEnumerable<LobbyPlayer> newThings = newConnections.Select(l => new LobbyPlayer(l, ServerDetail)
            {
                IsBanned = IsBannedID(l.SteamUniqueID),
                IsUserBanned = IsUserBannedID(l.SteamUniqueID),
                IsMe = MySteamUniqueID == l.SteamUniqueID,
                IsMuted = IsMutedID(l.SteamUniqueID),
            });
            if (_Players == null)
                _Players = new ObservableCollection<LobbyPlayer>(newThings);
            else
                //_Players.AddRange(newThings);
                foreach (LobbyPlayer p in newThings)
                    _Players.Add(p);// TODO from this thread modification not allowed

        }

        private void RemovePlayer(LobbyPlayer p)
        {
            p.RemoveCounter++;
            // 2 or 3 is probably enough to keep the list from jumping around,
            // but I want to be able to kick people after disconnecting or after they got kicked when I get a chance to click them.
            if (p.RemoveCounter > 15)
                _ = _Players.Remove(p);
        }

        private void NotRemovedPlayers(IEnumerable<LobbyPlayer> present)
        {
            // if they're still present, reset the remove counter
            foreach (LobbyPlayer p in present)
                p.RemoveCounter = 0;
        }

        //private bool cancelled = false;
        //public void Cancel() { cancelled = true; }

        public void Next()
        {
            //cancelled = false;
            Bot bot = GetNextKickableBot();
            if (bot == null)
                return;
            //TODO note that currently every "IsBot" is also "IsBanned" and skips straight to kicking.
            new KickInteraction(bot, this)
                .Begin();
        }

        private Bot GetNextKickableBot()
        {
            RefreshBotList();
            AddBotDetail();
            string myTeam = MyTeam;
            return Bots.Where(b
                => b.Team == myTeam
                && !SkippedList.Contains(b.SteamUniqueID)
                && !string.IsNullOrEmpty(b.GameID)
                //&& VotingOnThisBot?.GameID != b.GameID // TODO can I remove this now thanks to SkippedList?
                )
                .FirstOrDefault();
        }

        private void AddBotDetail()
        {
            IEnumerable<TF2Status> statuses = ServerDetail.GetNewStatus();
            foreach (TF2Status status in statuses)
            {
                Bot bot = Bots.FirstOrDefault(b => b.SteamUniqueID == status.SteamUniqueID);
                if (bot == null)
                    continue;

                bot.Name = status.UserName;
                bot.GameID = status.GameUserID;
                bot.Status = status;
            }
        }

        internal void Send(string command, Action<string> then)
        {
            TF2.SendCommand(new StringCommand(command), s =>
            {
                then?.Invoke(s);
            });
        }

        private void RecordThisIsNotABot(Bot bot)
        {
            string steamUniqueID = bot.SteamUniqueID;
            if (!SkippedList.Contains(steamUniqueID))
                SkippedList.Add(steamUniqueID);
        }

        internal void ChoseToSkip(Bot bot)
        {
            RecordThisIsNotABot(bot);
            //if(!cancelled)
            Next();
        }

        internal void ChoseToKickBot(Bot votingOnThisBot)
        {
            RecordAsABot(votingOnThisBot);
        }

        private void RecordAsABot(Bot votingOnThisBot)
        {
            // note the instance is banned now to update the Bot list for auto-kicking.
            votingOnThisBot.IsBanned = true;

            TF2BDPlayer bot = new TF2BDPlayer
            {
                attributes = new List<string> {
                    TF2BDPlayer.CHEATER
                },
                steamid = votingOnThisBot.SteamUniqueID,
                last_seen = new TF2BDLastSeen
                {
                    player_name = votingOnThisBot.Name,
                    time = new DateTime().Ticks
                }
            };

            RecordAsABot(bot);
        }

        private void RecordAsABot(TF2BDPlayer bot)
        {
            Banned.AddUserEntry(bot);
            SaveBanList();
        }

        private void SaveBanList()
        {
            //TODO during shutdown/config save instead?
            Banned.SaveUserFile();
        }

        public void RecordAsABot(LobbyPlayer player)
        {
            TF2BDPlayer bot = new TF2BDPlayer
            {
                attributes = new List<string> {
                    TF2BDPlayer.CHEATER
                },
                steamid = player.SteamID,
                last_seen = new TF2BDLastSeen
                {
                    player_name = player.StatusName,
                    time = new DateTime().Ticks
                }
            };
            RecordAsABot(bot);
        }

        public void UnRecordAsABot(LobbyPlayer player)
        {
            Banned.RemoveUserEntry(player.SteamID);
            // they're still banned if they're on the non-user list.
            player.IsUserBanned = IsUserBannedID(player.SteamID);
            player.IsBanned = IsBannedID(player.SteamID);
            SaveBanList();
        }
        public void RecordAsAFriend(LobbyPlayer player)
        {
            //TODO keep a list of trusted users
        }

        internal bool IsBotPresent(Bot votingOnThisBot)
        {
            RefreshBotList();
            AddBotDetail();

            return Bots.Any(b => b.SteamUniqueID == votingOnThisBot?.SteamUniqueID);
        }

        internal void BotKickOver(Bot votingOnThisBot)
        {
            //if(!cancelled)
            Next();
        }
    }

    internal class KickInteraction
    {
        private Bot VotingOnThisBot;
        private readonly BotHandling handler;

        public KickInteraction(Bot bot, BotHandling handler)
        {
            this.VotingOnThisBot = bot;
            this.handler = handler;
        }

        internal void Begin()
        {
            if (VotingOnThisBot.IsBanned)
            {
                SetupAwaitKick();
                return;
            }
            OfferKick(); // sets up two aliases - one accepts, sets value to say that, and attempts a kick; other sets value to skip; both disable both binds.
            SetupAwaitResponse(
                //(resp)=> VotingOnThisBot!= null && (resp == skipValue || resp == VotingOnThisBot.SteamUniqueID), 
                IsResponseReceived,
                afterresponse: AfterVoted);
        }

        private void OfferKick()
        {
            if (VotingOnThisBot == null)
                return;

            string kickkey = "0";
            string skipkey = "SEMICOLON";
            string kickCommand = FixQuotes(VoteKickCommand(VotingOnThisBot));

            string setupKick =
                $"alias bind_kickkey \"" +
                //$"setinfo {kick_response_variable} \"{VotingOnThisBot.SteamUniqueID}\";" +
                $"setinfo {kick_response_variable} \"{kickValue}\";" +
                //TODO if this just doesn't work due to quotes -leave it out...let the retry script do the kicking (and change it to pause AFTER the first try)
                //kickCommand +
                $";unbind {kickkey};unbind {skipkey}" +
                $"\";" +
                $"bind {kickkey} bind_kickkey;";
            string setupSkip =
                $"alias bind_skipkey \"" +
                $"setinfo {kick_response_variable} {skipValue};unbind {kickkey};unbind {skipkey}" +
                $"\";" +
                $"bind {skipkey} bind_skipkey;";

            // examples of sound files that could be played are at https://www.sounds-resource.com/pc_computer/tf2/
            // "replay/rendercomplete.wav" // front desk bell
            // "replay/replaydialog_warn.wav" // buzzer
            // "ui/mm_rank_up.wav" // harp strum
            // (not tested, maybe not ambient in front) "ambient/lair/jungle_alarm.wav" // single honk
            // didn't work when I tried, might require mode enabled?: "ui/coach/go_here.wav" // coach whistle

            string audioAlert = $"play player/cyoa_pda_beep4.wav;";

            string textAlert = $"say_party |spec| bot/cheat: '{VotingOnThisBot.Name}'    -     {kickkey} | {skipkey}";

            string setupAndOfferKick =
                $"setinfo {kick_response_variable} \"{blankValue}\";" +
                setupKick +
                setupSkip +
                audioAlert +
                textAlert;

            Send(setupAndOfferKick, null);
        }

        private const string kick_response_variable = "tf2spec_kick_response";
        private const string blankValue = "?";

        private string FixQuotes(string v)
        {
            // according to https://stackoverflow.com/questions/38362708/escaping-double-quotes-in-tf2-scripting
            // we can replace start and and doublequotes with ALT+0145 and ALT+0146 

            //HOWEVER, from console, always claimed to have 1 second left before I could callvote with
            // callvote kick ‘685 cheating’ 
            // same for using two ‘ or two ’ or two '
            // only two " worked.

            // so disabling this.

            //Regex quotegroup = new Regex(Regex.Escape("\""));
            //Match result = quotegroup.Match(v);
            //const string ALT0145 = "‘";
            //const string ALT0146 = "’";
            //bool first = true;
            //MatchEvaluator quoteunquote = (m) =>
            //(first = !first)
            //? ALT0146 // !first (second)
            //: ALT0145;

            //return quotegroup.Replace(v, quoteunquote);
            return v;
        }

        /// <summary>
        /// callvote kick "{GameID} [reason]" (format per tf2bd)
        /// callvote kick "4648 cheating"
        /// reasons:  "other", "cheating", "idle", "scamming"
        /// </summary>
        /// <param name="bot"></param>
        /// <returns></returns>
        private string VoteKickCommand(Bot bot)
        {
            string reason = "cheating";
            return
                //"play replay/replaydialog_warn.wav;" +// buzzer
                "play ui/mm_rank_up.wav;" + // harp strum
                //"say_party " +//TODO testing
                $"callvote kick \"{bot.GameID} {reason}\"";
        }


        private void Send(string command, Action<string> then)
        {
            handler.Send(command, then);
        }

        private void SetupAwaitResponse(Func<string, bool> validResponse, Action<string> afterresponse)
        {
            Polling(kick_response_variable, (string resp) =>
            {
                //if (cancelled) return true;
                if (!validResponse(resp))
                    return false;

                ClearVariable(kick_response_variable, blankValue);
                afterresponse(resp);
                return true;
            });
        }

        private void Polling(string variableName, Func<string, bool> valueSetTest)
        {
            Send(variableName, (response) =>
            {
                if (!valueSetTest(response))
                    SleepAndPoll(variableName, valueSetTest);
            });
        }

        private void SleepAndPoll(string variableName, Func<string, bool> valueSetTest)
        {
            Thread.Sleep(500);
            Polling(variableName, valueSetTest);
        }

        private void ClearVariable(string variableName, string clearedValue)
        {
            Send(variableName + $" \"{clearedValue}\"", null);
        }

        private bool IsResponseReceived(string resp)
        {
            return VotingOnThisBot != null && (resp == skipValue || resp == kickValue);
        }

        private const string skipValue = "N";
        private const string kickValue = "Y"; // was using UID to be extra sure... but that is hard to get into a variable.

        private void AfterVoted(string voted)
        {
            //if (voted == VotingOnThisBot?.SteamUniqueID)
            if (voted == kickValue)
                ChoseToKick();
            else if (voted == skipValue)
                ChoseToSkip();
            else
                VotingOnThisBot = null;
        }

        private void ChoseToKick()
        {
            //TODO event handler instead
            handler.ChoseToKickBot(VotingOnThisBot);
            // we await the bot vanishing, and keep trying to kick until then.
            SetupAwaitKick();
        }

        private void ChoseToSkip()
        {
            Bot bot = VotingOnThisBot;
            VotingOnThisBot = null;
            //TODO event handler instead
            handler.ChoseToSkip(bot);
        }

        private void SetupAwaitKick()
        {
            //Thread.Sleep(15000);
            while (VotingOnThisBot != null
                && handler.IsBotPresent(VotingOnThisBot))
            {
                Thread.Sleep(1000);
                Send(VoteKickCommand(VotingOnThisBot), null);

                Thread.Sleep(5000);
            }
            Bot bot = VotingOnThisBot;
            VotingOnThisBot = null;
            //if(!cancelled)
            //TODO event handler instead
            handler.BotKickOver(bot);
        }
    }
}
