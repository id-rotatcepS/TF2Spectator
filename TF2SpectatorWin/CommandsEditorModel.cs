using AspenWin;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TF2SpectatorWin
{
    internal class CommandsEditorModel
    {
        public static readonly string CommandsConfigFilename = "TF2SpectatorCommands.tsv";
        public static readonly string ConfigFilePath = TF2SpectatorSettings.GetConfigFilePath(CommandsConfigFilename);

        // NOTE: ObservableCollection is important for DataGrid edit/delete to work correctly
        public ObservableCollection<Config> CommandData { get; }

        private TF2WindowsViewModel vm;
        private ASPEN.AspenLogging Log => ASPEN.Aspen.Log;

        public CommandsEditorModel(TF2WindowsViewModel tF2WindowsViewModel)
        {
            this.vm = tF2WindowsViewModel;

            CommandData = new ObservableCollection<Config>();

            CommandData.CollectionChanged +=
                (o, b) => CommandDataChanged = true;
        }

        private Commands win;
        private void OpenCommands()
        {
            if (win != null)
            {
                _ = win.Activate();
                return;
            }

            win = new Commands
            {
                DataContext = this
            };

            win.Closed += CommandsClosedSaveChanges;

            //win.CommandsDataGrid.AddingNewItem += (o, e) => { };
            //win.CommandsDataGrid.Drop += ;
            // I guess this is the modern version of .CellValueChanged and .CellEndEdit
            win.CommandsDataGrid.CellEditEnding += (o, editingEvent) =>
            {
                if (editingEvent.EditAction == DataGridEditAction.Commit)
                    CommandDataChanged = true;
            };
            win.CommandsDataGrid.CellEditEnding += UpdateAliasesOnRename;

            PrepareCommandData(win);

            _ = win.CommandsDataGrid.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Paste, OnPasteDataGrid, OnCanPasteDataGrid));

            win.Show();
        }

        private void UpdateAliasesOnRename(object o, DataGridCellEditEndingEventArgs editingEvent)
        {
            if (editingEvent.EditAction != DataGridEditAction.Commit)
                return;

            if (nameof(Config.NameAndAliases).Equals(editingEvent.Column.Header)
            && editingEvent.EditingElement is TextBox textBox
            && editingEvent.Row.Item is Config config)
            {
                string uncommitted = textBox?.Text;
                string unrenamed = config.Names[0];
                if (unrenamed == uncommitted)
                    return;

                RenameAliasTargets(unrenamed, uncommitted);
            }
        }

        private void RenameAliasTargets(string oldName, string newName)
        {
            for (int i = 0; i < CommandData.Count; i++)
            {
                Config otherconfig = CommandData[i];
                if (otherconfig.CommandFormat == oldName)
                    otherconfig.CommandFormat = newName;
            }
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
            if (!CommandDataChanged)
                return;

            BackupConfigFile();

            WriteCommandConfig(CommandData);

            // then reload the file - SetTwitchInstance
            if (vm.IsTwitchConnected)
            {
                _ = vm.SetTwitchInstance();
            }
        }

        private void BackupConfigFile()
        {
            if (File.Exists(ConfigFilePath))
                File.Copy(ConfigFilePath,
                    TF2SpectatorSettings.GetBackupFilePath(
                        string.Format("{0}-{1}",
                        DateTime.Now.ToString("yyyyMMddTHHmmss"),
                        CommandsConfigFilename)));
        }

        //TODO move to commandconfigmodel
        private void WriteCommandConfig(Collection<Config> commandData)
        {
            List<string> lines = new List<string>();
            foreach (Config config in commandData)
            {
                lines.Add(config.ToString());
            }
            try
            {
                lock (vm)
                {
                    File.WriteAllLines(ConfigFilePath, lines);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error saving commands");
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

        public void DataGridAdd(DataGrid dataGrid)
        {
            int newIndex = SelectedCommandIndex + 1;
            CommandData.Insert(newIndex, new Config("NAME\tCOMMAND"));

            //dataGrid.SelectedIndex = newIndex;

            //dataGrid.BeginEdit();
        }

        private ICommand _DataGridAdd;
        public ICommand DataGridAddCommand => _DataGridAdd
            ?? (_DataGridAdd = new RelayCommand<object>((o) => DataGridAdd(o as DataGrid)));

        #region paste
        // DataGrid doesn't include Paste (just copy), so we add it via CommandBinding Command="{x:Static ApplicationCommands.Paste}"
        // Uses Clipboard in WPF (PresentationCore.dll in v4 of the framework)

        public void OnCanPasteDataGrid(object dg, CanExecuteRoutedEventArgs args)
        {
            DataGrid grid = dg as DataGrid;

            args.CanExecute = grid.SelectedItems.Count > 0;
        }

        public void OnPasteDataGrid(object dg, ExecutedRoutedEventArgs args)
        {
            // adapted from https://learn.microsoft.com/en-us/archive/blogs/vinsibal/wpf-datagrid-clipboard-paste-sample
            // parse the clipboard data
            List<string[]> rowData = ParseClipboardData();

            // all the "grid." below is why the original post did this as a subclass.  But I just don't want to go there right now.
            DataGrid grid = dg as DataGrid;

            // call OnPastingCellClipboardContent for each cell
            int minRowIndex = grid.Items.IndexOf(grid.CurrentItem);
            int maxRowIndex = grid.Items.Count - 1;
            int rowDataIndex = 0;
            for (int i = minRowIndex; i < maxRowIndex && rowDataIndex < rowData.Count; i++, rowDataIndex++)
            {
                PasteRowData(grid, grid.Items[i], rowData[rowDataIndex]);
            }
        }

        private List<string[]> ParseClipboardData()
        {
            IDataObject dataObj = Clipboard.GetDataObject();
            if (dataObj != null)
            {
                string[] formats = dataObj.GetFormats();
                if (formats.Contains(DataFormats.Text))
                {
                    string clipboardString = (string)dataObj.GetData(DataFormats.Text);
                    // in the future this could be adapted to multiple possible algorithms for csv, ssv, etc. like the original post
                    // - but that's way more than I need and has its own issues.
                    return new SimpleTabulatedData().ParseRows(clipboardString);
                }
            }
            return new List<string[]>();
        }

        private void PasteRowData(DataGrid grid, object rowItem, string[] rowData)
        {
            int minColumnDisplayIndex = (grid.SelectionUnit != DataGridSelectionUnit.FullRow) ? grid.Columns.IndexOf(grid.CurrentColumn) : 0;
            int maxColumnDisplayIndex = grid.Columns.Count - 1;
            int columnDataIndex = 0;
            // NOTE - fixed bug from post: j <= not <
            for (int j = minColumnDisplayIndex; j <= maxColumnDisplayIndex && columnDataIndex < rowData.Length; j++, columnDataIndex++)
            {
                DataGridColumn column = grid.ColumnFromDisplayIndex(j);
                column.OnPastingCellClipboardContent(rowItem, rowData[columnDataIndex]);
            }
        }

        #endregion paste
    }
}