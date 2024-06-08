using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

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
            Lobby.LobbyServerChanged += ResetBotServerKickInfo;

            ServerDetail = new StatusCommandLogOutput(TF2, config.TF2Path);
            //ServerDetail.StatusUpdated += (s) => GameLobbyUpdated?.DynamicInvoke(this);
            
            RefreshBotList();
        }

        private void ResetBotServerKickInfo(TFDebugLobbyCommandOutput source)
        {
            //CONCERN: does votekick number change on map change?
            //regardless same bot UID may match to new map as us but kick game id number definitely changes then.
            //reset kick numbers if server changes
            //TODO could we add a sanity check of duplicate kick IDs before using?

            // was causing stack overflow?
            //if (Bots != null)
            //    foreach (Bot p in Bots)
            //    {
            //        // leave the name for aesthetics
            //        p.GameID = null;
            //        p.Status = null;
            //    }
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
            // possible bot - cheap test.
            bool isSimilarNametoSuggestion = IsNameForIDSimilarToASuggestedName(steamUniqueID);
            if (isSimilarNametoSuggestion)
                return true;
            bool isMuted = Muted.UIDs.Contains(steamUniqueID);
            if (isMuted)
                return true;

            // definite bot.
            bool isCheater = IsBannedID(steamUniqueID);
            if (isCheater)
                return true;

            // possible bot - more expensive test.
            bool isSimilarNameToBot = IsNameForIDSimilarToARepeatedBotName(steamUniqueID);
            if (isSimilarNameToBot)
                return true;

            return false;
        }

        private bool IsNameForIDSimilarToASuggestedName(string steamUniqueID)
        {
            string subjectName = Players.FirstOrDefault(p => p.SteamID == steamUniqueID)?.StatusName;
            if (subjectName == null)
                return false;

            return IsSimilarToSuggestedName(subjectName);
        }

        public bool IsSimilarToSuggestedName(string subjectName)
        {
            string subjectBotEx = BotEx(subjectName);

            return SuggestedNames.Any(name => BotEx(name) == subjectBotEx);
        }

        private bool IsNameForIDSimilarToARepeatedBotName(string steamUniqueID)
        {
            string subjectName = Players.FirstOrDefault(p => p.SteamID == steamUniqueID)?.StatusName;
            if (subjectName == null)
                return false;

            return IsSimilarToRepeatedBotName(subjectName);
        }

        private bool IsSimilarToRepeatedBotName(string subjectName)
        {
            const int minimumRepeats = 2;
            if (subjectName == null)
                return false;

            string playerBotExName = BotEx(subjectName);
            if (IsNotComparableBotEx(playerBotExName))
                return false;

            // TODO got a simulatneous modification failure here
            // assume one of the known bot names is the shortest version - allow new potential to have added text (like a hashtag)
            return Banned.GetCheaterNames().Count(name =>
            {
                string botName = BotEx(name);
                if (IsNotComparableBotEx(botName))
                    return false;

                // currently this would not catch twitter/myg0t without changing this to "contains"
                // but that risks more false positives like "I love my GTO car" (mygt in the middle)

                // concerned for false positives: ("calico" and "ziggy" are sub names immediately found in regular longer-named players)
                // maybe worthwhile with the right minimum count.
                //bool result = playerBotExName.StartsWith(botName);
                bool result = playerBotExName.Equals(botName);

                return result;
            }) >= minimumRepeats;
        }

        private bool IsNotComparableBotEx(string botExName)
        {
            // subjective test of whether we have enough letters to be a reasonable test.
            // e.g. myg0t (mygt)
            return botExName == null || botExName.Length <= 4;
        }

        // "not an uppercase latin character"
        private Regex notBotExChar = new Regex(@"\P{Lu}");
        /// <summary>
        /// Inspired by "soundex" this deconstructs the bot's name to a format easily checked against similarly transformed names.
        /// </summary>
        /// <param name="subjectName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private string BotEx(string subjectName)
        {
            string botEx = subjectName.ToUpper();
            botEx = notBotExChar.Replace(botEx, string.Empty);
            return botEx;
        }

        private bool IsBot(Bot player) => IsBot(player.SteamUniqueID);

        private bool IsBannedID(string steamID)
        {
            return Banned.GetCheaterIDs().Contains(steamID);
        }

        private bool IsMutedID(string steamID)
        {
            return Muted.UIDs.Contains(steamID);
        }

        private bool IsUserBannedID(string steamID)
        {
            return Banned.GetUserCheaterIDs().Contains(steamID);
        }

        private bool IsFriendID(string steamID)
        {
            return SkippedList.Contains(steamID);
        }

        private TF2Instance TF2 { get; }
        private TF2VoiceBanFile Muted { get; }
        private TF2BDFiles Banned { get; }
        //public Bot VotingOnThisBot { get; private set; }
        public TFDebugLobbyCommandOutput Lobby { get; }
        private StatusCommandLogOutput ServerDetail { get; set; }
        public IEnumerable<Bot> Bots { get; private set; }
        private List<string> SkippedList { get; } = new List<string>();

        public string MyTeam => Lobby.TF2DebugLobbyStatus
            .Where(l => MySteamUniqueID == l.SteamUniqueID)
            .FirstOrDefault()?.Team;

        public string MySteamUniqueID { private get; set; }

        private ObservableCollection<LobbyPlayer> _Players;
        public ObservableCollection<LobbyPlayer> Players => _Players
            ?? (_Players = new ObservableCollection<LobbyPlayer>(
                Lobby.TF2DebugLobbyStatus.Select(
                    l => new LobbyPlayer(l, ServerDetail))
                ));

        private KickInteraction Kicking { get; set; }
        private KickInteraction Marking { get; set; }

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
                    p.IsFriend = IsFriendID(p.SteamID);
                    p.IsMe = MySteamUniqueID == p.SteamID;
                    p.IsMuted = IsMutedID(p.SteamID);

                    p.IsKicking = Kicking?.Target?.SteamUniqueID == p.SteamID;
                    p.IsMarking = Marking?.Target?.SteamUniqueID == p.SteamID;
                }
            }

            IEnumerable<LobbyPlayer> newThings = newConnections.Select(l => new LobbyPlayer(l, ServerDetail)
            {
                IsBanned = IsBannedID(l.SteamUniqueID),
                IsUserBanned = IsUserBannedID(l.SteamUniqueID),
                IsFriend = IsFriendID(l.SteamUniqueID),
                IsMe = MySteamUniqueID == l.SteamUniqueID,
                IsMuted = IsMutedID(l.SteamUniqueID),

                // shouldn't be targeted yet if it's new
                //IsKicking = Kicking?.Target?.SteamUniqueID == l.SteamUniqueID,
                //IsMarking = Marking?.Target?.SteamUniqueID == l.SteamUniqueID,
            });
            if (_Players == null)
                _Players = new ObservableCollection<LobbyPlayer>(newThings);
            else
                foreach (LobbyPlayer p in newThings)
                    _Players.Add(p);

        }

        private void RemovePlayer(LobbyPlayer p)
        {
            p.RemoveCounter++;

            // 2 or 3 is probably enough to keep the list from jumping around,
            // but I want to be able to kick people after disconnecting or after they got kicked when I get a chance to click them.
            if (p.RemoveCounter > 15
                || (p.RemoveCounter > 3 && p.IsBanned)) // already marked for kicking - no need to keep them around.
                _ = _Players.Remove(p);
        }

        private void NotRemovedPlayers(IEnumerable<LobbyPlayer> present)
        {
            // if they're still present, reset the remove counter
            foreach (LobbyPlayer p in present)
                p.RemoveCounter = 0;
        }

        private List<string> SuggestedNames = new List<string>();
        /// <summary>
        /// add a temporary name suggestion to offer as a bot match.
        /// </summary>
        /// <param name="botName"></param>
        public void SuggestBotName(string botName)
        {
            SuggestedNames.Add(botName);
        }

        //private bool cancelled = false;
        //public void Cancel() { cancelled = true; }

        public void Next()
        {
            NextKick();
            NextMark();
        }

        private void NextKick()
        {
            if (Kicking != null && Kicking.IsBusy)
            {
                Kicking.Next();
                return;
            }

            //    //cancelled = false;
            Bot bot = GetNextKickableBotBannedOnMyTeam();
            if (bot == null)
                return; // no action if banned is on other team.

            Kicking = new KickInteraction(bot, this);
            Kicking.Begin();
        }

        private Bot GetNextKickableBotBannedOnMyTeam()
        {
            string myTeam = MyTeam;
            return GetMarkableBots()
                .Where(b => b.Team == myTeam && b.IsBanned)
                .FirstOrDefault();
        }

        private IEnumerable<Bot> GetMarkableBots()
        {
            RefreshBotList();
            AddBotDetail();
            return Bots.Where(b
                => !SkippedList.Contains(b.SteamUniqueID)
                && !string.IsNullOrEmpty(b.GameID)
                );
        }



        private void NextMark()
        {
            if (Marking != null && Marking.IsBusy)
            {
                Marking.Next();
                return;
            }

            //    //cancelled = false;
            // prefer marking my team first to get the kicks going
            Bot bot = GetNextMarkableBotNotBannedOnMyTeam();
            if (bot != null)
            {
                Marking = new KickInteraction(bot, this); // we offer it as a "kick" but the actual kicking starts later.
                Marking.Begin();
            }
            else
            {
                bot = GetNextMarkableBotNotBannedOtherTeam();
                if (bot == null)
                    return;

                Marking = new MarkInteraction(bot, this);
                Marking.Begin();
            }
        }

        private Bot GetNextMarkableBotNotBannedOnMyTeam()
        {
            string myTeam = MyTeam;
            return GetMarkableBots()
                .Where(b => b.Team == myTeam && !b.IsBanned)
                .FirstOrDefault();
        }

        /// <summary>
        /// a selection from the (potential) bots on the other team that aren't already banned
        /// </summary>
        /// <returns></returns>
        private Bot GetNextMarkableBotNotBannedOtherTeam()
        {
            string myTeam = MyTeam;
            return GetMarkableBots()
                .Where(b => b.Team != myTeam && !b.IsBanned)
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

        internal void ChoseToSkip(Bot bot)
        {
            RecordThisIsNotABot(bot);
            //if(!cancelled)
            // don't need to do this, we're already in a loop that will do it. Next();
        }

        private void RecordThisIsNotABot(Bot bot)
        {
            string steamUniqueID = bot.SteamUniqueID;
            if (!SkippedList.Contains(steamUniqueID))
                SkippedList.Add(steamUniqueID);
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
            player.IsUserBanned = true;
            player.IsBanned = true;

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
            SkippedList.Add(player.SteamID);
            player.IsFriend = true;

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
            
            // don't need to do this, we're already in a loop that will do it. Next();
        }
    }

    internal class MarkInteraction : KickInteraction
    {
        public MarkInteraction(Bot bot, BotHandling handler)
            : base(bot, handler)
        {
        }

        // no change.
        //protected override string GetOfferKickAudioAlert()
        //{
        //    return $"play player/cyoa_pda_beep4.wav;";
        //}

        protected override string GetOfferKickTextAlert(string kickkey, string skipkey)
        {
            return GetOfferKickTextAlert(kickkey, skipkey, "mark");
        }

        protected override void SetupAwaitKick()
        {
            // do nothing - we're not kicking.
        }
    }

    internal class KickInteraction
    {
        public Bot Target => VotingOnThisBot;
        protected Bot VotingOnThisBot;
        protected readonly BotHandling handler;

        public KickInteraction(Bot bot, BotHandling handler)
        {
            this.VotingOnThisBot = bot;
            this.handler = handler;
        }

        internal void Begin()
        {
            if (VotingOnThisBot == null)
                return;

            this.IsBusy = true;
            if (VotingOnThisBot.IsBanned)
            {
                // no need to offer, just act.
                SetupAwaitKick();
                return;
            }

            OfferKick(); // sets up two aliases - one accepts, sets value to say that, and attempts a kick; other sets value to skip; both disable both binds.
            isPolling = true;
            pollTime = DateTime.Now;
        }

        // continue the kick interaction's next step/attempt
        internal void Next()
        {
            if (VotingOnThisBot == null)
                return;

            if (VotingOnThisBot.IsBanned)
            {
                AwaitKick();
                return;
            }

            if (isPolling)
                AwaitResponse(
                    IsResponseReceived,
                    afterresponse: AfterVoted);
            // might have to do this if it is getting stuck... but it should catch up after sending commands.
            //else 
            //    DoneKicking();
        }

        internal bool IsBusy { get; set; } = false;

        private void OfferKick()
        {

            string kickkey = "0";
            string skipkey = "SEMICOLON";

            string setupKick =
                $"alias bind_kickkey \"" +
                    //$"setinfo {kick_response_variable} \"{VotingOnThisBot.SteamUniqueID}\";" +
                    $"setinfo {kick_response_variable} {kickValue};" +
                    $"unbind {kickkey};unbind {skipkey}" +
                $"\";" +
                $"bind {kickkey} bind_kickkey;";
            string setupSkip =
                $"alias bind_skipkey \"" +
                    $"setinfo {kick_response_variable} {skipValue};" +
                    $"unbind {kickkey};unbind {skipkey}" +
                $"\";" +
                $"bind {skipkey} bind_skipkey;";

            // examples of sound files that could be played are at https://www.sounds-resource.com/pc_computer/tf2/
            // "replay/rendercomplete.wav" // front desk bell
            // "replay/replaydialog_warn.wav" // buzzer
            // "ui/mm_rank_up.wav" // harp strum
            // (not tested, maybe not ambient in front) "ambient/lair/jungle_alarm.wav" // single honk
            // didn't work when I tried, might require mode enabled?: "ui/coach/go_here.wav" // coach whistle

            string audioAlert = GetOfferKickAudioAlert();

            string textAlert = GetOfferKickTextAlert(kickkey, skipkey);

            string setupAndOfferKick =
                $"setinfo {kick_response_variable} \"{blankValue}\";" +
                setupKick +
                setupSkip +
                audioAlert +
                textAlert;

            Send(setupAndOfferKick, null);
        }

        protected virtual string GetOfferKickAudioAlert()
        {
            return $"play player/cyoa_pda_beep4.wav;";
        }

        protected virtual string GetOfferKickTextAlert(string kickkey, string skipkey)
        {
            return GetOfferKickTextAlert(kickkey, skipkey, "kick");
        }

        protected string GetOfferKickTextAlert(string kickkey, string skipkey, string kickOrMark)
        {
            if (handler.IsSimilarToSuggestedName(VotingOnThisBot.Name))
                return $"say_party twitch chat thinks '{VotingOnThisBot.Name}' is a bot    - deciding if I will {kickOrMark} ('{kickkey}') or not ('{skipkey}')";
            else
                return $"say_party '{VotingOnThisBot.Name}' named or muted like a past bot    - deciding if I will {kickOrMark} ('{kickkey}') or not ('{skipkey}')";
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
                //"say_party " +//TODO testing
                $"callvote kick \"{bot.GameID} {reason}\"";
        }


        private void Send(string command, Action<string> then)
        {
            handler.Send(command, then);
        }

        bool isPolling = false;
        private DateTime pollTime = DateTime.Now;
        private void AwaitResponse(Func<string, bool> validResponse, Action<string> afterresponse)
        {
            // eventual timeout on awaiting response.
            if (isPolling && DateTime.Now.Subtract(pollTime).TotalMinutes > 2)
            {
                afterresponse.Invoke(string.Empty);
                return;
            }

            PollOnce(kick_response_variable, (string resp) =>
            {
                //if (cancelled) return true;
                if (!validResponse(resp))
                    return false;

                ClearVariable(kick_response_variable, blankValue);
                afterresponse(resp);
                return true;
            });
        }

        private void PollOnce(string variableName, Func<string, bool> valueSetTest)
        {
            Send(variableName, (response) =>
            {
                if (valueSetTest(response))
                    isPolling = false;
            });
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
                _ = DoneKicking();
        }

        private Bot DoneKicking()
        {
            Bot bot = VotingOnThisBot;
            VotingOnThisBot = null;
            IsBusy = false;
            return bot;
        }

        private void ChoseToKick()
        {
            //TODO event handler instead
            handler.ChoseToKickBot(VotingOnThisBot);

            // setting up actual kick is the job of a normal Kick cycle - if the user was choosing we're in a mark cycle
            // ...kick will start in like 2 seconds if one isn't already in progress.
            _ = DoneKicking();
        }

        private void ChoseToSkip()
        {
            Bot bot = DoneKicking();
            //TODO event handler instead
            handler.ChoseToSkip(bot);
        }

        protected virtual void SetupAwaitKick()
        {
            AnnounceKicking();

            AwaitKick();
        }

        private void AwaitKick()
        {
            if (VotingOnThisBot != null
                && handler.IsBotPresent(VotingOnThisBot))
            {
                Send(VoteKickCommand(VotingOnThisBot), null);
            }
            else
            {
                Bot bot = DoneKicking();
                //if(!cancelled)
                //TODO event handler instead
                handler.BotKickOver(bot);
            }
        }

        private void AnnounceKicking()
        {
            Send(// "say_party trying to kick '"+ VotingOnThisBot.Name+"';" +
                // buzzer:
                //"play replay/replaydialog_warn.wav;"
                // harp strum:
                "play ui/mm_rank_up.wav;"
                , null);
        }
    }
}
