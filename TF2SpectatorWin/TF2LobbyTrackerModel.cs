﻿using ASPEN;
using AspenWin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TF2FrameworkInterface;

namespace TF2SpectatorWin
{
    //TODO consider using nuget package with this attribute instead [AddINotifyPropertyChangedInterface]
    public class TF2LobbyTrackerModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void ViewNotification(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private TF2WindowsViewModel tF2WindowsViewModel;

        private BotHandling _BotHandler = null;

        public string SteamUUID
        {
            get => tF2WindowsViewModel.SteamUUID;
            set => tF2WindowsViewModel.SteamUUID = value;
        }

        private BotHandling CreateBotHandler()
        {
            if (tF2WindowsViewModel.TF2 == null)
                return null;
            try
            {
                BotHandling handler = new BotHandling(tF2WindowsViewModel.TF2,
                    new BotHandlingConfig
                    {
                        TF2Path = tF2WindowsViewModel.TF2Path,
                        PlayerlistPath = TF2WindowsViewModel.GetConfigFilePath("playerlist.json"),
                        UserID = SteamUUID,
                    });

                handler.GameLobbyUpdated +=
                    (x) =>
                    {
                        ViewNotification(nameof(LobbyRedCollection));
                        ViewNotification(nameof(LobbyBluCollection));
                        ViewNotification(nameof(TeamColor));
                        ViewNotification(nameof(MeLabel));
                    };

                return handler;
            }
            catch (Exception ex)
            {
                Aspen.Log.Error("Unable to Start: " + ex.Message);
                return null;
            }
        }

        internal TF2LobbyTrackerModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.tF2WindowsViewModel = tF2WindowsViewModel;
        }

        private ICommand _ParseCommand;
        public ICommand LobbyParseCommand => _ParseCommand
            ?? (_ParseCommand = new RelayCommand<object>(
                execute: (o) => ToggleMonitorLobby(),
                canExecute: (o) => true));

        public Brush TeamColor =>
            IsParsing ? (
            _BotHandler?.MyTeam == TF2DebugLobby.TF_GC_TEAM_DEFENDERS ? RedTeamColor
            : _BotHandler?.MyTeam == TF2DebugLobby.TF_GC_TEAM_INVADERS ? BluTeamColor
            : null)
            : null;

        public string MeLabel
        {
            get
            {
                LobbyPlayer me = null;
                if (IsParsing)
                    me = GetMe();

                if (me != null && me.IsRED)
                    return "👉";
                if (me != null && me.IsBLU)
                    return "👈";
                return "Mark Me";
            }
        }

        private LobbyPlayer GetMe()
        {
            return _BotHandler?.Players.FirstOrDefault(player => player.IsMe);
        }

        private ICommand _MarkMeCommand;
        public ICommand MarkMeCommand => _MarkMeCommand
            ?? (_MarkMeCommand = new RelayCommand<object>(
                execute: (o) => MarkMe(),
                canExecute: (o) => CanMarkMe()));

        private void MarkMe()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            LobbyPlayer selected = GetLobbySelected();
            if (selected == null)
                return;

            SteamUUID = selected.SteamID;
            _BotHandler.MySteamUniqueID = SteamUUID;

            _BotHandler.RefreshPlayers();
        }

        private bool CanMarkMe()
        {
            return IsParsing
                && _BotHandler != null
                && _BotHandler.MyTeam == null
                && _BotHandler.Players.Any();
        }


        public void AddTwitchBotSuggestion(string name)
        {
            _BotHandler?.SuggestBotName(name);
        }

        public string GetBotInformation()
        {
            LobbyPlayer me = GetMe();
            IEnumerable<LobbyPlayer> bots = _BotHandler?.Players
                .Where(p => p.IsBanned);

            string mybots = GetBotInformation(bots.Where(
                p => p.IsRED == me.IsRED));
            string others = GetBotInformation(bots.Where(
                p => p.IsRED != me.IsRED));

            string result = string.Empty;
            if (!string.IsNullOrEmpty(mybots))
                result +=
                    "On my team:\n"
                    + mybots;

            if (!string.IsNullOrEmpty(others))
                result +=
                    "On the other team:\n"
                    + others;

            return result;
        }

        private string GetBotInformation(IEnumerable<LobbyPlayer> bots)
        {
            return string.Join("\n",
                            bots.Select(p => string.Format(
                                "{2}{1}{0}",
                                p.StatusName,
                                p.TextIcon,
                                p.IsMissing ? "❌" : ""
                                )
                            ));
        }

        private ICommand _MarkBotCommand;
        public ICommand MarkBotCommand => _MarkBotCommand
            ?? (_MarkBotCommand = new RelayCommand<object>(
                execute: (o) => MarkSelectionAsBot(),
                canExecute: (o) => CanMarkBot()));

        private bool CanMarkBot()
        {
            LobbyPlayer selected = GetLobbySelected();
            if (selected == null)
                return false;

            if (selected.IsFriend)
                return false;

            return !selected.IsBanned;
        }

        private LobbyPlayer GetLobbySelected()
        {
            bool redBad = LobbyRedSelected == null || LobbyRedSelected.IsMe;
            bool bluBad = LobbyBluSelected == null || LobbyBluSelected.IsMe;
            if (redBad && bluBad)
                return null;

            if (!redBad && !bluBad)
                return null;

            return redBad ? LobbyBluSelected : LobbyRedSelected;
        }

        private void MarkSelectionAsBot()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            LobbyPlayer selection = GetLobbySelected();
            if (selection == null)
                return;
            //Aspen.Show?.QuestionToContinue("Kick {0} as a Cheater/Bot?", 
            //    () =>
            // next pass will see this as a banned bot and immediately start the kick (if it's the next entry).
            _BotHandler.RecordAsABot(selection);
            //,
            //selection.StatusName);
        }

        private ICommand _MarkNotBotCommand;
        public ICommand MarkNotBotCommand => _MarkNotBotCommand
            ?? (_MarkNotBotCommand = new RelayCommand<object>(
                execute: (o) => MarkSelectionNotABot(),
                canExecute: (o) => (LobbyRedSelected ?? LobbyBluSelected)?.IsUserBanned ?? false));
        private void MarkSelectionNotABot()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            LobbyPlayer selection = LobbyRedSelected ?? LobbyBluSelected;
            if (selection == null)
                return;

            _BotHandler.UnRecordAsABot(selection);
        }

        private ICommand _MarkFriendCommand;
        public ICommand MarkFriendCommand => _MarkFriendCommand
            ?? (_MarkFriendCommand = new RelayCommand<object>(
                execute: (o) => MarkSelectionAsFriend(),
                canExecute: (o) => !((LobbyRedSelected ?? LobbyBluSelected)?.IsFriend ?? true)));

        private void MarkSelectionAsFriend()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            LobbyPlayer selection = GetLobbySelected();
            if (selection == null)
                return;
            //Aspen.Show.QuestionToContinue("Mark {0} as trusted?",
            //    () =>
            // next pass will see this as a banned bot and immediately start the kick (if it's the next entry).
            _BotHandler.RecordAsAFriend(selection);
            //,
            //selection.StatusName);
        }

        //public IEnumerable<LobbyPlayer> LobbyRedPlayers => LobbyPlayers.Where(p => p.IsRED);

        //private IOrderedEnumerable<LobbyPlayer> LobbyPlayers
        //    => BotHandler.Players
        //    .OrderBy(p => p.StatusConnectedSeconds);

        //public IEnumerable<LobbyPlayer> LobbyBluPlayers => LobbyPlayers.Where(p => p.IsBLU);

        public Brush RedTeamColor => new SolidColorBrush(Colors.HotPink);
        public LobbyPlayer LobbyRedCurrent { get; set; }
        public LobbyPlayer LobbyRedSelected { get; set; }
        private ICollectionView lobbyRedCollection;
        public ICollectionView LobbyRedCollection
        {
            get
            {
                if (!IsParsing)
                    return null;
                if (_BotHandler == null)
                    return null;
                RefreshLobbyDetails();
                lobbyRedCollection?.Refresh();

                return lobbyRedCollection
                    ?? (lobbyRedCollection = CreateLobbyRedView());
            }
        }

        private void RefreshLobbyDetails()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            _BotHandler.RefreshPlayers();
        }

        private ICollectionView CreateLobbyRedView()
        {
            ICollectionView collectionView = CreateLobbyViewSorted();
            collectionView.Filter = (i) => (i as LobbyPlayer).IsRED;
            return collectionView;
        }

        private ICollectionView CreateLobbyViewSorted()
        {
            //ICollectionView collectionView = CollectionViewSource.GetDefaultView(BotHandler.Players);
            // get independent instances to filter, not the default all-players viewer.
            ICollectionView collectionView = new ListCollectionView(_BotHandler.Players);
            collectionView.SortDescriptions.Add(
                new SortDescription(nameof(LobbyPlayer.StatusConnectedSeconds),
                ListSortDirection.Ascending));
            return collectionView;
        }

        public Brush BluTeamColor => new SolidColorBrush(Colors.CornflowerBlue);
        public LobbyPlayer LobbyBluCurrent { get; set; }
        public LobbyPlayer LobbyBluSelected { get; set; }
        private ICollectionView lobbyBluCollection;
        public ICollectionView LobbyBluCollection
        {
            get
            {
                if (!IsParsing)
                    return null;
                if (_BotHandler == null)
                    return null;
                RefreshLobbyDetails();
                lobbyBluCollection?.Refresh();

                return lobbyBluCollection
                    ?? (lobbyBluCollection = CreateLobbyBluView());
            }
        }

        private ICollectionView CreateLobbyBluView()
        {
            ICollectionView collectionView = CreateLobbyViewSorted();
            collectionView.Filter = (i) => (i as LobbyPlayer).IsBLU;
            return collectionView;
        }


        private CancellationTokenSource cancellationTokenSource = null;
        private void ToggleMonitorLobby()
        {
            if (IsParsing)
            {
                cancellationTokenSource.Cancel();
                ViewNotification(nameof(IsParsing));
                return;
            }
            // based on https://stackoverflow.com/questions/23340894/polling-the-right-way
            cancellationTokenSource = new CancellationTokenSource();
            ViewNotification(nameof(IsParsing));

            // every 2 seconds we get-lobby, load-detail, check for bots.
            // every get-lobby we also generate event that requests view refresh of red & blue lists
            // Get of those lists does RefreshPlayers - based on latest lobby info

            int delay = 2000;
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            var listener = Task.Factory.StartNew(() =>
            {
                _BotHandler = CreateBotHandler();
                while (_BotHandler != null)
                {
                    _BotHandler.Next();
                    //happens with next get of notified properties BotHandler.RefreshPlayers();
                    ViewNotification(nameof(LobbyRedCollection));
                    ViewNotification(nameof(LobbyBluCollection));
                    ViewNotification(nameof(TeamColor));
                    ViewNotification(nameof(MeLabel));

                    Thread.Sleep(delay);
                    if (cancellationToken.IsCancellationRequested)
                        break;
                }
                cancellationTokenSource = null;
                ViewNotification(nameof(IsParsing));
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);  
            // the TaskCreationOptions.LongRunning option tells the task-scheduler to not use a normal thread-pool thread
        }

        public bool IsParsing
            => cancellationTokenSource != null
            && !cancellationTokenSource.IsCancellationRequested;
    }
}