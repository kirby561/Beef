using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Beef.SharedServices {
    /// <summary>
    /// A base class that can be extended for implementing a polling service. 
    /// This is useful when something needs to be checked or
    /// updated periodically.
    /// </summary>
    public abstract class PollingService {
        // Constants
        public const long TicksPerMs = 10000;

        protected String _serviceName;
        protected long _pollingPeriodMs;

        protected Thread _pollingThread;
        protected bool _shouldRun = true;
        protected bool _isThreadRunning = false;
        protected object _lock = new object();
        protected bool _manualUpdateRequested = false; // Requests 1 update immediately instead of waiting the remaining time.

        public PollingService(String serviceName, long pollingPeriodMs) {
            _serviceName = serviceName;
            _pollingPeriodMs = pollingPeriodMs;
        }

        /// <summary>
        /// Implement this method, which will be called at the polling period specified in the constructor.
        /// </summary>
        protected abstract void PollingAction();
        
        /// <returns>Returns a unix timestamp (ms since the epoch)</returns>
        protected long GetNowInMs() {
            return ConvertDateTimeToUnixTimestampMs(DateTime.Now.ToUniversalTime());
        }

        protected long ConvertDateTimeToUnixTimestampMs(DateTime dateTime) {
            return dateTime.Subtract(
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                ).Ticks / TicksPerMs;
        }
        
        /// <summary>
        /// Gets the time to next refresh.
        /// </summary>
        /// <param name="nextRefreshTimeMs">The unix time of the next refresh in milliseconds.</param>
        /// <returns>Returns the number of milliseconds until the next refresh or 0 if it should refresh now.</returns>
        protected long GetTimeToNextRefresh(long nextRefreshTimeMs) {
            if (_manualUpdateRequested) {
                return 0; // Update immediately if requested
            }

            long time = nextRefreshTimeMs - GetNowInMs();
            if (time < 0)
                time = 0;
            return time;
        }

        
        /// <summary>
        /// This is the entry point to the background thread and just loops running the polling action at the configured interval.
        /// </summary>
        private void PollingLoop() {
            long nextPollTime = GetNowInMs();
            bool forcePollNow = true;
            while (_shouldRun) {
                if (GetTimeToNextRefresh(nextPollTime) == 0 || forcePollNow) {
                    Console.WriteLine("Running " + _serviceName);

                    // Set the next time
                    nextPollTime = GetNowInMs() + _pollingPeriodMs;
                    forcePollNow = false;

                    PollingAction();
                }

                int timeToNextRefresh = (int)GetTimeToNextRefresh(nextPollTime);
                lock (_lock) {
                    if (_manualUpdateRequested) {
                        forcePollNow = true;
                        _manualUpdateRequested = false;
                    } else {
                        Monitor.Wait(_lock, timeToNextRefresh);
                    }
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
        public void StartThread() {
            lock (_lock) {
                if (!_isThreadRunning) {
                    _isThreadRunning = true;
                    _pollingThread = new Thread(PollingLoop);
                    _pollingThread.Start();
                } else {
                    Console.WriteLine("Thread is already running.");
                }
            }
        }

        public void StopThread() {
            lock (_lock) {
                if (_isThreadRunning) {
                    _shouldRun = false;
                    Monitor.PulseAll(_lock);

                    while (_isThreadRunning) {
                        Monitor.Wait(_lock);
                    }
                } else {
                    Console.WriteLine("Thread is not running.");
                }
            }
        }

        /// <summary>
        /// Requests an MMR update to happen right away.
        /// </summary>
        public void RequestUpdate() {
            lock (_lock) {
                _manualUpdateRequested = true;
                Monitor.PulseAll(_lock);
            }
        }

        
        /// <summary>
        /// Checks if an OAuth access token is out of date. If it is, requests a new one.
        /// </summary>
        /// <param name="url">The URL of the endpoint to call to get the auth token from.</param>
        /// <param name="clientId">The ID of the client to use.</param>
        /// <param name="clientSecret">The client secret to use.</param>
        /// <param name="accessCacheFilePath">A path to file to use/check for a cached token. The file will be created if it does not exist for future calls.</param>
        /// <returns>
        /// Returns the current access token or an empty string if we couldn't get one.
        /// </returns>
        /// 
        /// <remarks>
        /// This method assumes the following parameters are supplied in a GET request to the URL:
        ///     [url]?client_id=...&client_secret=...&grant_type=client_credentials
        /// And the result returns json with the following:
        ///    {
        ///        "access_token": "...",
        ///        "expires_in": 5607648,
        ///        "token_type": "bearer"
        ///    }
        /// expires_in is in seconds.
        /// 
        /// </remarks>
        protected String GetOAuthAccessToken(String url, String clientId, String clientSecret, String accessCacheFilePath) {
            AccessTokenInfo accessTokenInfo = null;
            HttpClient httpClient = new HttpClient();

            // Update from our cache file
            if (File.Exists(accessCacheFilePath)) {
                String accessFileContents = File.ReadAllText(accessCacheFilePath);

                try {
                    accessTokenInfo = JsonConvert.DeserializeObject<AccessTokenInfo>(accessFileContents);
                } catch (Exception serializerEx) {
                    Console.WriteLine("Access token cache file corrupted.  Removing and refreshing the token.  Exception: " + serializerEx.Message);
                    try {
                        File.Delete(accessCacheFilePath);
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
                    {"client_id", clientId},
                    {"client_secret", clientSecret}
                };

            Task<HttpResponseMessage> responseTask = httpClient.PostAsync(url, new FormUrlEncodedContent(content));
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
                    dynamic response = JsonConvert.DeserializeObject<dynamic>(resultString.Result);
                    accessTokenInfo = new AccessTokenInfo();
                    accessTokenInfo.AccessToken = response["access_token"];

                    // expires_in is in seconds according to the Twitch API documentation.
                    accessTokenInfo.ExpirationTimeMs = GetNowInMs() + 1000 * (long)response["expires_in"];

                    // Serialize to file
                    try {
                        String serializedTokenInfo = JsonConvert.SerializeObject(accessTokenInfo);
                        File.WriteAllText(accessCacheFilePath, serializedTokenInfo);
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
    }
}
