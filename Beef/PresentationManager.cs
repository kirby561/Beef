using Google.Apis.Auth.OAuth2;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Beef.MmrReader;

using Range = Google.Apis.Slides.v1.Data.Range;

namespace Beef {
    /// <summary>
    /// Handles authentication and communication with the Google Slides API
    /// to make changes to the bracket.
    /// //?? TODO: Every return false in here should return a code that identifies what happened or log to a stream that's passed in so it can report what the problem is.
    /// </summary>
    class PresentationManager {
        private String[] _scopes = { SlidesService.Scope.Presentations };
        private String _applicationName;
        private String _credentialFile;
        private String _presentationId;

        private SlidesService _service;
        private List<BeefEntry> _backupEntries = new List<BeefEntry>(); // Keep the original so we know what changes need to be submitted.
        private List<BeefEntry> _entries = new List<BeefEntry>();
        private Presentation _presentation;
        private IList<Request> _requestList = new List<Request>();
        private BackupManager _backupManager;
        private Dictionary<String, Tuple<ProfileInfo, LadderInfo>> _mmrDictionary;

        /// <summary>
        /// Creates a PresentationManager with the given information.
        /// </summary>
        /// <param name="presentationId">A Google presentation ID containing a Google slide in the correct beef ladder format.</param>
        /// <param name="credentialFile">A credential file for the Google API</param>
        /// <param name="applicationName">The name of the Application registered with the Google API</param>
        /// <param name="backupLocation">The location to store backups of the ladder.</param>
        public PresentationManager(String presentationId, String credentialFile, String applicationName, String backupLocation) {
            _presentationId = presentationId;
            _credentialFile = credentialFile;
            _applicationName = applicationName;
            _backupManager = new BackupManager(backupLocation);            
        }

        /// <summary>
        /// Authenticates and opens the presentation.
        /// </summary>
        /// <returns>Returns false if authentication failed or the presentation couldn't be opened.</returns>
        public ErrorCode Authenticate() {
            UserCredential credential;

            using (var stream = new FileStream(_credentialFile, FileMode.Open, FileAccess.Read)) {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    _scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            // Create Google Slides API service.
            _service = new SlidesService(new BaseClientService.Initializer() {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName,
            });

            // Define request parameters.
            PresentationsResource.GetRequest request = _service.Presentations.Get(_presentationId);

            // Prints all the beef participants
            try {
                _presentation = request.Execute();
            } catch (Exception ex) {
                Console.WriteLine("Error when authenticating with the presentation API.");
                Console.WriteLine(ex.Message);
            }

            if (_presentation != null)
                return ErrorCode.Success;

            return ErrorCode.AuthenticationFailed;
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
            return UpdatePresentationFromEntries(entries, true);
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
                existingPlayer = _backupEntries[index];

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
            return UpdatePresentationFromEntries(_entries, true);
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
            // Assumes the beef ladder has already been updated
            /*
            List<BeefEntry> newList = new List<BeefEntry>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++) {
                if (i == (player1.PlayerRank - 1)) {
                    newList.Add(player2);
                } else if (i == (player2.PlayerRank - 1)) {
                    newList.Add(player1);
                } else {
                    newList.Add(_entries[i]);
                }
            }

            int player1PrevRank = player1.PlayerRank;
            String player1PrevObjectId = player1.
            player1.PlayerRank = player2.PlayerRank;
            player2.PlayerRank = player1PrevRank;

            return UpdatePresentationFromEntries(newList, true);
            */

            String player1Name = player1.PlayerName;
            String player2Name = player2.PlayerName;
            player1.PlayerName = player2Name;
            player2.PlayerName = player1Name;

            return UpdatePresentationFromEntries(_entries, true);
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

            return UpdatePresentationFromEntries(entries, true);
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
            String[] backups = _backupManager.GetLatestBackups(1);
            if (backups == null || backups.Length < 1)
                return ErrorCode.NothingToUndo;

            List<BeefEntry> backedUpEntries = _backupManager.LoadBackup(backups[0]);
            if (backedUpEntries.Count != currentBracket.Count)
                return ErrorCode.LadderDifferentSize;

            // Prepare the changes
            for (int i = 0; i < backedUpEntries.Count; i++) {
                AddDeleteRequest(currentBracket[i], i);

                String newName = GetBracketStringFromEntry(backedUpEntries[i]);
                currentBracket[i].PlayerName = newName;
                AddInsertRequest(currentBracket[i], i);
            }

            // Do the restore
            ErrorCode code = SubmitRequests(false);
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
        /// Reads the current bracket and sorts it by rank.
        /// </summary>
        /// <returns></returns>
        public List<BeefEntry> ReadBracket() {
            _backupEntries.Clear();
            _entries.Clear();

            IList<Page> slides = _presentation.Slides;
            for (var i = 0; i < slides.Count; i++) {
                Page slide = slides[i];
                IList<PageElement> elements = slide.PageElements;
                foreach (PageElement element in elements) {
                    // Check if this is a player entry. 
                    // A group of any 2 things with the second entry an integer is assumed to be a player entry.
                    if (element?.ElementGroup?.Children?.Count == 2) {
                        // Check that the second one is a number
                        IList<PageElement> children = element.ElementGroup.Children;
                        if (children[1]?.Shape?.Text?.TextElements?.Count > 0) {
                            IList<TextElement> textElements = children[1].Shape.Text.TextElements;
                            String numberInBeefStrElements = GetStringFromElements(textElements);
                            String[] beefNumberParts = numberInBeefStrElements.Split('\n');
                            if (beefNumberParts == null || beefNumberParts.Length == 0)
                                continue;

                            String numberInBeefStr = beefNumberParts[0].Trim();

                            int rank = -1;
                            if (Int32.TryParse(numberInBeefStr, out rank)) {
                                // We have the number, now get the player name, if any
                                if (children[0]?.Shape != null) {
                                    IList<TextElement> playerElements = children[0].Shape?.Text?.TextElements;
                                    String playerEntry = GetStringFromElements(playerElements);
                                    String[] lines = playerEntry.Split('\n');
                                    if (lines?.Length > 0) {
                                        String playerName = lines[0];
                                        var entry = new BeefEntry();
                                        entry.ObjectId = children[0].ObjectId;
                                        entry.PlayerName = playerName;
                                        entry.PlayerRank = rank;
                                        _entries.Add(entry);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Sort just in case the order of the elements isn't the same
            // as the labeled rank in each.
            _entries.Sort();

            // Copy the list so when we modify it we have a backup copy to compare to
            foreach (BeefEntry entry in _entries) {
                _backupEntries.Add(new BeefEntry(entry));
            }
            
            return _entries;
        }

        /// <summary>
        /// Submits all the queued requests in one batch and indicates if it succeeded or not.
        /// </summary>
        /// <param name="backup">True to backup the entries before submitting the request.</param>
        /// <returns>Returns ErrorCode.Success if it succeeded, or an error code otherwise.</returns>
        public ErrorCode SubmitRequests(bool backup) {
           ErrorCode result = ErrorCode.Success;
           var requestListUpdate = new BatchUpdatePresentationRequest();
            requestListUpdate.Requests = _requestList;

            if (backup)
                _backupManager.Backup(_backupEntries);

            try {
                PresentationsResource.BatchUpdateRequest batchRequest = _service.Presentations.BatchUpdate(requestListUpdate, _presentationId);
                BatchUpdatePresentationResponse response = batchRequest.Execute();
            } catch (Exception e) {
                result = ErrorCode.RequestException;
                Console.WriteLine(e.ToString());
            }
            
            _requestList.Clear();
            return result;
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

            return UpdatePresentationFromEntries(currentBracket, false);
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

        private ErrorCode UpdatePresentationFromEntries(List<BeefEntry> entries, bool backup) {
            // Make the list of changes
            for (int i = 0; i < entries.Count; i++) {
                BeefEntry entry = entries[i];

                AddDeleteRequest(entry, i);
                AddInsertRequest(entry, i);
            }

            // Update
            ErrorCode code = SubmitRequests(backup);
            if (!code.Ok())
                return code;

            return ErrorCode.Success;
        }

        /// <summary>
        /// Adds the given entry to the list to be deleted in the next SubmitRequests call.
        /// </summary>
        /// <param name="entry">The entry to erase.</param>
        private void AddDeleteRequest(BeefEntry entry, int beefIndex) {
            if (String.IsNullOrEmpty(entry.PlayerName) && String.IsNullOrEmpty(_backupEntries[beefIndex].PlayerName)) {
                return;
            }

            if (String.IsNullOrEmpty(_backupEntries[beefIndex].PlayerName)) {
                return; // Nothing to do
            }

            DeleteTextRequest deleteRequest = new DeleteTextRequest();
            deleteRequest.TextRange = new Range();
            deleteRequest.TextRange.Type = "ALL";
            deleteRequest.ObjectId = entry.ObjectId;
            
            var actualRequest = new Request();
            actualRequest.DeleteText = deleteRequest;
            _requestList.Add(actualRequest);
        }

        /// <summary>
        /// Adds a request to add the given playerName to the given beef slot the next time SubmitRequests is called.
        /// </summary>
        /// <param name="entry">The beef slot to add the player name in.</param>
        private void AddInsertRequest(BeefEntry entry, int beefIndex) {
            String bracketString = GetBracketStringFromEntry(entry);

            if (String.IsNullOrEmpty(bracketString))
                return;

            InsertTextRequest insertRequest = new InsertTextRequest();
            insertRequest.InsertionIndex = 0;
            insertRequest.Text = bracketString;
            insertRequest.ObjectId = entry.ObjectId;

            var actualRequest = new Request();
            actualRequest.InsertText = insertRequest;
            _requestList.Add(actualRequest);

            // Bold the name if there's an MMR too
            int newLineIndex = bracketString.IndexOf("\n");
            if (newLineIndex >= 0) {
                AddBoldRequest(entry, true, 0, newLineIndex);
                AddBoldRequest(entry, false, newLineIndex, bracketString.Length);
            } else if (bracketString.Length > 0) {
                AddBoldRequest(entry, true, 0, bracketString.Length);
            }
        }

        private void AddBoldRequest(BeefEntry entry, bool bold, int startIndex, int endIndex) {
            UpdateTextStyleRequest boldRequest = new UpdateTextStyleRequest();
            boldRequest.ObjectId = entry.ObjectId;
            boldRequest.Fields = "bold";
            Range textRange = new Range();
            textRange.Type = "FIXED_RANGE";
            textRange.StartIndex = startIndex;
            textRange.EndIndex = endIndex;
            boldRequest.TextRange = textRange;

            TextStyle boldStyle = new TextStyle();
            boldStyle.Bold = bold;

            boldRequest.Style = boldStyle;

            var actualRequest = new Request();
            actualRequest.UpdateTextStyle = boldRequest;
            _requestList.Add(actualRequest);
        }
        
        /// <summary>
        /// Squashes a list of text elements into a single string and removes the spacing.
        /// </summary>
        /// <param name="elements">The list of elements</param>
        /// <returns>Returns the squashed string or an empty string if there are no elements.</returns>
        private String GetStringFromElements(IList<TextElement> elements) {
            // If there are no string elements there is no string
            if (elements == null)
                return "";

            // Combine all the elments.  Player names can't be too complicated so it's most likely white space anyway.
            String result = "";
            foreach (TextElement element in elements) {
                String content = element?.TextRun?.Content;
                if (content != null)
                    result += content;
            }

            return result;
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
