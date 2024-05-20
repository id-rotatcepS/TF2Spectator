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

        public void AddFile(string filename)
        {
            string json = File.ReadAllText(filename);
            files.Add(JsonConvert.DeserializeObject<TF2BD>(json));
        }

        public IEnumerable<string> GetCheaterIDs()
        {
            return files.SelectMany(f => GetCheaterIDs(f));
        }

        public void AddURLFile(string url)
        {
            string name = "players." + files.Count + ".json";
            string filepath = Path.Combine(Path.GetTempPath(), name);

            using (HttpClient client = new HttpClient())
                using (var s = client.GetStreamAsync(url))
                    using (FileStream fs = new FileStream(filepath, FileMode.OpenOrCreate))
                        s.Result.CopyTo(fs);

            AddFile(filepath);
        }

        private IEnumerable<string> GetCheaterIDs(TF2BD file)
        {
            if (file.players == null) 
                return new List<string>();

            IEnumerable<TF2BDPlayer> cheaters = file.players.Where(p => p.IsCheater);

            return cheaters.Select(p => p.steamid);
        }
    }

    //{
    //"file_info": {
    //"authors": [
    //  "roto"
    //],
    //"description": "@trusted meta list",
    //"title": "@trusted",
    //"update_url": "https://trusted.roto.lol/v1/steamids"
    //},
    //"$schema": "https://raw.githubusercontent.com/PazerOP/tf2_bot_detector/master/schemas/v3/playerlist.schema.json",
    //"players": [
    //],
    //"version": 0
    public class TF2BD
    {

        //public TF2BDFileInfo file_info;
        public List<TF2BDPlayer> players;
        public int version;
    }
    // {
    //  "steamid": "76561199637126796",
    //  "attributes": [
    //    "cheater"
    //  ],
    //  "last_seen": {
    //    "player_name": "MegaAntiCheat Hitman 29",
    //    "time": 1714190497
    //  },
    //  "proof": []
    //},         
    public class TF2BDPlayer
    {
        public string steamid;
        public List<string> attributes;
        public bool IsCheater => attributes?.Contains("cheater") ?? false;
        //TF2BDLastSeen last_seen;
        //List<string> proof;// string?
    }

}
