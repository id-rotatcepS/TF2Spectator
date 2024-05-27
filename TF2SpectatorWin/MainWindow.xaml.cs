using System.Windows;

namespace TF2SpectatorWin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = new TF2WindowsViewModel();

            this.Closing += ((TF2WindowsViewModel)this.DataContext).ClosingHandler;
        }

        private void log_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // don't auto-scroll if the user was accessing the log when the new text came in.
            if (logScroller.VerticalOffset != logScroller.ScrollableHeight)
                return;

            // auto-scroll the log
            logScroller.ScrollToEnd();
        }

        private void RedLobby_GotFocus(object sender, RoutedEventArgs e)
        {
            BluLobby.SelectedItem = null;
        }

        private void BluLobby_GotFocus(object sender, RoutedEventArgs e)
        {
            RedLobby.SelectedItem = null;
        }
    }
}
