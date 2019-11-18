using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Beef.MmrReader {
    /// <summary>
    /// Reads the MMR of the given profile every "MsPerRead" milliseconds.
    /// </summary>
    public class MmrReader {
        /// <summary>
        /// Data class to keep track of the access token we were granted and when it
        /// expires.  This class is serialized to and from JSON directly.
        /// </summary>
        public class AccessTokenInfo {
            public String AccessToken { get; set; }     // The access token
            public long ExpirationTimeMs { get; set; }  // Unix timestamp when this token expires
        }

        // Constants
        public const long TicksPerMs = 10000;

        // Maps regions to region IDs that we use on the endpoint.
        public Dictionary<String, int> _regionIdMap = new Dictionary<String, int>();

        // Reader state
        private Thread _mmrReadThread;
        private bool _shouldReadMmr = true;
        private bool _isThreadRunning = false;
        private object _lock = new object();
        private HttpClient _httpClient = new HttpClient();
        private JavaScriptSerializer _jsonSerializer = new JavaScriptSerializer();
        private String _accessCacheFile;

        // Interface
        private ProfileInfoProvider _profileInfoProvider;
        private MmrListener _listener;

        // Reader configuration
        private ReaderConfig _configuration;

        /// <summary>
        /// Creates an MmrReader using the given configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.  See ReaderConfig for details.</param>
        public MmrReader(ReaderConfig configuration) {
            _configuration = configuration;
            _accessCacheFile = _configuration.DataDirectory + "/Access.tmp";
            InitRegionIdMap();
        }

        /// <summary>
        /// Initializes a map that maps country codes to their corresponding IDs as defined by the Blizzard API docs.
        /// </summary>
        private void InitRegionIdMap() {
            // From the Blizzard API docs:  (1=US, 2=EU, 3=KO and TW, 5=CN)
            _regionIdMap.Add("US", 1);
            _regionIdMap.Add("EU", 2);
            _regionIdMap.Add("KO", 3);
            _regionIdMap.Add("CN", 5);
        }

        /// <returns>Returns a unix timestamp (ms since the epoch)</returns>
        private long GetNowInMs() {
            return DateTime.Now.ToUniversalTime().Subtract(
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                ).Ticks / TicksPerMs;
        }

        /// <summary>
        /// Gets the time to next refresh.
        /// </summary>
        /// <param name="nextRefreshTimeMs">The unix time of the next refresh in milliseconds.</param>
        /// <returns>Returns the number of milliseconds until the next refresh or 0 if it should refresh now.</returns>
        private long GetTimeToNextRefresh(long nextRefreshTimeMs) {
            long time = nextRefreshTimeMs - GetNowInMs();
            if (time < 0)
                time = 0;
            return time;
        }

        private LadderInfo GetBestLadderInfoFor(ProfileInfo profileInfo) {
            if (profileInfo == null || profileInfo.RegionId == null)
                return null;

            // Check that our authorization token is up to date
            String accessToken = GetAccessToken();
            
            String url = "https://us.api.blizzard.com/sc2/profile/";
            url += _regionIdMap[profileInfo.RegionId] + "/";
            url += profileInfo.RealmId + "/";
            url += profileInfo.ProfileId + "/";
            url += "/ladder/summary?locale=en_US";
            url += "&access_token=" + accessToken;

            // Make the request
            Task<string> responseTask = _httpClient.GetStringAsync(url);
            bool succeeded = true;
            try {
                responseTask.Wait();
            } catch (Exception ex) {
                succeeded = false;
                Console.WriteLine("An exception was thrown polling the endpoint: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("\tInnerException: " + ex.InnerException.Message);
                }

                // Since we had an exception, try refreshing the Auth token as well.
                try {
                    File.Delete(_accessCacheFile);
                } catch (Exception deleteEx) {
                    Console.WriteLine("After a response error, could not delete the access cache file: " + deleteEx.Message);
                }
            }

            if (succeeded) {
                // The Blizzard API returns a JSON string that contains  
                //    ladder information about the above profile.
                string jsonResponse = responseTask.Result;

                // We expect the response to have a "allLadderMemberships" property with each ladder inside
                try {
                    dynamic ladderData = _jsonSerializer.Deserialize<dynamic>(jsonResponse);
                    dynamic allLadderMemberships = ladderData["allLadderMemberships"];

                    int maxMmrSoFar = -1;
                    LadderInfo bestLadder = null;
                    for (int i = 0; i < allLadderMemberships.Length; i++) {
                        dynamic ladderEntry = allLadderMemberships[i];
                        String ladderIdStr = ladderEntry["ladderId"];
                        String gameMode = ladderEntry["localizedGameMode"];

                        long ladderId;
                        if (long.TryParse(ladderIdStr, out ladderId) && gameMode.StartsWith("1v1")) {
                            Tuple<String, String> mmrAndRace = GetMmrInfoFor(profileInfo.RegionId, profileInfo.RealmId, profileInfo.ProfileId, ladderId);
                            String mmrStr = mmrAndRace.Item1;
                            String race = mmrAndRace.Item2;
                            int mmr;
                            if (mmrStr != null && int.TryParse(mmrStr, out mmr)) {
                                if (mmr > maxMmrSoFar) {
                                    maxMmrSoFar = mmr;
                                    bestLadder = new LadderInfo();
                                    bestLadder.LadderId = ladderId;
                                    bestLadder.Mmr = mmrStr;
                                    bestLadder.League = gameMode.Replace("1v1", "").Trim();
                                    bestLadder.Race = race[0].ToString().ToUpper() + race.Substring(1);
                                }
                            }
                        }
                    }
                    
                    return bestLadder;
                } catch (Exception e) {
                    Console.WriteLine("An error occurred extracting the MMR from the API response.  Exception: " + e.Message);
                }
            }
            
            return null;
        }

        /// <summary>
        /// Polls the Blizzard API for the latest MMR and returns it or null if there was an error
        /// If the access token is out of date it is updated as well first.
        /// </summary>
        /// <param name="regionId">Region ID of the account.</param>
        /// <param name="realmId">Realm ID of the account.</param>
        /// <param name="profileId">Profile ID of the account.</param>
        /// <param name="ladderId">Ladder ID to get the MMr for.</param>
        /// <returns>Returns a tuple. The first is the MMR and the second is the race corresponding to that MMR.  null is returned if there was an error.  The race will be "Unknown" if it couldn't be found.</returns>
        private Tuple<String, String> GetMmrInfoFor(String regionId, long realmId, long profileId, long ladderId) {
            // Check that our authorization token is up to date
            String accessToken = GetAccessToken();

            // Build the URL
            String url = "https://us.api.blizzard.com/sc2/profile/";
            url += _regionIdMap[regionId] + "/";
            url += realmId + "/";
            url += profileId + "/";
            url += "ladder/";
            url += ladderId;
            url += "?locale=en_US";
            url += "&access_token=" + accessToken;

            // Make the request
            Task<string> responseTask = _httpClient.GetStringAsync(url);
            bool succeeded = true;
            try {
                responseTask.Wait();
            } catch (Exception ex) {
                succeeded = false;
                Console.WriteLine("An exception was thrown polling the endpoint: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("\tInnerException: " + ex.InnerException.Message);
                }

                // Since we had an exception, try refreshing the Auth token as well.
                try {
                    File.Delete(_accessCacheFile);
                } catch (Exception deleteEx) {
                    Console.WriteLine("After a response error, could not delete the access cache file: " + deleteEx.Message);
                }
            }

            if (succeeded) {
                // The Blizzard API returns a JSON string that contains  
                //    ladder information about the above profile.
                string jsonResponse = responseTask.Result;
                String mmrResult = null;
                String raceResult = "Unknown";

                // We expect the response to have a "rankedAndPools.mmr" property with our MMR.
                try {
                    dynamic ladderData = _jsonSerializer.Deserialize<dynamic>(jsonResponse);
                    long mmr = (long)Math.Round((double)ladderData["ranksAndPools"][0]["mmr"]);

                    // Convert to string
                    mmrResult = mmr.ToString();

                    // We also want to get the race from the ladder listing.
                    // ?? TODO: This should maybe be done after we know the MMR so we don't search for the race
                    // for MMRs we don't care about
                    String profileIdStr = "" + profileId;
                    dynamic ladderTeams = ladderData["ladderTeams"];
                    String firstErrorMessage = null;
                    for (int i = 0; i < ladderTeams.Length; i++) {
                        try {
                            dynamic team = ladderTeams[i];
                            dynamic teamMembers = team["teamMembers"];
                            dynamic firstTeamMember = teamMembers[0];
                            String id = firstTeamMember["id"];
                            if (id == profileIdStr) {
                                // Found it.
                                raceResult = firstTeamMember["favoriteRace"];
                                break;
                            }
                        } catch (Exception ex) {
                            // Rather than spew logs when this fails, just report the first failure
                            if (firstErrorMessage == null) {
                                firstErrorMessage = ex.Message;
                            }
                        }

                        if (firstErrorMessage != null) {
                            Console.WriteLine("One or more exceptions were thrown when enumerating the ladder entries.  First exception: " + firstErrorMessage);
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine("An error occurred extracting the MMR from the API response.  Exception: " + e.Message);
                }

                return new Tuple<String, String>(mmrResult, raceResult);
            }

            return null;
        }

        /// <summary>
        /// Checks if our access token is out of date. If it is, requests a new one.
        /// </summary>
        /// <returns>
        /// Returns the current access token or an empty string if we couldn't get one.
        /// </returns>
        private String GetAccessToken() {
            AccessTokenInfo accessTokenInfo = null;

            // Update from our cache file
            if (File.Exists(_accessCacheFile)) {
                String accessFileContents = File.ReadAllText(_accessCacheFile);

                try {
                    accessTokenInfo = _jsonSerializer.Deserialize<AccessTokenInfo>(accessFileContents);
                } catch (Exception serializerEx) {
                    Console.WriteLine("Access token cache file corrupted.  Removing and refreshing the token.  Exception: " + serializerEx.Message);
                    try {
                        File.Delete(_accessCacheFile);
                    } catch (Exception deleteEx) {
                        Console.WriteLine("Could not delete the access cache file: " + deleteEx.Message);
                    }
                }
            }

            // If we had a cached token, check if it's still valid
            if (accessTokenInfo != null) {
                long now = GetNowInMs();
                if (now < accessTokenInfo.ExpirationTimeMs) {
                    // Use this one
                    return accessTokenInfo.AccessToken;
                }
            }

            // Otherwise we need to get a new token from the server.
            //    Make the request
            var content = new Dictionary<string, string> {
                    {"grant_type", "client_credentials"},
                    {"client_id", _configuration.ClientId},
                    {"client_secret", _configuration.ClientSecret}
                };

            Task<HttpResponseMessage> responseTask = _httpClient.PostAsync("https://us.battle.net/oauth/token", new FormUrlEncodedContent(content));
            bool succeeded = true;
            try {
                responseTask.Wait();
            } catch (Exception ex) {
                succeeded = false;
                Console.WriteLine("An exception was thrown requesting an auth token: " + ex.Message);
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
                    Console.WriteLine("Exception reading authorization token content: " + ex.Message);
                }

                try {
                    dynamic response = _jsonSerializer.Deserialize<dynamic>(resultString.Result);
                    accessTokenInfo = new AccessTokenInfo();
                    accessTokenInfo.AccessToken = response["access_token"];

                    // The joke is that I have no idea what the unit of "expires_in" is.  It seems like it's in seconds
                    //    so I treat it like that but the docs don't say.  It doesn't matter a whole lot because
                    //    if it expired we will just request another token again.
                    accessTokenInfo.ExpirationTimeMs = 1000 * (long)response["expires_in"];

                    // Serialize to file
                    try {
                        String serializedTokenInfo = _jsonSerializer.Serialize(accessTokenInfo);
                        File.WriteAllText(_accessCacheFile, serializedTokenInfo);
                    } catch (Exception ex) {
                        Console.WriteLine("Failed to cache the access token.  Exception: " + ex.Message);
                    }

                    return accessTokenInfo.AccessToken;
                } catch (Exception ex) {
                    Console.WriteLine("Exception deserializing authorization token content: " + ex.Message);
                }
            }

            return String.Empty;
        }

        /// <summary>
        /// This is the entry point to the background MMR reading thread and just loops reading the MMR at the configured interval.
        /// </summary>
        private void ReadMmrLoop() {
            long nextRefreshTimeMs = GetNowInMs();
            while (_shouldReadMmr) {
                if (GetTimeToNextRefresh(nextRefreshTimeMs) == 0) {
                    // Set the next time
                    nextRefreshTimeMs = GetNowInMs() + _configuration.MsPerRead;

                    // Do the next refresh
                    List<Tuple<ProfileInfo, LadderInfo>> nextMmrList = new List<Tuple<ProfileInfo, LadderInfo>>();
                    List<ProfileInfo> users = _profileInfoProvider.GetLadderUsers();
                    foreach (ProfileInfo user in users) {
                        LadderInfo ladderInfo = GetBestLadderInfoFor(user);
                        nextMmrList.Add(new Tuple<ProfileInfo, LadderInfo>(user, ladderInfo));
                    }

                    _listener.OnMmrRead(nextMmrList);
                }

                int timeToNextRefresh = (int)GetTimeToNextRefresh(nextRefreshTimeMs);
                lock (_lock) {
                    Monitor.Wait(_lock, timeToNextRefresh);
                }
            }

            // Exiting
            lock (_lock) {
                _isThreadRunning = false;
                Monitor.PulseAll(_lock);
            }
        }

        /// <summary>
        /// Starts a thread that periodically reads the MMRs of all the accounts provided by the ProfilerInfoProvider and calls the given listener whenever it has been updated.
        /// </summary>
        /// <param name="provider">A class to call to get the current list of accounts' ladder information to get MMR for.</param>
        /// <param name="listener">A listener to call when MMR has been retrieved for each account.</param>
        public void StartThread(ProfileInfoProvider provider, MmrListener listener) {
            lock (_lock) {
                if (!_isThreadRunning) {
                    _profileInfoProvider = provider;
                    _listener = listener;
                    _isThreadRunning = true;
                    _mmrReadThread = new Thread(ReadMmrLoop);
                    _mmrReadThread.Start();
                } else {
                    Console.WriteLine("Thread is already running.");
                }
            }
        }

        public void StopThread() {
            lock (_lock) {
                if (_isThreadRunning) {
                    _shouldReadMmr = false;
                    Monitor.PulseAll(_lock);

                    while (_isThreadRunning) {
                        Monitor.Wait(_lock);
                    }
                } else {
                    Console.WriteLine("Thread is not running.");
                }
            }
        }
    }
}
