using ASPEN;

using AspenWin;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
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

        public BotHandlingConfig BotConfig => Aspen.Option.Get<BotHandlingConfig>(nameof(BotHandlingConfig));

        public string[] BINDKEYS => TF2Command.BINDKEYS;

        public TF2Sound[] SOUNDS => TF2Sound.SOUNDS;

        private BotHandling CreateBotHandler()
        {
            if (tF2WindowsViewModel.TF2 == null)
                return null;
            try
            {
                BotConfig.TF2Path = tF2WindowsViewModel.TF2Path;
                BotConfig.PlayerlistPath = TF2SpectatorSettings.GetConfigFilePath("playerlist.json");
                BotConfig.UserID = SteamUUID;
                BotHandling handler = new BotHandling(tF2WindowsViewModel.TF2,
                    BotConfig);

                handler.GameLobbyUpdated += LobbyUpdate;

                return handler;
            }
            catch (Exception ex)
            {
                Aspen.Log.ErrorException(ex, "Unable to Start");
                return null;
            }
        }

        private void LobbyUpdate(BotHandling handler)
        {
            LobbyUpdateDebug(handler);

            ViewNotification(nameof(LobbyRedCollection));
            ViewNotification(nameof(LobbyBluCollection));
            ViewNotification(nameof(TeamColor));
            ViewNotification(nameof(MeLabel));
        }

        private string lastLobbyUpdate = null;
        private void LobbyUpdateDebug(BotHandling handler)
        {
            return;

            string newLobbyUpdate = string.Format("G Lobby Upd: disp w/info {0}/{1}: t_d_l {2}, bot w/info {3}/{4}",
                handler.Players.Count(p => p.HaveKickInfo),
                handler.Players.Count,
                handler.Lobby.TF2DebugLobbyStatus.Count(),
                handler.Bots.Count(b => b.Status != null),
                handler.Bots.Count()
                );
            if (lastLobbyUpdate != newLobbyUpdate)
                Aspen.Log.Info(lastLobbyUpdate = newLobbyUpdate);
        }

        internal TF2LobbyTrackerModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.tF2WindowsViewModel = tF2WindowsViewModel;
        }

        private ICommand _InstallVoteEraser;
        public ICommand InstallVoteEraserCommand => _InstallVoteEraser
            ?? (_InstallVoteEraser = new RelayCommand<object>(
                (o) => InstallVoteEraser(),
                (o) => !IsVoteEraserInstalled()));

        private string TF2CustomHudsPath => Path.Combine(tF2WindowsViewModel.TF2Path, @"tf\custom\");
        private string HudFolderName = "aaaaaaaaaa_votefailed_eraser_v2";

        private void InstallVoteEraser()
        {
            UseOverrideCursor(Cursors.Wait, () =>
            {
                string path = Path.Combine(Path.GetTempPath(), "master.zip");

                TF2BDFiles.CopyURLToFile("https://github.com/PazerOP/tf2_bot_detector/archive/refs/heads/master.zip", path);
                string addonsArchivePath = @"tf2_bot_detector-master/staging/tf2_addons/";

                ExtractZipSubfolder(path, addonsArchivePath, TF2CustomHudsPath);

                File.Delete(path);
            });
        }

        /// <summary>
        /// Opens a ZipFile and extracts a folder in it to a destionation folder.
        /// </summary>
        /// <param name="path">zip file to open</param>
        /// <param name="sourcePath">zip folder prefix path to extract. Must use forward slashes to match <see cref="ZipArchiveEntry.FullName"/></param>
        /// <param name="destPath">folder in which to extract zip folder contents</param>
        public static void ExtractZipSubfolder(string path, string sourcePath, string destPath)
        {
            using (ZipArchive zip = ZipFile.OpenRead(path))
                foreach (ZipArchiveEntry entry in zip.Entries)
                    ExtractZipSubfolder(entry, sourcePath, destPath);
        }

        private static void ExtractZipSubfolder(ZipArchiveEntry entry, string sourcePath, string destPath)
        {
            if (!entry.FullName.StartsWith(sourcePath))
                return;

            string addOnRelative = entry.FullName.Substring(sourcePath.Length);
            string extractFullPath = Path.Combine(destPath, addOnRelative);

            bool isDirectory = string.IsNullOrEmpty(entry.Name);
            if (isDirectory)
                Directory.CreateDirectory(extractFullPath);
            else
                entry.ExtractToFile(extractFullPath);
        }

        private bool IsVoteEraserInstalled()
        {
            string modPath = Path.Combine(TF2CustomHudsPath, HudFolderName);
            return Directory.Exists(modPath);
        }

        /// <summary>
        /// set Mouse.OverrideCursor during the action.
        /// </summary>
        /// <param name="cur">a cursor from <see cref="Cursors"/></param>
        /// <param name="action"></param>
        public static void UseOverrideCursor(Cursor cur, Action action)
        {
            Cursor previous = Mouse.OverrideCursor;
            try
            {
                Mouse.OverrideCursor = cur;

                action?.Invoke();
            }
            finally
            {
                Mouse.OverrideCursor = previous;
            }
        }

        private ICommand _OpenLobby;
        public ICommand OpenLobbyCommand => _OpenLobby
            ?? (_OpenLobby = new RelayCommand<object>(
                (o) => OpenLobby(),
                (o) => !string.IsNullOrWhiteSpace(tF2WindowsViewModel.TF2Path)));

        private Lobby win;
        private void OpenLobby()
        {
            if (win != null)
            {
                win.Activate();
                return;
            }

            win = new Lobby
            {
                DataContext = this
            };

            win.Closed += (x, eventArgs) =>
            {
                EndMonitorLobby();
                win = null;
            };

            StartMonitorLobby();
            win.Show();
        }

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
                && GetLobbySelected() != null;
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

            _BotHandler.RecordAsABot(selection);
        }

        private ICommand _UnmarkCommand;
        public ICommand UnmarkSelectionCommand => _UnmarkCommand
            ?? (_UnmarkCommand = new RelayCommand<object>(
                execute: (o) => UnmarkSelection(),
                canExecute: (o) => CanUnmark()));

        private void UnmarkSelection()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            LobbyPlayer selection = GetLobbySelected();
            if (selection == null)
                return;

            if (selection.IsUserBanned)
                _BotHandler.UnRecordAsABot(selection);
            if (selection.IsFriend)
                _BotHandler.UnRecordAsAFriend(selection);
        }

        private bool CanUnmark()
        {
            LobbyPlayer selection = GetLobbySelected();
            if (selection == null)
                return false;

            return selection.IsUserBanned
                || selection.IsFriend;
        }

        private ICommand _MarkFriendCommand;
        public ICommand MarkFriendCommand => _MarkFriendCommand
            ?? (_MarkFriendCommand = new RelayCommand<object>(
                execute: (o) => MarkSelectionAsFriend(),
                canExecute: (o) => CanMarkFriend()));

        private void MarkSelectionAsFriend()
        {
            if (!IsParsing)
                return;
            if (_BotHandler == null)
                return;

            LobbyPlayer selection = GetLobbySelected();
            if (selection == null)
                return;

            _BotHandler.RecordAsAFriend(selection);
        }

        private bool CanMarkFriend()
        {
            LobbyPlayer selected = GetLobbySelected();
            if (selected == null)
                return false;

            return !selected.IsFriend;
        }


        public Brush RedTeamColor => new SolidColorBrush(Colors.HotPink);
        public LobbyPlayer LobbyRedCurrent { get; set; }
        public LobbyPlayer LobbyRedSelected { get; set; }
        private ICollectionView lobbyRedCollection;
        public ICollectionView LobbyRedCollection
        {
            get
            {
                if (lobbyRedCollection == null)
                    if (!IsParsing || _BotHandler == null)
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
            // create independent instances to filter, not the default all-players viewer.
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
                if (lobbyBluCollection == null)
                    if (!IsParsing || _BotHandler == null)
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
        private void EndMonitorLobby()
        {
            if (!IsParsing)
                return;

            cancellationTokenSource?.Cancel();
            ViewNotification(nameof(IsParsing));
            ViewNotification(nameof(LobbyBluCollection));
            ViewNotification(nameof(LobbyRedCollection));
        }

        private void StartMonitorLobby()
        {
            // based on https://stackoverflow.com/questions/23340894/polling-the-right-way
            cancellationTokenSource = new CancellationTokenSource();
            ViewNotification(nameof(IsParsing));

            // every {delay} seconds we get-lobby, load-detail, check for bots.
            // every get-lobby we also generate event that requests view refresh of red & blue lists
            // Get of those lists does RefreshPlayers - based on latest lobby info

            int delay = 1500;
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