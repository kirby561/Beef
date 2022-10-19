using System;
using System.Collections.Generic;
using System.IO;

namespace Beef {
    class BackupManager {
        private String _backupPath;

        public BackupManager(String backupPath) {
            _backupPath = backupPath;
        }

        /// <summary>
        /// Writes all the given entries to a backup file. // ?? TODO: Make this not crash if it can't open the file for some reason.
        /// </summary>
        /// <param name="entries">The entires to write.</param>
        public void Backup(List<BeefEntry> entries) { 
            EnsureBackupDirectoryExists();

            String backupContents = "";
            foreach (BeefEntry entry in entries) {
                backupContents += entry.PlayerRank + "=" + entry.PlayerName + "\n";
            }

            String backupName = "backup_" + (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) + ".beef";
            File.WriteAllText(_backupPath + "/" + backupName, backupContents);
        }

        /// <summary>
        /// Loads the backup at the given path.
        /// </summary>
        /// <param name="backupPath">The path of the backup to load.</param>
        /// <returns>Returns the list of entries (note there's no ObjectId associated with them yet) or null if there was something wrong with the backup.</returns>
        public List<BeefEntry> LoadBackup(String backupPath) {
            String backup = File.ReadAllText(backupPath);

            List<BeefEntry> entries = new List<BeefEntry>();
            String[] lines = backup.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String line in lines) {
                String[] rankAndPlayer = line.Split('=');
                if (rankAndPlayer.Length != 2) {
                    Console.WriteLine("Invalid backup at " + backupPath);
                    return null;
                }

                String rank = rankAndPlayer[0];
                String player = rankAndPlayer[1];

                int rankInt;
                if (!int.TryParse(rank, out rankInt)) {
                    Console.WriteLine("Invalid rank in backup at " + backupPath);
                    return null;
                }

                entries.Add(new BeefEntry {
                    PlayerRank = rankInt,
                    PlayerName = player
                });
            }

            return entries;
        }

        /// <summary>
        /// Gets the latest n number of backup paths from the backups folder.
        /// </summary>
        /// <param name="n">The number of backups to retrieve.</param>
        /// <returns>An array of the paths to each backup.</returns>
        public String[] GetLatestBackups(int n) {
            List<String> files = new List<String>();
            foreach (String filePath in Directory.EnumerateFiles(_backupPath, "*.beef")) {
                files.Add(filePath);
            }

            files.Sort(delegate (String str1, String str2) {
                // Sort most recent to least recent
                return str2.CompareTo(str1);
            });

            int numBackups = Math.Min(n, files.Count);
            String[] latestBackups = new String[numBackups];
            for (int i = 0; i < numBackups; i++)
                latestBackups[i] = files[i];

            return latestBackups;
        }

        private void EnsureBackupDirectoryExists() {
            Directory.CreateDirectory(_backupPath);
        }
    }
}
