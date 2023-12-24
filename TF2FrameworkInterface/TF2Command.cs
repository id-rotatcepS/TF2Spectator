namespace TF2FrameworkInterface
{
    public class TF2Command
    {
        public string ConsoleString { get; internal set; }
    }

    public class StringCommand : TF2Command
    {
        public StringCommand(string cmd)
        {
            ConsoleString = cmd;
        }
    }

    public class Quit : TF2Command
    {
        public Quit()
        {
            ConsoleString = "quit";
        }
    }
}
