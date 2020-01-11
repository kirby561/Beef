using System;

namespace Beef {
    /// <summary>
    /// Keeps track of settings for the config file.  This class needs to exactly match the Config.json.example file so that it can be
    /// deserialized into it.  So if you rename or change parameters here, you must upgrade the config file format as well.
    /// </summary>
    public class ReaderConfig {
        // This is the current version and should always be incremented when changing the config file format.
        public static int ReaderConfigVersion = 1;

        // Version
        public int Version { get; set; } = 0;          // This identifies the version of the config file.  If it's not set it will be at 0.

        // Config settings:
        public long MsPerRead { get; set; }            // How many milliseconds inbetween reads
        public String DataDirectory { get; set; }      // Where to store cached files

        // Information about who is connecting:
        public String ClientId { get; set; }       // Get this by making a developer account for the Blizzard API.
        public String ClientSecret { get; set; }   // Same as above.

        /// <summary>
        /// Creates a ReaderConfig with default settings.
        /// </summary>
        /// <returns>Returns the created config.</returns>
        public static ReaderConfig CreateDefault() {
            // Fill out the default settings and version
            ReaderConfig config = new ReaderConfig();
            config.Version = ReaderConfigVersion;
            config.MsPerRead = 50000;
            config.DataDirectory = "";

            return config;
        }
    }
}
