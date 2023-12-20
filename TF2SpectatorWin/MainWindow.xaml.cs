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
    }
}
