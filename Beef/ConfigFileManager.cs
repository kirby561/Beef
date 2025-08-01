﻿using Newtonsoft.Json;
using System;
using System.IO;

namespace Beef {
    /// <summary>
    /// Provides methods for reading and writing the config file.
    /// </summary>
    public class ConfigFileManager {
        /// <summary>
        /// Reads the configuration file at the given path.
        /// </summary>
        /// <param name="path">A path to a JSON config file to read.  The JSON file must exactly match the ReaderConfig class.</param>
        /// <returns>Returns the configuration read in the file or null if there was an error.  The error is printed to stdout as well.</returns>
        public static BeefConfig ReadConfigFile(String path) {
            String configFileText = null;
            try {
                configFileText = File.ReadAllText(path);
            } catch (IOException ex) {
                Console.WriteLine("Unable to open the config file at " + path);
                Console.WriteLine("Does the file exist?");
                Console.WriteLine("Exception: " + ex.Message);
                return null;
            }

            BeefConfig configFile = null;

            try {
                configFile = JsonConvert.DeserializeObject<BeefConfig>(configFileText);
            } catch (Exception ex) {
                Console.WriteLine("Could not deserialize the config file.  Is the format correct?  See Config.json.example for correct usage.");
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }
            }

            return configFile;
        }

        /// <summary>
        /// Saves the given config file to the given path.
        /// </summary>
        /// <param name="config">The config to write.</param>
        /// <param name="path">The path to save the config file to</param>
        /// <returns>Returns true if it succeeded, false if there was an error.</returns>
        public static bool SaveConfigFile(BeefConfig config, String path) {
            String configString = null;
            try {
                configString = JsonConvert.SerializeObject(config);
            } catch (Exception ex) {
                Console.WriteLine("Could not serialize the given config.");
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                return false;
            }

            configString = JsonUtil.PoorMansJsonFormat(configString);

            // Write it to a file
            try {
                File.WriteAllText(path, configString);
            } catch (Exception ex) {
                Console.WriteLine("Could not write the config file to " + path);
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }
            }

            return true;
        }
    }
}
