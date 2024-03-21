namespace TF2SpectatorWin
{
    /// <summary>
    /// a single command configuration entry
    /// </summary>
    internal class Config
    {
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

        public string NameAndAliases { get; set; }
        internal string[] Names =>
            //(IsEnabled ? 
            NameAndAliases
            //: NamesWithoutComment())
            .Split('|');
        public string CommandFormat { get; set; }
        public string CommandHelp { get; set; }
        public string ResponseFormat { get; set; }
    }
}