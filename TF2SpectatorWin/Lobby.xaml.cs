using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TF2SpectatorWin
{
    /// <summary>
    /// Interaction logic for Lobby.xaml
    /// </summary>
    public partial class Lobby : Window
    {
        public Lobby()
        {
            InitializeComponent();
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
