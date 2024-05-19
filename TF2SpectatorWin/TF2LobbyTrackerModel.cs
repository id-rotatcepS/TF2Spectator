using AspenWin;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using TF2FrameworkInterface;

namespace TF2SpectatorWin
{
    internal class TF2LobbyTrackerModel
    {
        private TF2WindowsViewModel tF2WindowsViewModel;

        private BotHandling _BotHandler = null;
        private BotHandling BotHandler => _BotHandler
            ?? (_BotHandler = new BotHandling(tF2WindowsViewModel.TF2, tF2WindowsViewModel.TF2Path));

        public TF2LobbyTrackerModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.tF2WindowsViewModel = tF2WindowsViewModel;
        }

        private ICommand _ParseCommand;
        public ICommand LobbyParseCommand => _ParseCommand
            ?? (_ParseCommand = new RelayCommand<object>(
                execute: (o) => ToggleParseAndLogLobby(),
                canExecute: (o) => true));

        private CancellationTokenSource cancellationTokenSource = null;
        private void ToggleParseAndLogLobby()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                return;
            }
            // based on https://stackoverflow.com/questions/23340894/polling-the-right-way
            cancellationTokenSource = new CancellationTokenSource();

            int delay = 15000;
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            var listener = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    BotHandler.Next();

                    Thread.Sleep(delay);
                    if (cancellationToken.IsCancellationRequested)
                        break;
                }
                cancellationTokenSource = null;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            // the TaskCreationOptions.LongRunning option tells the task-scheduler to not use a normal thread-pool thread
        }
    }
}