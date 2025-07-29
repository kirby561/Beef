using Beef.SharedServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Beef.TwitchManager {
    /// <summary>
    /// Configured by a TwitchConfig. Polls twitch at the specified interval
    /// to check for players going online.
    /// </summary>
    public class TwitchPollingService : PollingService {
        private const String TwitchNameVariable = "{TwitchName}";
        private const String StreamLinkVariable = "{StreamLink}";
        private const String GameVariable = "{Game}";
        private const String StreamTitleVariable = "{StreamTitle}";
        private const String DefaultGoLiveMessage = "Hey everyone, " + TwitchNameVariable + " is live on Twitch!\n\n**{StreamTitle}**\n{Game}\n" + StreamLinkVariable;
        private const String TwitchStreamPrefix = "https://www.twitch.tv/";

        private Dictionary<String, StreamInfo> _monitoredStreams; // Maps Twitch Stream Name to StreamInfo
        private String _streamsDirectory; // The directory with StreamInfo files to monitor
        private String _accessCacheFile;
        private TwitchConfig _configuration;
        private TwitchLiveListener _twitchLiveListener;

        public TwitchPollingService(TwitchConfig config, String dataDirectory, TwitchLiveListener listener) : base("TwitchPollingService", config.MsPerPoll) {
            _streamsDirectory = dataDirectory + "/MonitoredStreams";
            _accessCacheFile = dataDirectory + "/TwitchAccessToken.tmp";
            _configuration = config;
            _twitchLiveListener = listener;

            InitializeStreamMonitor();
        }

        public TwitchConfig GetConfiguration() {
            return _configuration;
        }

        public List<StreamInfo> GetMonitoredStreams() {
            List<StreamInfo> pollingList;

            // Lock/copy in case there are streams being added/removed while this is running.
            lock (_lock) {
                pollingList = _monitoredStreams.Values.ToList();
            }

            return pollingList;
        }

        protected override void PollingAction() {
            List<StreamInfo> pollingList = GetMonitoredStreams();

            foreach (StreamInfo stream in pollingList) {
                String accessToken = GetAccessToken();
                if (String.IsNullOrEmpty(accessToken)) {
                    Console.WriteLine("TwitchPollingService: Unable to get an access token.");
                    break; // Try again later
                }

                UpdateLiveStatus(stream, accessToken);
            }
        }

        /// <summary>
        /// Escapes all the underscores in the given name. There are other special characters but only underscores
        /// are allowed in twitch URLs and thus twitch names.
        /// </summary>
        /// <param name="name">The name to escape</param>
        /// <returns>The same name with all the _ escaped.</returns>
        protected String EscapeUnderscores(String name) {
            return name.Replace("_", "\\_");
        }

        /// <summary>
        /// Checks and updates the live status of the given stream using the given access token for authentication.
        /// </summary>
        /// <param name="stream">The stream to check.</param>
        /// <param name="accessToken">The access token to use for authentication.</param>
        protected void UpdateLiveStatus(StreamInfo stream, String accessToken) {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Add("Client-ID", _configuration.ClientId); 
            String url = "https://api.twitch.tv/helix/streams?user_login=" + stream.GetTwitchUsername();
            Task<HttpResponseMessage> responseTask = httpClient.GetAsync(url);
            bool succeeded = true;
            try {
                responseTask.Wait();
            } catch (Exception ex) {
                succeeded = false;
                Console.WriteLine("An exception was thrown getting a Twitch user's live status: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("\tInnerException: " + ex.InnerException.Message);
                }
            }

            if (succeeded) {
                HttpResponseMessage result = responseTask.Result;
                Task<string> resultString = result.Content.ReadAsStringAsync();
                try {
                    resultString.Wait();
                } catch (Exception ex) {
                    Console.WriteLine("Exception reading Twitch user's live status content: " + ex.Message);
                }

                try {               
                    TwitchHelixStreamsResponse response = JsonConvert.DeserializeObject<TwitchHelixStreamsResponse>(resultString.Result);                            
                    foreach (TwitchHelixStreamsDataEntry entry in response.data) {
                        if (stream.GetTwitchUsername().ToLower().Equals(entry.user_login)) {
                            // The user is live, check if they were live before
                            String startedAt = entry.started_at;
                            long timestampMs = ConvertDateTimeToUnixTimestampMs(DateTime.Parse(startedAt).ToUniversalTime());
                            if (timestampMs != stream.LastLiveTimestamp) {
                                // The user just went live
                                stream.LastLiveTimestamp = timestampMs;
                                lock (_lock) {
                                    // ?? TODO: This should probably be stored separately from the file itself
                                    // so it can be completely independent of registrations.
                                    UpdateStreamInfoFile(stream);
                                }

                                // Use the reported name so the capitalization matches.
                                // If it's not provided, just use the twitch username.
                                String name = entry.user_name;
                                if (String.IsNullOrWhiteSpace(name))
                                    name = stream.GetTwitchUsername();
                                
                                if (_twitchLiveListener != null) {
                                    // Fill in the variables for the go live message
                                    String goLiveMessage = stream.GoLiveMessage
                                        .Replace(TwitchNameVariable, EscapeUnderscores(name))
                                        .Replace(StreamLinkVariable, stream.StreamUrl)
                                        .Replace(GameVariable, entry.game_name)
                                        .Replace(StreamTitleVariable, entry.title);
                                    _twitchLiveListener.OnTwitchStreamLive(stream.GetTwitchUsername(), goLiveMessage);
                                }
                            }
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Exception deserializing authorization token content: " + ex.Message);
                }
            } else {
                Console.WriteLine("Could not request the live stream information for " + stream.StreamUrl);
            }
        }

        /// <summary>
        /// Loads all the people's streams that are registered for monitoring
        /// </summary>
        protected void InitializeStreamMonitor() {
            _monitoredStreams = new Dictionary<String, StreamInfo>();

            // Make sure the streams directory exists
            Directory.CreateDirectory(_streamsDirectory);

            // Read all the existing streams that should be monitored
            foreach (String filePath in Directory.EnumerateFiles(_streamsDirectory)) {
                try {
                    String fileContents = File.ReadAllText(filePath);
                    if (!String.IsNullOrEmpty(fileContents)) {
                        StreamInfo configFile = null;
                        try {
                            configFile = JsonConvert.DeserializeObject<StreamInfo>(fileContents);

                            // Do some sanity checking
                            if (configFile == null) {
                                Console.WriteLine("Invalid StreamInfo contents for: " + filePath);
                                continue;
                            }

                            _monitoredStreams.Add(configFile.GetTwitchUsername(), configFile);
                        } catch (Exception ex) {
                            Console.WriteLine("Could not deserialize the StreamInfo file.  Is the format correct?");
                            Console.WriteLine("Exception: " + ex.Message);
                            if (ex.InnerException != null) {
                                Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                            }
                        }
                    } else {
                        Console.WriteLine("Invalid StreamInfo file, it shouldnt be empty: " + filePath);
                    }
                } catch (IOException ex) {
                    Console.WriteLine("Unable to open the config file at " + filePath);
                    Console.WriteLine("Does the file exist?");
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Starts monitoring the given stream URL and associates it with the given stream URL.
        /// The "go live" message will be the default one.
        /// </summary>
        /// <param name="streamUrl">The Twitch URL to monitor.</param>
        /// <returns>Returns Success or a code describing the error.</returns>
        public ErrorCode MonitorStream(String streamUrl) {
            return MonitorStream(streamUrl, DefaultGoLiveMessage);
        }

        /// <summary>
        /// Starts monitoring the given stream URL and associates it with the given stream URL.
        /// When the user goes live, it will send the given "go live" message.
        /// </summary>
        /// <param name="streamUrl">The Twitch URL to monitor.</param>
        /// <returns>Returns Success or a code describing the error.</returns>
        public ErrorCode MonitorStream(String streamUrl, String goLiveMessage) {
            if (!IsValidTwitchStreamUrl(streamUrl))
                return ErrorCode.InvalidTwitchUrl;

            StreamInfo info = new StreamInfo(streamUrl, goLiveMessage);
            String twitchName = info.GetTwitchUsername();
            // Lock because this could be on the request thread rather than the polling thread.
            lock (_lock) {
                if (_monitoredStreams.ContainsKey(twitchName)) {
                    // If we already have this stream monitored, update the name
                    _monitoredStreams[twitchName] = info;
                } else {
                    _monitoredStreams.Add(twitchName, info);
                }
            }
            return UpdateStreamInfoFile(info);
        }

        /// <summary>
        /// Stops monitoring the Twitch stream associated with the given stream URL.
        /// </summary>
        /// <param name="streamUrl">The Twitch stream URL to stop monitoring.</param>
        /// <returns>Returns Success or an error code identifying what went wrong.</returns>
        public ErrorCode StopMonitoringStream(String streamUrl) {
            String twitchName = GetTwitchUsernameFromUrl(streamUrl);
            if (String.IsNullOrWhiteSpace(twitchName))
                return ErrorCode.InvalidTwitchUrl;

            if (_monitoredStreams.ContainsKey(twitchName)) {
                StreamInfo info = _monitoredStreams[twitchName];
                lock (_lock) {                    
                    _monitoredStreams.Remove(twitchName);
                }
                File.Delete(GetStreamInfoPath(info));
                return ErrorCode.Success;
            }
            return ErrorCode.TwitchUserDoesNotExist;
        }
        
        /// <summary>
        /// Updates the stream info file for the given stream name.
        /// If the twitch name doesn't exist an error is returned.
        /// </summary>
        /// <param name="twitchName">The name of the stream to update.</param>
        /// <returns>Returns Success or an error code indicating what went wrong.</returns>
        protected ErrorCode UpdateStreamInfoFile(String twitchName) {
            if (_monitoredStreams.ContainsKey(twitchName)) {
                return UpdateStreamInfoFile(_monitoredStreams[twitchName]);
            }
            return ErrorCode.TwitchUserDoesNotExist;
        }

        /// <summary>
        /// Updates the stream info file for the given stream.
        /// If it doesn't exist it will create it. If it does exist it will write
        /// the new information. The file name is based off the twitch username in the given StreamInfo.
        /// </summary>
        /// <param name="info">The stream information to write.</param>
        /// <returns>Returns Success or an error code indicating what went wrong.</returns>
        protected ErrorCode UpdateStreamInfoFile(StreamInfo info) {
            String streamFilePath = GetStreamInfoPath(info);
            if (File.Exists(streamFilePath))
                File.Delete(streamFilePath);

            String configString = null;
            try {
                configString = JsonConvert.SerializeObject(info);
            } catch (Exception ex) {
                Console.WriteLine("Could not serialize the given config.");
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                return ErrorCode.TwitchFileCouldNotSerialize;
            }

            configString = JsonUtil.PoorMansJsonFormat(configString);

            // Write it to a file
            try {
                File.WriteAllText(streamFilePath, configString);
            } catch (Exception ex) {
                Console.WriteLine("Could not write the StreamInfo file to " + streamFilePath);
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                return ErrorCode.BeefFileCouldNotWriteFile;
            }

            return ErrorCode.Success;
        }

        protected String GetStreamInfoPath(StreamInfo file) {
            return _streamsDirectory + "/" + file.GetTwitchUsername() + ".json";
        }
        
        /// <returns>Requests and returns an access token to use for HTTP requests to the API.</returns>
        protected String GetAccessToken() {
            return GetOAuthAccessToken("https://id.twitch.tv/oauth2/token", _configuration.ClientId, _configuration.ClientSecret, _accessCacheFile);
        }

        /// <summary>
        /// Gets the twitch username from a stream url in the form:
        ///     https://www.twitch.tv/[username]
        /// </summary>
        /// <param name="streamUrl">A URL of the form in the summary.</param>
        /// <returns>Returns the [username] from the url in the summary or an empty string if the URL is incorrect. Note this does not necessarily mean the Twitch URL is invalid, use IsValidTwitchStreamUrl for that.</returns>
        protected static String GetTwitchUsernameFromUrl(String streamUrl) {
            if (String.IsNullOrWhiteSpace(streamUrl))
                return "";

            // Make su
            int indexOfTwitchPrefix = streamUrl.IndexOf(TwitchStreamPrefix);
            if (indexOfTwitchPrefix < 0)
                return "";

            return streamUrl.Substring(indexOfTwitchPrefix + TwitchStreamPrefix.Length);
        }

        /// <summary>
        /// Indicates if the given stream URL is a valid twitch stream URL.
        /// </summary>
        /// <param name="streamUrl">The stream URL to check</param>
        /// <returns>Returns true if it is a valid stream URL</returns>
        protected static bool IsValidTwitchStreamUrl(String streamUrl) {
            // Not whitespace or null
            if (String.IsNullOrWhiteSpace(streamUrl))
                return false;

            // Starts with twitch prefix
            if (!streamUrl.StartsWith(TwitchStreamPrefix))
                return false;

            // Has content after the prefix
            if (streamUrl.Length <= TwitchStreamPrefix.Length)
                return false; // No name after

            String twitchName = GetTwitchUsernameFromUrl(streamUrl);

            // No forward slashes in name
            if (twitchName.Contains('/'))
                return false;

            // Passed all our checks, it should be valid
            return true;
        }

        public class StreamInfo {
            public String StreamUrl {get; set; }
            public String GoLiveMessage { get; set; }
            public long LastLiveTimestamp { get; set; } = -1;

            // Constructor for deserialization
            public StreamInfo() { }

            public StreamInfo(String streamUrl, String goLiveMessage) {
                StreamUrl = streamUrl;
                GoLiveMessage = goLiveMessage;
            }

            public String GetTwitchUsername() {
                return GetTwitchUsernameFromUrl(StreamUrl);
            }
        }
    }
}
