using System;

namespace Beef {
    /// <summary>
    /// Keeps track of settings for the config file.  This class needs to exactly match the Config.json.example file so that it can be
    /// deserialized into it.  So if you rename or change parameters here, you must upgrade the config file format as well.
    /// </summary>
    public class BeefConfig {
        // This is the current version and should always be incremented when changing the config file format.
        public static int ReaderConfigVersion = 1;  

        // Version
        public int Version { get; set; } = ReaderConfigVersion;          // This identifies the version of the config file.

        // Discord stuff
        public String DiscordBotToken { get; set; } = "";
        public String BotPrefix { get; set; } = ".";
        public String[] LeaderRoles { get; set; } = new String[] { "ExampleRole1", "ExampleRole2" };

        // Presentation Stuff
        public String GoogleApiPresentationId { get; set; } = "";
        public String GoogleApiCredentialFile { get; set; } = "credentials.json";
        public String GoogleApiApplicationName { get; set; } = "";
        public String BeefLadderLink { get; set; } = ""; // This is separate from the presentation ID because you need the readonly link.

        public ReaderConfig MmrReaderConfig { get; set; } = ReaderConfig.CreateDefault();

        /// <summary>
        /// Creates a ReaderConfig with default settings.
        /// </summary>
        /// <returns>Returns the created config.</returns>
        public static BeefConfig CreateDefault() {
            // Fill out the default settings and version
            BeefConfig config = new BeefConfig();
            
            // The credentials are left blank and need to be filled out after
            return config;
        }
    }

    // Keep track of old config versions in case we want to be
    //    able to update old versions in a smarter way.
    #region OldConfigVersions

    #endregion
}
