using AspenWin;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace TF2SpectatorWin
{
    internal class CommandsEditorModel
    {
        public static readonly string CommandsConfigFilename = "TF2SpectatorCommands.tsv";
        public static readonly string ConfigFilePath = TF2WindowsViewModel.GetConfigFilePath(CommandsConfigFilename);

        // NOTE: ObservableCollection is important for DataGrid edit/delete to work correctly
        public ObservableCollection<Config> CommandData { get; }

        private TF2WindowsViewModel vm;
        public CommandsEditorModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.vm = tF2WindowsViewModel;

            CommandData = new ObservableCollection<Config>();

            CommandData.CollectionChanged +=
                (o, b) => CommandDataChanged = true;
        }

        Commands win;
        private void OpenCommands()
        {
            if (win != null)
            {
                win.Activate();
                return;
            }

            win = new Commands();
            //win.CommandsDataGrid.AddingNewItem += (o, e) => { };
            //win.CommandsDataGrid.Drop += ;
            // I guess this is the modern version of .CellValueChanged and .CellEndEdit
            win.CommandsDataGrid.CellEditEnding += (o, editingEvent) =>
            {
                if (editingEvent.EditAction == System.Windows.Controls.DataGridEditAction.Commit)
                {
                    CommandDataChanged = true;
                    //b.Row.Item; 
                }
            };

            PrepareCommandData(win);

            win.DataContext = this;

            win.Closed += CommandsClosedSaveChanges;

            win.Show();
        }

        private void PrepareCommandData(Commands win)
        {
            CommandData.Clear();
            foreach (string config in vm.ReadCommandConfig())
            {
                if (config.Trim().Length == 0)
                    continue;
                try
                {
                    Config configobj = new Config(config);
                    CommandData.Add(configobj);
                }
                catch (Exception)
                {
                    //"bad command config: " + config
                }
            }
            CommandDataChanged = false;
        }

        private bool CommandDataChanged = false;

        private void CommandsClosedSaveChanges(object sender, EventArgs e)
        {
            win = null;
            if (!CommandDataChanged) return;

            BackupConfigFile();

            WriteCommandConfig();

            // then reload the file - SetTwitchInstance
            if (vm.IsTwitchConnected)
            {
                vm.SetTwitchInstance();
            }
        }

        private void BackupConfigFile()
        {
            if (File.Exists(ConfigFilePath))
                File.Copy(ConfigFilePath,
                    TF2WindowsViewModel.GetBackupFilePath(
                        string.Format("{0}-{1}",
                        DateTime.Now.ToString("yyyyMMddTHHmmss"),
                        CommandsConfigFilename)));
        }

        private void WriteCommandConfig()
        {
            List<string> lines = new List<string>();
            foreach (Config config in CommandData)
            {
                lines.Add(config.ToString());
            }
            try
            {
                File.WriteAllLines(ConfigFilePath, lines);
            }
            catch (Exception ex)
            {
                vm.Log.Error(ex.Message);
            }
        }

        private ICommand _OpenCommands;
        public ICommand OpenCommandsCommand => _OpenCommands
            ?? (_OpenCommands = new RelayCommand<object>((o) => OpenCommands()));


        public Config SelectedCommand { get; set; }
        public int SelectedCommandIndex { get; set; }

        public void DataGridUp()
        {
            if (SelectedCommandIndex < 0 || SelectedCommandIndex == 0)
                return;
            int newIndex = SelectedCommandIndex - 1;
            CommandData.Move(SelectedCommandIndex, newIndex);
        }

        private ICommand _DataGridUp;
        public ICommand DataGridUpCommand => _DataGridUp
            ?? (_DataGridUp = new RelayCommand<object>((o) => DataGridUp()));

        public void DataGridDown()
        {
            if (SelectedCommandIndex < 0 || SelectedCommandIndex >= CommandData.Count - 1)
                return;
            int newIndex = SelectedCommandIndex + 1;
            CommandData.Move(SelectedCommandIndex, newIndex);
        }

        private ICommand _DataGridDown;
        public ICommand DataGridDownCommand => _DataGridDown
            ?? (_DataGridDown = new RelayCommand<object>((o) => DataGridDown()));

        public void DataGridAdd(System.Windows.Controls.DataGrid dataGrid)
        {
            int newIndex = SelectedCommandIndex + 1;
            CommandData.Insert(newIndex, new Config("NAME\tCOMMAND"));

            //dataGrid.SelectedIndex = newIndex;

            //dataGrid.BeginEdit();
        }

        private ICommand _DataGridAdd;
        public ICommand DataGridAddCommand => _DataGridAdd
            ?? (_DataGridAdd = new RelayCommand<object>((o) => DataGridAdd(o as System.Windows.Controls.DataGrid)));

    }
}