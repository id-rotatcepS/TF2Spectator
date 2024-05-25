using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace TF2FrameworkInterface
{
    /// <summary>
    /// Embody the tf2_bot_detector JSON files
    /// </summary>
    public class TF2BDFiles
    {
        private List<TF2BD> files = new List<TF2BD>();
        private TF2BD userfile;
        private string userfilename;

        public TF2BDFiles(string playerlistPath)
        {
            AddFile(playerlistPath);
        }

        public void AddURLFile(string url)
        {
            string name = "players." + files.Count + ".json";
            string filepath = Path.Combine(Path.GetTempPath(), name);

            CopyURLToFile(url, filepath);

            AddFile(filepath);
        }

        private static void CopyURLToFile(string url, string filepath)
        {
            using (HttpClient client = new HttpClient())
            using (var s = client.GetStreamAsync(url))
            using (FileStream fs = new FileStream(filepath, FileMode.OpenOrCreate))
                s.Result.CopyTo(fs);
        }

        /// <summary>
        /// If this is the first file added, assumes that is the user file for recording new entries and saving later.
        /// </summary>
        /// <param name="filename"></param>
        public void AddFile(string filename)
        {
            string json = File.ReadAllText(filename);
            // make sure userfile isn't established by a race condition
            lock (files)
            {
                files.Add(JsonConvert.DeserializeObject<TF2BD>(json));

                if (files.Count == 1)
                {
                    userfile = files[0];
                    userfilename = filename;
                }
            }
        }

        public void SaveUserFile()
        {
            if (!IsUserFileAvailable())
                return;

            string json = JsonConvert.SerializeObject(userfile);
            File.WriteAllText(userfilename, json);
        }

        private bool IsUserFileAvailable()
        {
            return userfile != null && !string.IsNullOrWhiteSpace(userfilename);
        }

        /// <summary>
        /// adds an entry to the userfile.
        /// does nothing if userfile isn't established.
        /// TODO maybe should create the file
        /// does nothing if the player's steamid is already present.
        /// TODO should update last_seen
        /// does not add if steamid exists in other lists.
        /// </summary>
        /// <param name="player"></param>
        public void AddUserEntry(TF2BDPlayer player)
        {
            if (!IsUserFileAvailable())
                return;

            if (userfile.players.Any(p => p.steamid == player.steamid))
                return;//TODO update entry

            // don't duplicate what's in other lists.
            if (GetCheaterIDs().Contains(player.steamid))
                return;

            userfile.players.Add(player);
        }

        public void RemoveUserEntry(string steamid)
        {
            try
            {
                TF2BDPlayer player = userfile.players.First(p => p.steamid == steamid);
                _ = userfile.players.Remove(player);
            }
            catch
            {

            }
        }

        //TODO GetSuspiciousIDs() and use it to offer kicks that aren't confirmed already.

        public IEnumerable<string> GetCheaterIDs()
        {
            return files.SelectMany(f => GetCheaterIDs(f));
        }

        public IEnumerable<string> GetUserCheaterIDs()
        {
            return GetCheaterIDs(userfile);
        }

        private IEnumerable<string> GetCheaterIDs(TF2BD file)
        {
            if (file.players == null) 
                return new List<string>();

            IEnumerable<TF2BDPlayer> cheaters = file.players.Where(p => p.IsCheater);

            return cheaters.Select(p => p.steamid);
        }
    }

    //"$schema": "https://raw.githubusercontent.com/PazerOP/tf2_bot_detector/master/schemas/v3/playerlist.schema.json",
    public class TF2BD
    {
        //"file_info": {
        //"authors": [
        //  "roto"
        //],
        //"description": "@trusted meta list",
        //"title": "@trusted",
        //"update_url": "https://trusted.roto.lol/v1/steamids"
        //},
        //public TF2BDFileInfo file_info;

        public List<TF2BDPlayer> players;

        public int version;
    }

    public class TF2BDPlayer
    {
        public const string CHEATER = "cheater";
        public string steamid;
        
        public List<string> attributes;
        
        public bool IsCheater => attributes?.Contains(CHEATER) ?? false;

        public TF2BDLastSeen last_seen;

        //  "proof": []
        //List<string> proof;// string?
    }

    public class TF2BDLastSeen
    {
        public string player_name;
        
        //    "time": 1714190497
        public long time;
    }
}
