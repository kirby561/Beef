using System;

namespace Beef {
    /// <summary>
    /// Keeps track of settings for the config file.  This class needs to exactly match the Config.json.example file so that it can be
    /// deserialized into it.  So if you rename or change parameters here, you must upgrade the config file format as well.
    /// </summary>
    public class BeefConfig {
        // This is the current version and should always be incremented when changing the config file format.
        public static int BeefConfigVersion = 5;  

        // Version
        public int Version { get; set; } = BeefConfigVersion;          // This identifies the version of the config file.

        // Discord stuff
        public String DiscordBotToken { get; set; } = "";
        public String BotPrefix { get; set; } = ".";
        public String BeefCommand { get; set; } = "beef";
        public String[] LeaderRoles { get; set; } = new String[] { "ExampleRole1", "ExampleRole2" };
        public String[] DynamicChannels { get; set; } = new String[] { "Teams" }; // Names of channels that there should always be exactly 1 empty of

        // Ladder Link
        public String BeefLadderLink { get; set; } = "";  // This is the ladders that can be viewed by users.

        // Config for reading MMRs using the battle.net API
        public ReaderConfig MmrReaderConfig { get; set; } = ReaderConfig.CreateDefault();

        // Config for interacting with Twitch
        public TwitchConfig TwitchConfig {get; set; } = new TwitchConfig();

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
    /// <summary>
    /// Keeps track of settings for the config file.  This class needs to exactly match the Config.json.example file so that it can be
    /// deserialized into it.  So if you rename or change parameters here, you must upgrade the config file format as well.
    /// </summary>
    /// 
    public class BeefConfigV4 {
        // This is the current version and should always be incremented when changing the config file format.
        public static int BeefConfigVersion = 4;  

        // Version
        public int Version { get; set; } = BeefConfigVersion;          // This identifies the version of the config file.

        // Discord stuff
        public String DiscordBotToken { get; set; } = "";
        public String BotPrefix { get; set; } = ".";
        public String BeefCommand { get; set; } = "beef";
        public String[] LeaderRoles { get; set; } = new String[] { "ExampleRole1", "ExampleRole2" };
        public String[] DynamicChannels { get; set; } = new String[] { "Teams" }; // Names of channels that there should always be exactly 1 empty of

        // Presentation Stuff
        public String GoogleApiCredentialFile { get; set; } = "credentials.json";
        public String GoogleApiApplicationName { get; set; } = "";
        public String GoogleApiPresentationId { get; set; } = ""; // This is the Google doc IDs of the presentation.
        public String BeefLadderLink { get; set; } = "";  // This is the ladders that can be viewed by users.

        // Config for reading MMRs using the battle.net API
        public ReaderConfig MmrReaderConfig { get; set; } = ReaderConfig.CreateDefault();

        // Config for interacting with Twitch
        public TwitchConfig TwitchConfig {get; set; } = new TwitchConfig();

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

    public class BeefConfigV3 {
        // This is the current version and should always be incremented when changing the config file format.
        public static int BeefConfigVersion = 3;  

        // Version
        public int Version { get; set; } = BeefConfigVersion;          // This identifies the version of the config file.

        // Discord stuff
        public String DiscordBotToken { get; set; } = "";
        public String BotPrefix { get; set; } = ".";
        public String BeefCommand { get; set; } = "beef";
        public String[] LeaderRoles { get; set; } = new String[] { "ExampleRole1", "ExampleRole2" };
        public String[] DynamicChannels { get; set; } = new String[] { "Teams" }; // Names of channels that there should always be exactly 1 empty of

        // Presentation Stuff
        public String GoogleApiCredentialFile { get; set; } = "credentials.json";
        public String GoogleApiApplicationName { get; set; } = "";
        public String GoogleApiPresentationId { get; set; } = ""; // This is the Google doc IDs of the presentation.
        public String BeefLadderLink { get; set; } = "";  // This is the ladders that can be viewed by users.

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

    /// <summary>
    /// Keeps track of settings for the config file.  This class needs to exactly match the Config.json.example file so that it can be
    /// deserialized into it.  So if you rename or change parameters here, you must upgrade the config file format as well.
    /// </summary>
    public class BeefConfigV2 {
        // This is the current version and should always be incremented when changing the config file format.
        public static int BeefConfigVersion = 2;

        // Version
        public int Version { get; set; } = BeefConfigVersion;          // This identifies the version of the config file.

        // Discord stuff
        public String DiscordBotToken { get; set; } = "";
        public String BotPrefix { get; set; } = ".";
        public String BeefCommand { get; set; } = "beef";
        public String[] LeaderRoles { get; set; } = new String[] { "ExampleRole1", "ExampleRole2" };

        // Presentation Stuff
        public String GoogleApiCredentialFile { get; set; } = "credentials.json";
        public String GoogleApiApplicationName { get; set; } = "";
        public String GoogleApiPresentationId { get; set; } = ""; // This is the Google doc IDs of the presentation.
        public String BeefLadderLink { get; set; } = "";  // This is the ladders that can be viewed by users.

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

    /// <summary>
    /// Keeps track of settings for the config file.  This class needs to exactly match the Config.json.example file so that it can be
    /// deserialized into it.  So if you rename or change parameters here, you must upgrade the config file format as well.
    /// </summary>
    public class BeefConfigV1 {
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

    #endregion
}
