using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TF2FrameworkInterface
{
    /// <summary>
    /// TF2's tf/voice_ban.dt which is technically a binary file. 
    /// Unfortunately, the file only gets updated on tf2 shutdown, and probably is only read on startup.
    /// ... and worse, it's a FIFO list of 256 entries max.
    /// 
    /// After the first few bytes, the rest of the content is null-padded 32 byte uniqueid strings.
    /// i.e. SOH NUL NUL NUL [U:1:1234567] NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL NUL
    /// </summary>
    public class TF2VoiceBanFile
    {
        public string FileName => "voice_ban.dt";

        public List<string> UIDs { get; private set; } = new List<string>();

        private static int EntryLength = 32;
        private static int HeaderLength = 4;

        private string TF2Path;
        public TF2VoiceBanFile(string TF2Path)
        {
            this.TF2Path = Path.Combine(Path.Combine(TF2Path, "tf"), FileName);
        }

        //private static byte SOH = ??;
        private static byte NUL = 0;

        //TODO if it's just on startup, then make this private and part of the constructor and remove the events.
        public void Load()
        {
            if (!File.Exists(TF2Path))
                return;

            byte[] bytes = File.ReadAllBytes(TF2Path);

            // check size and header are "proper"
            if (bytes.Length < HeaderLength)
                return;
            if (//bytes[0] != SOH ||
                bytes[1] != NUL ||
                bytes[2] != NUL ||
                bytes[3] != NUL)
                return;

            List<string> updatedUIDs = new List<string>();
            for (int entryPosition = HeaderLength;
                entryPosition + EntryLength <= bytes.Length;
                entryPosition += EntryLength)
                updatedUIDs.Add(GetUIDString(bytes, entryPosition));

            IEnumerable<string> missingUIDs = UIDs.Except(updatedUIDs);
            IEnumerable<string> newUIDs = updatedUIDs.Except(UIDs);

            UIDs.RemoveAll(id => missingUIDs.Contains(id));
            UIDs.AddRange(newUIDs);

            if (missingUIDs.Any())
                VoiceUnbanned?.Invoke(missingUIDs);
            if (newUIDs.Any()) 
                VoiceBanned?.Invoke(newUIDs);
        }
        public delegate void VoiceBanAdded(IEnumerable<string> newUIDs);
        public event VoiceBanAdded VoiceBanned;

        public delegate void VoiceBanRemoved(IEnumerable<string> removedUIDs);
        public event VoiceBanRemoved VoiceUnbanned;

        private static string GetUIDString(byte[] bytes, int entryPosition)
        {
            int length = GetStringLengthWithoutNullPadding(bytes, entryPosition);
            return System.Text.Encoding.UTF8.GetString(bytes, entryPosition, length);
        }

        private static int GetStringLengthWithoutNullPadding(byte[] bytes, int entryPosition)
        {
            int length;
            for (length = 0; length < EntryLength; length++)
                if (bytes[entryPosition + length] == NUL)
                    return length;

            return length;
        }
    }

}
