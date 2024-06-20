namespace TF2FrameworkInterface
{
    /// <summary>
    /// In-game "play"-able sound files.
    /// Examples of sound files that could be played are at https://www.sounds-resource.com/pc_computer/tf2/
    /// </summary>
    public class TF2Sound
    {
        public static readonly TF2Sound[] SOUNDS = new TF2Sound[]
        {
            new TF2Sound(string.Empty)
            {
                Description = "(none)"
            },
            new TF2Sound("replay/rendercomplete.wav")
            {
                Description = "front desk bell (replay render complete)"
            },
            new TF2Sound("replay/replaydialog_warn.wav")
            {
                Description = "buzzer (replay warn)"
            },
            new TF2Sound("ui/mm_rank_up.wav")
            {
                Description = "harp strum (rank up)"
            },
            new TF2Sound("player/cyoa_pda_beep4.wav")
            {
                Description = "beep 4 (pda)"
            },
            new TF2Sound("ambient/lair/jungle_alarm.wav")
            {
                Description = "alarm honk (jungle lair) - untested" //TODO not tested, maybe not ambient in front
            },
            // didn't work when I tried, might require mode enabled?: "ui/coach/go_here.wav" // coach whistle
        };

        public TF2Sound(string file)
        {
            File = file;
        }
        public TF2Sound(string file, string description)
        {
            File = file;
            Description = description;
        }

        public string File { get; }
        public string Description { get; set; }
        public bool IsActive => !string.IsNullOrWhiteSpace(File);
    }
}
