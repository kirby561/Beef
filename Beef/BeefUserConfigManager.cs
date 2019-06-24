using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Beef {
    class BeefUserConfigManager {
        // User registration information
        private String _beefUsersPath; // This is where to store the BeefUserConfigs
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
                        JavaScriptSerializer deserializer = new JavaScriptSerializer();
                        BeefUserConfig configFile = null;
                        try {
                            configFile = deserializer.Deserialize<BeefUserConfig>(fileContents);

                            // Do some sanity checking
                            if (configFile == null) {
                                Console.WriteLine("Invalid BeefUserConfigFile contents for: " + filePath);
                                continue;
                            }

                            if (String.IsNullOrWhiteSpace(configFile.BeefName)) {
                                Console.WriteLine("The beef name cant be empty in a config file: " + filePath);
                                continue;
                            }

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
                return _userNameToBeefConfigMap[name];
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
                return _discordNameBeefConfigMap[discordId];
            } catch (KeyNotFoundException) {
                return null;
            }
        }

        /// <summary>
        /// Registers the given user information with the ladder.
        /// </summary>
        /// <param name="beefName">The name that will appear on the ladder itself.  This must be unique.</param>
        /// <param name="discordName">The discord name to associate with the beef name.</param>
        /// <returns>Returns success if it was successful or an error code if not.</returns>
        public ErrorCode RegisterUser(String beefName, String discordName) {
            if (_discordNameBeefConfigMap.ContainsKey(discordName))
                return ErrorCode.DiscordNameExists;

            if (_userNameToBeefConfigMap.ContainsKey(beefName))
                return ErrorCode.BeefNameAlreadyExists;

            if (ContainsInvalidCharacters(beefName))
                return ErrorCode.BeefNameContainsInvalidCharacters;

            String filePath = GetBeefUserFilePath(beefName);
            if (File.Exists(filePath))
                return ErrorCode.BeefFileAlreadyExists;

            BeefUserConfig config = new BeefUserConfig(beefName, discordName);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            String configString = null;
            try {
                configString = serializer.Serialize(config);
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

            // Add them to the maps
            _beefUserConfigs.Add(config);
            _userNameToBeefConfigMap.Add(config.BeefName, config);
            _discordNameBeefConfigMap.Add(config.DiscordName, config);

            return ErrorCode.Success;
        }

        /// <summary>
        /// Delets the given user from the list of registered users or returns the reason why it can't be removed.
        /// </summary>
        /// <param name="beefName">The name to remove.</param>
        /// <returns>Returns success if the user was unregistered.  An error code is returned otherwise.</returns>
        public ErrorCode DeleteUser(String beefName) {
            if (!_userNameToBeefConfigMap.ContainsKey(beefName))
                return ErrorCode.BeefNameDoesNotExist;

            String filePath = GetBeefUserFilePath(beefName);
            if (File.Exists(filePath)) {
                try {
                    File.Delete(filePath);
                } catch (Exception ex) {
                    Console.WriteLine("Could not delete the BeefUserConfig file at " + filePath);
                    Console.WriteLine("Exception: " + ex.Message);
                    if (ex.InnerException != null) {
                        Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                    }

                    return ErrorCode.CouldNotDeleteFile;
                }
            }

            return ErrorCode.Success;
        }
        
        /// <returns>Returns the list of registered users.</returns>
        public List<BeefUserConfig> GetUsers() {
            return _beefUserConfigs;
        }

        /// <summary>
        /// Modifies the user with the given information.
        /// </summary>
        /// <param name="existingBeefName">The existing beef name of the user to modify.</param>
        /// <param name="newBeefName">The new beef name to give them.</param>
        /// <param name="newDiscordName">The new discord name to give them.</param>
        /// <returns>Returns success if it was successful or an error code if it wasn't.</returns>
        public ErrorCode ModifyUser(String existingBeefName, String newBeefName, String newDiscordName) {
            ErrorCode result = RegisterUser(newBeefName, newDiscordName);
            if (!result.Ok()) {
                return result;
            }

            return DeleteUser(existingBeefName);
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
    }
}
