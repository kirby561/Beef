﻿using System;

namespace Beef {
    /// <summary>
    /// Keeps track of information for each user on the Beef ladder.
    /// It is persisted to disk in the BeefUsers folder.
    /// </summary>
    class BeefUserConfig {
        // This is the current version and should always be incremented when changing the config file format.
        public static int BeefUserVersion = 0;

        // This is the persisted version so we know if it's not the latest when we start up.
        public int Version = BeefUserVersion;

        // User information
        public String BeefName = ""; // This is the name that is displayed on the Beef ladder. It must be unique on the ladder.
        public String DiscordName = ""; // This is the discord name including the #1234 tag.

        public BeefUserConfig(String beefName, String discordName) {
            BeefName = beefName;
            DiscordName = discordName;
        }

        public BeefUserConfig() {
            // This is so we can deserialize from JSON.
        }
    }
}