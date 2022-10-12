using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beef.TwitchManager {
    /// <summary>
    /// A model of the JSON response from "https://api.twitch.tv/helix/streams?user_login=" of the form:
    /// The response should look like the following:  
    ///  {
    ///    "data": [
    ///        {
    ///            "id": "39932210728",
    ///            "user_id": "21635116",
    ///            "user_login": "heromarine",
    ///            "user_name": "HeroMarine",
    ///            "game_id": "490422",
    ///            "game_name": "StarCraft II",
    ///            "type": "live",
    ///            "title": "ESL Open Cup #144",
    ///            "viewer_count": 1054,
    ///            "started_at": "2022-10-10T16:50:13Z",
    ///            "language": "en",
    ///            "thumbnail_url": "https://static-cdn.jtvnw.net/previews-ttv/live_user_heromarine-{width}x{height}.jpg",
    ///            "tag_ids": [
    ///                "6ea6bca4-4712-4ab9-a906-e3336a9d8039"
    ///            ],
    ///            "is_mature": false
    ///        }
    ///    ],
    ///    "pagination": {}
    ///  }
    /// </summary>
    public class TwitchHelixStreamsResponse {
        public TwitchHelixStreamsDataEntry[] data { get; set; }
        public object pagination { get; set; }
    }

    public class TwitchHelixStreamsDataEntry {
        public String id { get; set; }
        public String user_id { get; set; }
        public String user_login { get; set; }
        public String user_name { get; set; }
        public String game_id { get; set; }
        public String game_name { get; set; }
        public String type { get; set; }
        public String title { get; set; }
        public long viewer_count { get; set; }
        public String started_at { get; set; }
        public String language { get; set; }
        public String thumbnail_url { get; set; }
        public String[] tag_ids { get; set; }
        public String is_mature { get; set; }
    }
}
