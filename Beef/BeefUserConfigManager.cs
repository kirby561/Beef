using Beef.MmrReader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Beef {
    public class BeefUserConfigManager {
        // User registration information
        private String _beefUsersPath; // This is where to store the BeefUserConfigs

        // The lock is so that we can have safe read-only access from threads other than the thread this
        // manager is being used on.  Only one thread should be modifying this class however.
        private Object _userConfigsLock = new Object();
        private List<BeefUserConfig> _beefUserConfigs = new List<BeefUserConfig>();
        private Dictionary<String, BeefUserConfig> _userNameToBeefConfigMap = new Dictionary<String, BeefUserConfig>();
        private Dictionary<String, BeefUserConfig> _discordNameBeefConfigMap = new Dictionary<String, BeefUserConfig>();

        public static HashSet<char> _invalidCharacters = new HashSet<char>();

        static BeefUserConfigManager() {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                _invalidCharacters.Add(invalidChar);
            _invalidCharacters.Add('\r');
            _invalidCharacters.Add('\n');
        }

        public BeefUserConfigManager(String beefUsersPath) {
            _beefUsersPath = beefUsersPath;

            LoadBeefUserConfigurations();
        }

        /// <summary>
        /// Fills up the BeefUserConfig list and the corresponding maps to map user names and discord names.
        /// </summary>
        private void LoadBeefUserConfigurations() {
            EnsureBeefUserConfigDirectoryExists();

            foreach (String filePath in Directory.EnumerateFiles(_beefUsersPath)) {
                try {
                    String fileContents = File.ReadAllText(filePath);
                    if (!String.IsNullOrEmpty(fileContents)) {
                        BeefUserConfig configFile = null;
                        try {
                            configFile = JsonConvert.DeserializeObject<BeefUserConfig>(fileContents);
                            configFile.Version = BeefUserConfig.BeefUserVersion; // Since reading updates to the latest version, change it. In the future we may need to do something to upgrade.

                            // Do some sanity checking
                            if (configFile == null) {
                                Console.WriteLine("Invalid BeefUserConfigFile contents for: " + filePath);
                                continue;
                            }

                            if (String.IsNullOrWhiteSpace(configFile.BeefName)) {
                                Console.WriteLine("The beef name cant be empty in a config file: " + filePath);
                                continue;
                            }

                            lock (_userConfigsLock) {
                                if (_userNameToBeefConfigMap.ContainsKey(configFile.BeefName)) {
                                    Console.WriteLine("Duplicate names in the beef config: " + configFile.BeefName + " file: " + filePath);
                                    continue;
                                }
                           
                                if (String.IsNullOrWhiteSpace(configFile.DiscordName)) {
                                    Console.WriteLine("Discord name cant be empty. File: " + filePath);
                                    continue;
                                }

                                if (_discordNameBeefConfigMap.ContainsKey(configFile.DiscordName)) {
                                    Console.WriteLine("Duplicate discord names a beef user: " + configFile.DiscordName + " file: " + filePath);
                                    continue;
                                }

                                // Add the user and move on
                                _beefUserConfigs.Add(configFile);
                                _userNameToBeefConfigMap.Add(configFile.BeefName, configFile);
                                _discordNameBeefConfigMap.Add(configFile.DiscordName, configFile);
                            }
                        } catch (Exception ex) {
                            Console.WriteLine("Could not deserialize the BeefUserConfig file.  Is the format correct?");
                            Console.WriteLine("Exception: " + ex.Message);
                            if (ex.InnerException != null) {
                                Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                            }
                        }
                    } else {
                        Console.WriteLine("Invalid BeefUserConfig, it shouldnt be empty: " + filePath);
                    }
                } catch (IOException ex) {
                    Console.WriteLine("Unable to open the config file at " + filePath);
                    Console.WriteLine("Does the file exist?");
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Gets the given user config if the user has been registered.
        /// </summary>
        /// <param name="name">The beef name of the user that has been registered.</param>
        /// <returns>Returns the user config if they have been registered or null if they have not.</returns>
        public BeefUserConfig GetUserByName(String name) {
            try {
                lock (_userConfigsLock) {
                    return _userNameToBeefConfigMap[name];
                }
            } catch (KeyNotFoundException) {
                return null;
            }
        }

        /// <summary>
        /// Gets the beef name of the registered user if they are registered.
        /// </summary>
        /// <param name="discordId">The discord ID of the player you are looking for.</param>
        /// <returns>Returns the config of the user if they have been registered.  Returns null if they have not been registered yet.</returns>
        public BeefUserConfig GetUserByDiscordId(String discordId) {
            try {
                lock (_userConfigsLock) {
                    return _discordNameBeefConfigMap[discordId];
                }
            } catch (KeyNotFoundException) {
                return null;
            }
        }

        /// <summary>
        /// Updates the last known ladder information for a given beef user.
        /// </summary>
        /// <param name="beefName">The user's beef name.</param>
        /// <param name="ladderInfo">The ladder info for the user.</param>
        /// <returns>Returns ErrorCode.Success if it succeeded, otherwise an error indicating the problem.</returns>
        public ErrorCode UpdateUserLadderInfo(String beefName, LadderInfo ladderInfo) {
            lock (_userConfigsLock) {
                BeefUserConfig user = _userNameToBeefConfigMap[beefName];
                user.LastKnownLeague = ladderInfo.League;
                user.LastKnownMmr = ladderInfo.Mmr;
                user.LastKnownMainRace = ladderInfo.Race;
                ErrorCode error = WriteBeefUserToFile(user, GetBeefUserFilePath(user.BeefName));
                return error;
            }
        }

        /// <summary>
        /// Links the given beef name to the given battle.net account.
        /// </summary>
        /// <param name="beefName">The beef name to link.</param>
        /// <param name="battleNetAccountUrl">A URL to a SC2 Battle.net account of the form https://starcraft2.com/en-gb/profile/[region]/[realm]/[profileId]</param>
        /// <returns>Returns ErrorCode.Success if everything was ok or an error otherwise.</returns>
        public ErrorCode LinkUserToBattleNetAccount(String beefName, String battleNetAccountUrl) {
            ProfileInfo info = ParseBattleNetAccountUrl(battleNetAccountUrl);
            if (info == null)
                return ErrorCode.InvalidBattleNetUrlFormat;

            lock (_userConfigsLock) {
                BeefUserConfig user = _userNameToBeefConfigMap[beefName];
                user.ProfileInfo = info;
                WriteBeefUserToFile(user, GetBeefUserFilePath(user.BeefName));
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// Unlinks the Battle.net account from the given beefName.
        /// </summary>
        /// <param name="beefName">The name to unlink the battle.net account from.</param>
        /// <returns>Returns ErrorCode.Success if everything was ok or an error otherwise.</returns>
        public ErrorCode UnlinkUserFromBattleNetAccount(String beefName) {
            lock (_userConfigsLock) {
                BeefUserConfig user = _userNameToBeefConfigMap[beefName];
                user.ProfileInfo = null;
                WriteBeefUserToFile(user, GetBeefUserFilePath(user.BeefName));
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// Registers the given user information with the ladder.
        /// </summary>
        /// <param name="beefName">The name that will appear on the ladder itself.  This must be unique.</param>
        /// <param name="discordName">The discord name to associate with the beef name.</param>
        /// <returns>Returns success if it was successful or an error code if not.</returns>
        public ErrorCode RegisterUser(String beefName, String discordName) {
            return RegisterUser(new BeefUserConfig(beefName, discordName));
        }

        /// <summary>
        /// Registers the given user information with the ladder.
        /// </summary>
        /// <param name="userConfig">A BeefUserConfig containing the information for the player to register.</param>
        /// <returns>Returns success if it was successful or an error code if not.</returns>
        public ErrorCode RegisterUser(BeefUserConfig userConfig) {
            lock (_userConfigsLock) {
                if (_discordNameBeefConfigMap.ContainsKey(userConfig.DiscordName))
                    return ErrorCode.DiscordNameExists;

                if (_userNameToBeefConfigMap.ContainsKey(userConfig.BeefName))
                    return ErrorCode.BeefNameAlreadyExists;

                if (ContainsInvalidCharacters(userConfig.BeefName))
                    return ErrorCode.BeefNameContainsInvalidCharacters;
            }

            String filePath = GetBeefUserFilePath(userConfig.BeefName);
            if (File.Exists(filePath))
                return ErrorCode.BeefFileAlreadyExists;
            
            ErrorCode code = WriteBeefUserToFile(userConfig, filePath);

            // Add them to the maps
            lock (_userConfigsLock) {
                _beefUserConfigs.Add(userConfig);
                _userNameToBeefConfigMap.Add(userConfig.BeefName, userConfig);
                _discordNameBeefConfigMap.Add(userConfig.DiscordName, userConfig);
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// Delets the given user from the list of registered users or returns the reason why it can't be removed.
        /// </summary>
        /// <param name="beefName">The name to remove.</param>
        /// <returns>Returns success if the user was unregistered.  An error code is returned otherwise.</returns>
        public ErrorCode DeleteUser(String beefName) {
            lock (_userConfigsLock) {
                if (!_userNameToBeefConfigMap.ContainsKey(beefName))
                    return ErrorCode.BeefNameDoesNotExist;

                BeefUserConfig user = _userNameToBeefConfigMap[beefName];
                String filePath = GetBeefUserFilePath(beefName);
                if (File.Exists(filePath)) {
                    try {
                        File.Delete(filePath);
                        _beefUserConfigs.Remove(user);
                        _userNameToBeefConfigMap.Remove(beefName);
                        _discordNameBeefConfigMap.Remove(user.DiscordName);
                    } catch (Exception ex) {
                        Console.WriteLine("Could not delete the BeefUserConfig file at " + filePath);
                        Console.WriteLine("Exception: " + ex.Message);
                        if (ex.InnerException != null) {
                            Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                        }

                        return ErrorCode.CouldNotDeleteFile;
                    }
                }
            }

            return ErrorCode.Success;
        }

        /// <returns>Returns the list of registered users.</returns>
        public List<BeefUserConfig> GetUsers() {
            lock (_userConfigsLock) {
                return _beefUserConfigs;
            }
        }

        /// <returns>Returns a copy of the list of registered users.</returns>
        public List<BeefUserConfig> GetUsersCopy() {
            lock (_userConfigsLock) {
                List<BeefUserConfig> copy = new List<BeefUserConfig>();
                foreach (BeefUserConfig config in GetUsers()) {
                    copy.Add(config.Clone());
                }
                return copy;
            }
        }

        /// <summary>
        /// Modifies the user with the given information.
        /// </summary>
        /// <param name="existingBeefName">The existing beef name of the user to modify.</param>
        /// <param name="newBeefName">The new beef name to give them.</param>
        /// <param name="newDiscordName">The new discord name to give them.</param>
        /// <returns>Returns success if it was successful or an error code if it wasn't.</returns>
        public ErrorCode ModifyUser(String existingBeefName, String newBeefName, String newDiscordName) {
            BeefUserConfig existingUser = null;
            lock (_userConfigsLock) {
                if (!_userNameToBeefConfigMap.ContainsKey(existingBeefName)) {
                    return ErrorCode.NoExistingPlayerByThatName;
                }

                // Grab their info
                existingUser = new BeefUserConfig(_userNameToBeefConfigMap[existingBeefName]);
                existingUser.BeefName = newBeefName;
            }

            ErrorCode result = DeleteUser(existingBeefName);
            if (!result.Ok())
                return result;

            return RegisterUser(existingUser);
        }

        /// <summary>
        /// Makes sure the user directory exists.
        /// </summary>
        private void EnsureBeefUserConfigDirectoryExists() {
            Directory.CreateDirectory(_beefUsersPath);
        }

        /// <summary>
        /// Indicates if the name contains invalid characters or not.
        /// Invalid characters are anything that shouldn't be in a beef name or
        /// anything that shouldn't be in a file name.
        /// </summary>
        /// <param name="beefName">The name to check</param>
        /// <returns>Returns true if there are invalid characters.  False if it's good to go.</returns>
        private Boolean ContainsInvalidCharacters(String beefName) {
            foreach (char c in beefName.ToCharArray()) {
                if (_invalidCharacters.Contains(c))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the beef user file path from the given name.
        /// </summary>
        /// <param name="beefName">The name of the user</param>
        /// <returns>Returns the full file path to the config file for that user.</returns>
        private String GetBeefUserFilePath(String beefName) {
            return _beefUsersPath + "/" + beefName;
        }

        /// <returns>Returns the list of characters that can't be used in names.</returns>
        public static char[] GetInvalidCharacters() {
            char[] items = new char[_invalidCharacters.Count];
            _invalidCharacters.CopyTo(items);
            return items;
        }

        /// <summary>
        /// Parses the given profile URL.
        /// </summary>
        /// <param name="url">The URL to parse.  Should be of the form https://starcraft2.com/en-gb/profile/[region]/[realm]/[profileId] </param>
        /// <returns>The generated profile info or null if the link wasn't the right format.</returns>
        private ProfileInfo ParseBattleNetAccountUrl(String url) {
            const String ladderIdRegex = "\\/profile\\/([0-9]{1})\\/([0-9]{1})\\/([0-9]*)";
            Match match = Regex.Match(url, ladderIdRegex, RegexOptions.None, new TimeSpan(0, 0, 5));
            if (match.Success) {
                // There should be 4 groups (3 + the full match at [0])
                if (match.Groups.Count != 4)
                    return null;

                long regionId = -1;
                if (!long.TryParse(match.Groups[1].Value, out regionId))
                    return null;
                if (regionId >= ProfileInfo.RegionIdMap.Length) {
                    Console.WriteLine("Unknown RegionId in the URL: " + regionId);
                    return null;
                }

                int realmId = -1;
                if (!int.TryParse(match.Groups[2].Value, out realmId))
                    return null;

                long profileId = -1;
                if (!long.TryParse(match.Groups[3].Value, out profileId))
                    return null;

                // Fill out the config
                ProfileInfo info = new ProfileInfo(
                    ProfileInfo.RegionIdMap[regionId],
                    realmId,
                    profileId
                );

                return info;
            } else {
                // No matches found - the URL is probably not formatted correctly
                return null;
            }
        }

        /// <summary>
        /// Writes the given beef user config to the given file.
        /// </summary>
        /// <param name="userConfig">The user config to write</param>
        /// <param name="filePath">The file path to write to.</param>
        /// <returns>Returns ErrorCode.Success or an error if there was a problem.</returns>
        private ErrorCode WriteBeefUserToFile(BeefUserConfig userConfig, String filePath) {
            String configString = null;
            try {
                configString = JsonConvert.SerializeObject(userConfig);
            } catch (Exception ex) {
                Console.WriteLine("Could not serialize the given config.");
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                return ErrorCode.BeefFileCouldNotSerialize;
            }

            configString = JsonUtil.PoorMansJsonFormat(configString);

            // Write it to a file
            try {
                File.WriteAllText(filePath, configString);
            } catch (Exception ex) {
                Console.WriteLine("Could not write the BeefUserConfig file to " + filePath);
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                return ErrorCode.BeefFileCouldNotWriteFile;
            }

            return ErrorCode.Success;
        }

        public BeefUserConfig GetUserByProfileId(long profileId) {
            // Slow implementation for the moment
            lock (_userConfigsLock) {
                foreach (BeefUserConfig user in _beefUserConfigs) {
                    if (user.ProfileInfo != null && user.ProfileInfo.ProfileId == profileId)
                        return user;
                }
            }

            return null;
        }
    }
}
