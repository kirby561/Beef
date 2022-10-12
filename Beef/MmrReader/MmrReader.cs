using Beef.SharedServices;
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
    public class MmrReader : PollingService {    /// <summary>
        // Maps regions to region IDs that we use on the endpoint.
        public Dictionary<String, int> _regionIdMap = new Dictionary<String, int>();

        // Reader state
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
        /// <param name="provider">A class to call to get the current list of accounts' ladder information to get MMR for.</param>
        /// <param name="listener">A listener to call when MMR has been retrieved for each account.</param>
        /// <param name="configuration">The configuration to use.  See ReaderConfig for details.</param>
        public MmrReader(ProfileInfoProvider provider, MmrListener listener, ReaderConfig configuration) : base("MMR Reader Service", configuration.MsPerRead) {
            _profileInfoProvider = provider;
            _listener = listener;
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

        /// <returns>Requests and returns an access token to use for HTTP requests to the API.</returns>
        protected String GetAccessToken() {
            return GetOAuthAccessToken("https://us.battle.net/oauth/token", _configuration.ClientId, _configuration.ClientSecret, _accessCacheFile);
        }

        /// <summary>
        /// Run the polling action.
        /// </summary>
        protected override void PollingAction() {
            // Do the next refresh
            List<Tuple<ProfileInfo, LadderInfo>> nextMmrList = new List<Tuple<ProfileInfo, LadderInfo>>();
            List<ProfileInfo> users = _profileInfoProvider.GetLadderUsers();
            foreach (ProfileInfo user in users) {
                LadderInfo ladderInfo = GetBestLadderInfoFor(user);
                nextMmrList.Add(new Tuple<ProfileInfo, LadderInfo>(user, ladderInfo));

                // We are getting error 500 a lot so try not spamming the server as much
                Thread.Sleep(10);
            }

            _listener.OnMmrRead(nextMmrList);
        }
    }
}
