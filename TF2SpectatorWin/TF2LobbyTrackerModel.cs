using ASPEN;

using AspenWin;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
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
        private BotHandling BotHandler => _BotHandler
            ?? (_BotHandler = CreateBotHandler());

        private BotHandling CreateBotHandler()
        {
            BotHandling handler = new BotHandling(tF2WindowsViewModel.TF2,
                new BotHandlingConfig
                {
                    TF2Path = tF2WindowsViewModel.TF2Path,
                    PlayerlistPath = TF2WindowsViewModel.GetConfigFilePath("playerlist.json"),
                    //TODO config source
                    UserID = "[U:1:123650837]",
                });

            handler.GameLobbyUpdated +=
                (x) =>
                {
                    ViewNotification(nameof(LobbyRedCollection));
                    ViewNotification(nameof(LobbyBluCollection));
                };

            return handler;
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

        private ICommand _MarkBotCommand;
        public ICommand MarkBotCommand => _MarkBotCommand
            ?? (_MarkBotCommand = new RelayCommand<object>(
                execute: (o) => MarkSelectionAsBot(),
                canExecute: (o) => CanMarkBot()));

        private bool CanMarkBot()
        {
            bool redBad = LobbyRedSelected == null || LobbyRedSelected.IsMe;
            bool bluBad = LobbyBluSelected == null || LobbyBluSelected.IsMe;
            if (redBad && bluBad)
                return false;

            if (!redBad && !bluBad)
                return false;

            LobbyPlayer selected = redBad ? LobbyBluSelected : LobbyRedSelected;
            if (selected.IsFriend)
                return false;

            return !selected.IsBanned;
        }

        private void MarkSelectionAsBot()
        {
            LobbyPlayer selection = LobbyRedSelected ?? LobbyBluSelected;
            if (selection == null)
                return;
            //Aspen.Show?.QuestionToContinue("Kick {0} as a Cheater/Bot?", 
            //    () =>
            // next pass will see this as a banned bot and immediately start the kick (if it's the next entry).
            BotHandler.RecordAsABot(selection);
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
            LobbyPlayer selection = LobbyRedSelected ?? LobbyBluSelected;
            if (selection == null)
                return;

            BotHandler.UnRecordAsABot(selection);
        }

        private ICommand _MarkFriendCommand;
        public ICommand MarkFriendCommand => _MarkFriendCommand
            ?? (_MarkFriendCommand = new RelayCommand<object>(
                execute: (o) => MarkSelectionAsFriend(),
                canExecute: (o) => !((LobbyRedSelected ?? LobbyBluSelected)?.IsFriend ?? true)));

        private void MarkSelectionAsFriend()
        {
            LobbyPlayer selection = LobbyRedSelected ?? LobbyBluSelected;
            if (selection == null)
                return;
            //Aspen.Show.QuestionToContinue("Mark {0} as trusted?",
            //    () =>
            // next pass will see this as a banned bot and immediately start the kick (if it's the next entry).
            BotHandler.RecordAsAFriend(selection);
            //,
            //selection.StatusName);
        }

        //public IEnumerable<LobbyPlayer> LobbyRedPlayers => LobbyPlayers.Where(p => p.IsRED);

        //private IOrderedEnumerable<LobbyPlayer> LobbyPlayers
        //    => BotHandler.Players
        //    .OrderBy(p => p.StatusConnectedSeconds);

        //public IEnumerable<LobbyPlayer> LobbyBluPlayers => LobbyPlayers.Where(p => p.IsBLU);

        public LobbyPlayer LobbyRedCurrent { get; set; }
        public LobbyPlayer LobbyRedSelected { get; set; }
        private ICollectionView lobbyRedCollection;
        public ICollectionView LobbyRedCollection
        {
            get
            {
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
            BotHandler.RefreshPlayers();
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
            ICollectionView collectionView = new ListCollectionView(BotHandler.Players);
            collectionView.SortDescriptions.Add(
                new SortDescription(nameof(LobbyPlayer.StatusConnectedSeconds),
                ListSortDirection.Ascending));
            return collectionView;
        }

        public LobbyPlayer LobbyBluCurrent { get; set; }
        public LobbyPlayer LobbyBluSelected { get; set; }
        private ICollectionView lobbyBluCollection;
        public ICollectionView LobbyBluCollection
        {
            get
            {
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

            // every 5 seconds we get-lobby, load-detail, check for bots.
            // every get-lobby we also generate event that requests view refresh of red & blue lists
            // Get of those lists does RefreshPlayers - based on latest lobby info

            int delay = 5000;
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            var listener = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    BotHandler.Next();
                    //happens with next get of notified properties BotHandler.RefreshPlayers();
                    ViewNotification(nameof(LobbyRedCollection));
                    ViewNotification(nameof(LobbyBluCollection));

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