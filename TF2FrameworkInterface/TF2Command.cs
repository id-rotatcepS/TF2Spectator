namespace TF2FrameworkInterface
{
    public class TF2Command
    {
        public static readonly string[] BINDKEYS = new string[]
        {
            "ESCAPE",
            "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12",

            "`",
            "0","1","2","3","4","5","6","7","8","9",
            "-",
            "=",
            "BACKSPACE",

            "TAB",
            "[",
            "]",
            "\\",

            "CAPSLOCK",
            "SEMICOLON",
            "'",
            "ENTER",

            "SHIFT",
            ",",
            ".",
            "/",
            "RSHIFT",

            "CTRL",
            "LWIN",
            "ALT",
            "SPACE",
            "RWIN",
            "RALT",
            //(Menu Cannot be bound)
            "RCTRL",

            //(PrtScn Cannot be bound)
            "SCROLLLOCK",
            "PAUSE",

            "INS",
            "HOME",
            "PGUP",

            "DEL",
            "END",
            "PGDN",

            "UPARROW",
            "LEFTARROW",
            "DOWNARROW",
            "RIGHTARROW",

            "NUMLOCK",
            "KP_SLASH",
            "KP_MULTIPLY",
            "KP_MINUS",
            "KP_HOME",
            "KP_UPARROW",
            "KP_PGUP",
            "KP_PLUS",
            "KP_LEFTARROW",
            "KP_5",
            "KP_RIGHTARROW",
            "KP_END",
            "KP_DOWNARROW",
            "KP_PGDN",
            "KP_ENTER",
            "KP_INS",
            "KP_DEL",

            "MOUSE1",
            "MOUSE2",
            "MWHEELUP",
            "MOUSE3",
            "MWHEELDOWN",
            "MOUSE4", //(Left Button Click (forward))
            "MOUSE5", //(Right Button Click (back))

            "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
        };

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
