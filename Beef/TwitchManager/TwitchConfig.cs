using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beef {
    /// <summary>
    /// Keeps track of settings for Twitch integration features.
    /// This class needs to exactly match "TwitchConfig" in Config.json.example.
    /// 
    /// Currently Twitch integration just checks if people are live and can message the Discord when they go online.
    /// </summary>
    public class TwitchConfig {
        // This is the current version and should always be incremented when changing the config file format.
        public static int TwitchConfigVersion = 1;

        // Version
        public int Version { get; set; } = TwitchConfigVersion;

        // Config settings:
        public long MsPerPoll { get; set; } = 6000; // The number of milliseconds between checking if people are live
        public String GoLiveChannel = "#stream-links"; // The name of the channel to post "go-live" notifications in.

        // Twitch config settings. To get these, make an application on dev.twitch.tv associated with a Twitch account.
        public String ClientId { get; set; } // The ID of the twitch application to use as permission to get an auth token.
        public String ClientSecret { get; set; } // The client secret for the application.

        /// <summary>
        /// Creates a TwitchConfig with default settings.
        /// </summary>
        /// <returns>Returns the created config.</returns>
        public static TwitchConfig CreateDefault() {
            // Fill out the default settings and version
            TwitchConfig config = new TwitchConfig();
            
            // The credentials are left blank and need to be filled out after
            return config;
        }
    }
}
