using System.Collections.Generic;

using Newtonsoft.Json;

namespace TF2FrameworkInterface
{
    /// <summary>
    /// In-game "play"-able sound files.
    /// Examples of sound files that could be played are at https://www.sounds-resource.com/pc_computer/tf2/
    /// also apparently all of HL1/2 is available: https://wiki.facepunch.com/gmod/HL2_Sound_List
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
            new TF2Sound("player/cyoa_pda_beep5.wav")
            {
                Description = "beep 5 (pda)"
            },
            new TF2Sound("player/cyoa_pda_beep6.wav")
            {
                Description = "beep 6 (pda)"
            },
            // loops
            //new TF2Sound("ambient/lair/jungle_alarm.wav")
            //{
            //    Description = "alarm honk (jungle lair)"
            //},

            new TF2Sound("hl1/fvox/warning.wav")
            {
                Description = "HL1 'warning'"
            },
            new TF2Sound("HL1/fvox/targetting_system.wav")
            {
                Description = "HL1 'automatic target acquisition system'"
            },
            new TF2Sound("HL1/fvox/biohazard_detected.wav")
            {
                Description = "HL1 'warning biohazard detected'"
            },
            new TF2Sound("HL1/fvox/chemical_detected.wav")
            {
                Description = "HL1 'warning hazardous chemical detected'"
            },
            new TF2Sound("hl1/fvox/activated.wav")
            {
                Description = "HL1 'activated'"
            },
            new TF2Sound("hl1/fvox/acquired.wav")
            {
                Description = "HL1 'acquired'"
            },

            new TF2Sound("hl1/fvox/blip.wav")
            {
                Description = "HL1 blip"
            },
            new TF2Sound("hl1/fvox/fuzz.wav")
            {
                Description = "HL1 fuzz (beep)"
            },

            new TF2Sound("common/warning.wav")
            {
                Description = "HL warning beep"
            },
            new TF2Sound("buttons/button3.wav")
            {
                Description = "HL button"
            },
            new TF2Sound("npc/scanner/combat_scan5.wav")
            {
                Description = "HL boop bass worble"
            },
            // loops
            //new TF2Sound("npc/scanner/combat_scan_loop4.wav")
            //{
            //    Description = "HL bass worble"
            //},
            // loops
            //new TF2Sound("npc/turret_floor/alarm.wav")
            //{
            //    Description = "HL turret alarm"
            //},

            new TF2Sound("vo/breencast/br_welcome07.wav")
            {
                Description = "HL Dr. Breen 'It's safer here'"
            },
            new TF2Sound("vo/citadel/al_heylisten.wav")
            {
                Description = "HL 'Hey, listen'"
            },
            new TF2Sound("vo/citadel/gman_exit02.wav")
            {
                Description = "HL G-man 'Is it really that time again?'"
            },
            new TF2Sound("vo/gman_misc/gman_riseshine.wav")
            {
                Description = "HL G-man 'Rise and shine Mr. Freeman - rise and shine'"
            },
            new TF2Sound("npc/metropolice/vo/matchonapblikeness.wav")
            {
                Description = "HL Combine 'I have a match on APB likeness'"
            },      

            //"Player sounds"
            // mud3.wav
            // mud4.wav
            // can throw: pl_scout_dodge_can_pitch.wav
            // neon annih bass: sign_bass_solo.wav
            // slosh1 .. 4.wav
            // taunt_bell.wav (boxing)
            // taunt_eng_gunslinger.wav
            // taunt_eng_strum.wav
            // taunt_eng_smash1 .. 3.wav
            // taunt_knuckle_crack.wav
            // taunt_medic_heroic.wav
            // taunt_pyro_balloonicorn.wav _hellicorn.wav
            // taunt_shake_it.wav (squeaky)
            // taunt_v01 .. 07.wav (medic saw)
            // taunt_wormshhg.wav (hallelueah)

            // didn't work when I tried, might require mode enabled?: "ui/coach/go_here.wav" // coach whistle
        };

        /// <summary>
        /// Needed for Json deserializing, along with the File property's setter.
        /// </summary>
        public TF2Sound()
        {
        }

        public TF2Sound(string file)
        {
            File = file;
        }
        public TF2Sound(string file, string description)
        {
            File = file;
            Description = description;
        }

        public string File { get; set; }
        public string Description { get; set; }

        [JsonIgnore]
        public bool IsActive => !string.IsNullOrWhiteSpace(File);

        // auto-generated by VS
        public override bool Equals(object obj)
        {
            return obj is TF2Sound sound &&
                   File == sound.File;
        }

        // auto-generated by VS
        public override int GetHashCode()
        {
            return -825322277 + EqualityComparer<string>.Default.GetHashCode(File);
        }
    }
}
