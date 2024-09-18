using System.ComponentModel;

namespace TF2SpectatorWin
{
    /// <summary>
    /// a single command configuration entry
    /// </summary>
    internal class Config : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void ViewNotification(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotifyPropertyChanged

        public const char CommandSeparator = '\t';

        public Config(string config)
        {
            string[] commandParts = config.Split(CommandSeparator);

            string namePart = commandParts[0];
            NameAndAliases = namePart;

            CommandFormat = commandParts[1];
            CommandHelp = commandParts.Length > 2 ? commandParts[2] : string.Empty;
            ResponseFormat = commandParts.Length > 3 ? commandParts[3] : string.Empty;
        }

        public override string ToString()
        {
            return NameAndAliases + CommandSeparator + CommandFormat + CommandSeparator + CommandHelp + CommandSeparator + ResponseFormat;
        }

        //internal readonly string COMMENT = "#";
        //public bool IsEnabled
        //{
        //    get => !NameAndAliases.StartsWith(COMMENT);
        //    //set
        //    //{
        //    //    if (IsEnabled && value) NameAndAliases = COMMENT + NameAndAliases;
        //    //    else
        //    //    if (!IsEnabled && !value) NameAndAliases = NamesWithoutComment();
        //    //}
        //}

        //private string NamesWithoutComment()
        //{
        //    return NameAndAliases?.Substring(COMMENT.Length);
        //}

        private string nameAndAliases;
        public string NameAndAliases
        {
            get => nameAndAliases;
            set
            {
                nameAndAliases = value;
                ViewNotification(nameof(NameAndAliases));
            }
        }

        internal string[] Names =>
            //(IsEnabled ? 
            NameAndAliases
            //: NamesWithoutComment())
            .Split('|');

        private string commandFormat;
        public string CommandFormat
        {
            get => commandFormat;
            set
            {
                commandFormat = value;
                ViewNotification(nameof(CommandFormat));
            }
        }

        private string commandHelp;
        public string CommandHelp
        {
            get => commandHelp;
            set
            {
                commandHelp = value;
                ViewNotification(nameof(CommandHelp));
            }
        }

        private string responseFormat;
        public string ResponseFormat
        {
            get => responseFormat;
            set
            {
                responseFormat = value;
                ViewNotification(nameof(ResponseFormat));
            }
        }
    }
}