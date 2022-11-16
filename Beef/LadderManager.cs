
using System.IO;
using Beef.MmrReader;

namespace Beef {
    /// <summary>
    /// Manages the latest version of the current ladder and faciliates changes to it.
    /// </summary>
    public class LadderManager {
        private List<BeefEntry> _entries = new List<BeefEntry>();

        private BackupManager _backupManager;
        private Dictionary<String, Tuple<ProfileInfo, LadderInfo>> _mmrDictionary;

        public delegate void OnLadderChanged(List<BeefEntry> entries);
        public event OnLadderChanged LadderChanged;

        /// <summary>
        /// Creates a LadderManager with the given information.
        /// </summary>
        /// <param name="backupLocation">The location to store backups of the ladder.</param>
        public LadderManager(String backupLocation) {
            _backupManager = new BackupManager(backupLocation);            
        }

        /// <summary>
        /// This can be used for reporting a win from a player not on the bracket yet.
        /// </summary>
        /// <param name="winnerName">The name of the winner.</param>
        /// <param name="loserRank">The rank of the loser.</param>
        /// <returns></returns>
        public ErrorCode ReportWin(String winnerName, int loserRank) {
            List<BeefEntry> entries = ReadBracket();
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder; // There was an error reading the bracket.

            int loserIndex = loserRank - 1;

            if (loserIndex < 0 || loserIndex >= entries.Count)
                return ErrorCode.LoserRankInvalid; // Invalid loser rank

            // Check for the winner
            int winnerIndex = -1;
            for (int i = 0; i < entries.Count; i++) {
                BeefEntry entry = entries[i];

                // Is this the winner?
                if (winnerName.Equals(entry.PlayerName)) {
                    if (winnerIndex == -1)
                        winnerIndex = i;
                    else {
                        // The name is in there twice?
                        return ErrorCode.DuplicateWinnerEntriesWithSameName;
                    }
                }
            }

            // If the winner wasn't found, they are new to the bracket.
            // So give them one passed the end of the list
            winnerIndex = entries.Count;

            return ReportWin(entries, winnerName, winnerIndex, loserIndex);
        }

        public ErrorCode ReportWin(int winnerRank, int loserRank) {
            List<BeefEntry> entries = ReadBracket();
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder; // There was an error reading the bracket.

            if (winnerRank <= loserRank)
                return ErrorCode.WinnerCantBeHigherThanLoser; // You can't beat yourself and if you beat someone below you nothing happens.

            int winnerIndex = winnerRank - 1;
            int loserIndex = loserRank - 1;

            if (winnerIndex < 0 || winnerIndex >= entries.Count)
                return ErrorCode.WinnerRankInvalid; // Invalid winner rank

            if (loserIndex < 0 || loserIndex >= entries.Count)
                return ErrorCode.LoserRankInvalid; // Invalid loser rank

            // Get the winner name
            String winnerName = entries[winnerIndex].PlayerName;
            return ReportWin(entries, winnerName, winnerIndex, loserIndex);
        }

        public ErrorCode ReportWin(String winnerName, String loserName) {
            List<BeefEntry> entries = ReadBracket();
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder;

            if (winnerName.Equals(loserName))
                return ErrorCode.YouCantBeatYourself;

            int winnerIndex = -1;
            int loserIndex = -1;
            for (int i = 0; i < entries.Count; i++) {
                BeefEntry entry = entries[i];

                // Is this the winner?
                if (winnerName.Equals(entry.PlayerName)) {
                    if (winnerIndex == -1)
                        winnerIndex = i;
                    else {
                        // The name is in there twice?
                        return ErrorCode.DuplicateWinnerEntriesWithSameName;
                    }
                }

                // Is this the loser?
                if (loserName.Equals(entry.PlayerName)) {
                    if (loserIndex == -1)
                        loserIndex = i;
                    else {
                        // The name is in there twice?
                        return ErrorCode.DuplicateLoserEntriesWithSameName;
                    }
                }
            }

            if (winnerIndex == -1) {
                // This is fine.  This means that the winner is not in the bracket so they need to be added
                winnerIndex = _entries.Count;
            }

            if (loserIndex == -1) {
                // This is a problem. You can't beat someone not on the bracket to get on the bracket...
                return ErrorCode.LoserIsNotOnTheLadder;
            }

            if (loserIndex >= winnerIndex) {
                return ErrorCode.WinnerCantBeHigherThanLoser;
            }

            return ReportWin(entries, winnerName, winnerIndex, loserIndex);
        }

        /// <summary>
        /// Internal report win method that assumes the list of entries has already been retrieved.
        /// </summary>
        /// <param name="winnerName">The name of the winner.  This is needed because the winner is not necessarily on the bracket.</param>
        /// <param name="winnerIndex">The INDEX of the winner.  Note that this is NOT the rank it is therank - 1. 
        ///                           (Or 1 passed the last index if the winner is not yet on the bracket)</param>
        /// <param name="loserIndex">The INDEX of the loser.  Note that this is NOT the rank, it is the rank - 1</param>
        /// <returns>Returns true if the bracket was successfully updated.  False otherwise.</returns>
        private ErrorCode ReportWin(List<BeefEntry> entries, String winnerName, int winnerIndex, int loserIndex) {
            String previousName = "";
            for (int i = loserIndex; i <= winnerIndex; i++) {
                // If the winner wasn't on the bracket, then we want to bail out here
                if (i == entries.Count)
                    break;

                // If it's the winner, add them
                if (i == loserIndex) {
                    previousName = entries[i].PlayerName;
                    entries[i].PlayerName = winnerName;
                } else {
                    String temp = entries[i].PlayerName;
                    entries[i].PlayerName = previousName;
                    previousName = temp;
                }
            }

            // Update the bracket
            return UpdateLadderFromEntries(entries, true);
        }

        /// <summary>
        /// Renames the given player to the new name in the bracket.
        /// If they aren't in the bracket nothing happens.
        /// </summary>
        /// <param name="oldName">The existing name.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>Returns true if the rename happened.  False if there was an error.</returns>
        public ErrorCode RenamePlayer(String oldName, String newName) {
            List<BeefEntry> entries = ReadBracket();
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder; // There was an error reading the bracket.

             // Make sure that name doesn't exist already
            foreach (BeefEntry entry in entries) {
                if (entry.PlayerName.Equals(newName)) {
                    return ErrorCode.DuplicatePlayerNameWhenRenaming;
                }
            }

            // Get the object ID for this player
            BeefEntry existingPlayerEntry = null;
            for (int i = 0; i < entries.Count; i++) {
                BeefEntry entry = entries[i];
                if (oldName.Equals(entry.PlayerName)) {
                    existingPlayerEntry = entry;
                    break;
                }
            }

            if (existingPlayerEntry == null)
                return ErrorCode.NoExistingPlayerByThatName; // Player not found

            return RenamePlayer(existingPlayerEntry, newName);
        }

        /// <summary>
        /// Renames the player at the given index with the given name.
        /// </summary>
        /// <param name="index">The index of the player (the rank - 1).</param>
        /// <param name="newName">The new name for the player.</param>
        /// <param name="existingPlayer">Set to the existing player if there wasn't an error.  Set to null if there was an error.</param>
        /// <returns>Returns Success or an error code for what went wrong.</returns>
        public ErrorCode RenamePlayer(int index, String newName, out BeefEntry existingPlayer) {
            existingPlayer = null;
            List<BeefEntry> entries = ReadBracket();
            BeefEntry existingPlayerToBe = entries[index];
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder; // There was an error reading the bracket.

            if (index <= 0 || index >= entries.Count) {
                return ErrorCode.RankNotOnLadder;
            }

            // Make sure that name doesn't exist already
            foreach (BeefEntry entry in entries) {
                if (entry.PlayerName.Equals(newName)) {
                    return ErrorCode.DuplicatePlayerNameWhenRenaming;
                }
            }

            BeefEntry playerToRename = entries[index];
            ErrorCode result = RenamePlayer(playerToRename, newName);
     
            if (result.Ok())
                existingPlayer = existingPlayerToBe;

            return result;
        }

        /// <summary>
        /// Renames the given entry to the new name.
        /// </summary>
        /// <param name="existingEntry">The existing entry in the ladder.</param>
        /// <param name="newName">The new name to give the player</param>
        /// <returns>Returns Success or what went wrong if it failed.</returns>
        private ErrorCode RenamePlayer(BeefEntry existingEntry, String newName) {
            existingEntry.PlayerName = newName;
            return UpdateLadderFromEntries(_entries, true);
        }

        /// <summary>
        /// Switches the 2 players in the beef ladder.
        /// </summary>
        /// <param name="nameOrRank1">A rank or beef name for the first player.</param>
        /// <param name="nameOrRank2">A rank or beef name for the second player.</param>
        /// <param name="player1">This is set to the BeefEntry if the first player is found.</param>
        /// <param name="player2">This is set to the BeefEntry if the second player is found.</param>
        /// <returns>Returns success if it succeeded or an error code identifying what happened.</returns>
        public ErrorCode SwitchPlayers(String nameOrRank1, String nameOrRank2, out BeefEntry player1, out BeefEntry player2) {
            // Init the out params
            player1 = null;
            player2 = null;

            List<BeefEntry> entries = ReadBracket();
            player1 = GetBeefEntryFromNameOrRank(nameOrRank1, entries);
            if (player1 == null) {
                return ErrorCode.Player1DoesNotExist;
            }

            player2 = GetBeefEntryFromNameOrRank(nameOrRank2, entries);
            if (player2 == null) {
                return ErrorCode.Player2DoesNotExist;
            }

            return SwitchPlayers(player1, player2);
        }

        private ErrorCode SwitchPlayers(BeefEntry player1, BeefEntry player2) {
            // Assumes ReadBracket has already been called.
            String player1Name = player1.PlayerName;
            String player2Name = player2.PlayerName;
            player1.PlayerName = player2Name;
            player2.PlayerName = player1Name;

            return UpdateLadderFromEntries(_entries, true);
        }

        /// <summary>
        /// Removes the player from the ladder with the given name and shuffles everyone else down.
        /// </summary>
        /// <param name="name">The name of the player to remove.</param>
        /// <returns>Returns Success if it was successful or the error code if there was an issue.</returns>
        public ErrorCode RemovePlayer(String name) {
            List<BeefEntry> entries = ReadBracket();
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder; // There was an error reading the bracket.

            // Get the object ID for this player
            BeefEntry playerToRemove = null;
            for (int i = 0; i < entries.Count; i++) {
                BeefEntry entry = entries[i];
                if (name.Equals(entry.PlayerName)) {
                    playerToRemove = entry;
                    break;
                }
            }

            if (playerToRemove == null) {
                return ErrorCode.NoExistingPlayerByThatName;
            }

            return RemovePlayer(entries, playerToRemove);
        }

        /// <summary>
        /// Removes the player at the given index.
        /// </summary>
        /// <param name="index">The index of the player to remove.  Note this is the INDEX not the RANK.</param>
        /// <param name="playerToRemove">This is set to the player being removed if successful and null otherwise.</param>
        /// <returns>Returns Success if it was successful or the error code if there was an issue.</returns>
        public ErrorCode RemovePlayer(int index, out BeefEntry playerToRemove) {
            playerToRemove = null;
            List<BeefEntry> entries = ReadBracket();
            if (entries.Count == 0)
                return ErrorCode.CouldNotReadTheLadder; // There was an error reading the bracket.

            if (index <= 0 || index >= entries.Count) {
                return ErrorCode.RankNotOnLadder;
            }

            playerToRemove = new BeefEntry(entries[index]);
            ErrorCode result = RemovePlayer(entries, playerToRemove);
            if (!result.Ok()) {
                playerToRemove = null;
            }
            return result;
        }

        /// <summary>
        /// Builds the list of requests to remove the given BeefEntry (player) and submits it.
        /// </summary>
        /// <param name="entries">The list of entries</param>
        /// <param name="playerToRemove">The player to remove</param>
        /// <returns>Returns Success if it worked or the error code if it didn't.</returns>
        private ErrorCode RemovePlayer(List<BeefEntry> entries, BeefEntry playerToRemove) {
            // Move everyone one rank higher
            for (int index = playerToRemove.PlayerRank - 1; index < entries.Count; index++) {
                if (index + 1 < entries.Count) {
                    entries[index].PlayerName = entries[index + 1].PlayerName;
                } else {
                    entries[index].PlayerName = "";
                }                
            }

            return UpdateLadderFromEntries(entries, true);
        }

        /// <summary>
        /// Undoes the last change to the bracket.  Note that you can't undo across shape changes.
        /// For example, you add a new row to the ladder, you can't undo to a ladder of different size.
        /// </summary>
        /// <returns>Returns Success if it was successful or an error code if there was an issue.</returns>
        public ErrorCode Undo() {
            // Get the current ladder
            List<BeefEntry> currentBracket = ReadBracket();
            if (currentBracket.Count == 0)
                return ErrorCode.CouldNotReadTheLadder;

            // Get the latest backup
            String[] backups = _backupManager.GetLatestBackups(2);
            if (backups == null || backups.Length < 2)
                return ErrorCode.NothingToUndo;

            List<BeefEntry> backedUpEntries = _backupManager.LoadBackup(backups[1]);
            if (backedUpEntries.Count != currentBracket.Count)
                return ErrorCode.LadderDifferentSize;

            // Do the restore
            ErrorCode code = UpdateLadderFromEntries(backedUpEntries, false);
            if (!code.Ok())
                return code;

            // Move the backup
            try {
                String backupFileName = Path.GetFileName(backups[0]);
                String backupPath = Directory.GetParent(backups[0]).FullName;
                String archivePath = Directory.CreateDirectory(backupPath + "/Archive").FullName;
                File.Move(backups[0], archivePath + "/" + backupFileName);
            } catch (Exception e) {
                return ErrorCode.CouldNotRevertBackupFile;
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// Reads the current bracket sorted by rank.
        /// </summary>
        /// <returns>The bracket.</returns>
        public List<BeefEntry> ReadBracket() {
            _entries.Clear();

            // Get the latest version
            String[] backups = _backupManager.GetLatestBackups(1);
            if (backups == null || backups.Length < 1)
                return _entries;

            // The current ladder is the latest backup
            _entries = _backupManager.LoadBackup(backups[0]);
            
            return _entries;
        }

        /// <summary>
        /// Sets the dictionary to use to lookup player mmr and race.  Updates the bracket when set.
        /// </summary>
        /// <param name="dictionary">A dictionary mapping Beef Name to their corresponding best ladder and profile info.</param>
        /// <returns>Returns</returns>
        public ErrorCode UpdateMmrDictionary(Dictionary<String, Tuple<ProfileInfo, LadderInfo>> dictionary) {
            _mmrDictionary = dictionary;
            
            // Make sure we have the latest names
            List<BeefEntry> currentBracket = ReadBracket();

            return UpdateLadderFromEntries(currentBracket, false);
        }

        /// <summary>
        /// Returns the string that should be in a bracket entry on the ladder given the player entry.
        /// This includes the MMR and race if we have one for that player.
        /// </summary>
        /// <param name="entry">The beef entry to get the full string for.</param>
        /// <returns>Returns the full string.</returns>
        private String GetBracketStringFromEntry(BeefEntry entry) {
            if (String.IsNullOrEmpty(entry.PlayerName)) {
                return "";
            }

            String mmr = "";
            String race = "";
            String bracketString = entry.PlayerName;
            if (_mmrDictionary != null && _mmrDictionary.ContainsKey(entry.PlayerName)) {
                LadderInfo ladderInfo = _mmrDictionary[entry.PlayerName].Item2;
                if (ladderInfo != null) {
                    mmr = ladderInfo.Mmr;
                    race = ladderInfo.Race;
                }
            }

            if (mmr != "" || race != "") {
                bracketString += "\n";
                if (race != "")
                    bracketString += race + " ";
                bracketString += mmr;
            }

            return bracketString;
        }

        private ErrorCode UpdateLadderFromEntries(List<BeefEntry> entries, bool backup) {
            // Update
            if (backup)
                _backupManager.Backup(_entries);

            // Notify listeners
            LadderChanged.Invoke(_entries);

            return ErrorCode.Success;
        }

        /// <summary>
        /// Gets the BeefEntry corresponding to the given name or rank.
        /// </summary>
        /// <param name="nameOrRank">A Beef Name or rank on the ladder (RANK not INDEX)</param>
        /// <param name="entries">The list of current entries to check</param>
        /// <returns>Returns the entry that was found or null</returns>
        private BeefEntry GetBeefEntryFromNameOrRank(String nameOrRank, List<BeefEntry> entries) {
            int rank;
            if (!int.TryParse(nameOrRank, out rank)) rank = -1;

            if (rank == -1) {
                foreach (BeefEntry entry in entries) {
                    if (entry.PlayerName.Equals(nameOrRank)) {
                        return entry;
                    }
                }
                return null; // Not found
            } else {
                int index = rank - 1;
                if (index < 0 || index >= entries.Count) {
                    return null;
                }

                return entries[index];
            }            
        }
    }
}
