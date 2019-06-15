﻿using Google.Apis.Auth.OAuth2;
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
        private List<BeefEntry> _entries = new List<BeefEntry>();
        private Presentation _presentation;
        private IList<Request> _requestList = new List<Request>();
        private BackupManager _backupManager;

        public PresentationManager(BeefConfig config, String backupLocation) {
            _credentialFile = config.GoogleApiCredentialFile;
            _backupManager = new BackupManager(backupLocation);
            _presentationId = config.GoogleApiPresentationId;
            _applicationName = config.GoogleApiApplicationName;
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
            _presentation = request.Execute();
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
            for (int i = loserIndex; i <= winnerIndex; i++) {
                // If the winner wasn't on the bracket, then we want to bail out here
                if (i == entries.Count)
                    break;

                // Delete whoever's there
                AddDeleteRequest(entries[i]);

                // If it's the winner, add them
                if (i == loserIndex) {
                    AddInsertRequest(entries[i], winnerName);
                } else {
                    // Otherwise shuffle down the list.
                    AddInsertRequest(entries[i], entries[i - 1].PlayerName);
                }
            }

            // Submit the requests
            return SubmitRequests();
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

            BeefEntry playerToRename = entries[index];
            ErrorCode result = RenamePlayer(playerToRename, newName);
     
            if (result.Ok())
                existingPlayer = playerToRename;

            return result;
        }

        /// <summary>
        /// Renames the given entry to the new name.
        /// </summary>
        /// <param name="existingEntry">The existing entry in the ladder.</param>
        /// <param name="newName">The new name to give the player</param>
        /// <returns>Returns Success or what went wrong if it failed.</returns>
        private ErrorCode RenamePlayer(BeefEntry existingEntry, String newName) {
            // Remove the existing and add the new one
            AddDeleteRequest(existingEntry);
            AddInsertRequest(existingEntry, newName);

            return SubmitRequests();
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

            playerToRemove = entries[index];
            ErrorCode result = RemovePlayer(entries, playerToRemove);
            if (result.Ok()) {
                playerToRemove = entries[index];
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
            // Add a delete request for each player and insert the player one rank above
            // so it shuffles everyone up the bracket leaving the last one black.
            for (int index = playerToRemove.PlayerRank - 1; index < entries.Count; index++) {
                AddDeleteRequest(entries[index]);

                if (index + 1 < entries.Count) {
                    AddInsertRequest(entries[index], entries[index + 1].PlayerName);
                } else {
                    AddInsertRequest(entries[index], "");
                }
            }

            return SubmitRequests();
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
                AddDeleteRequest(currentBracket[i]);
                AddInsertRequest(currentBracket[i], backedUpEntries[i].PlayerName);
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
                            String numberInBeefStr = GetStringFromElements(textElements);
                            int rank = -1;
                            if (Int32.TryParse(numberInBeefStr, out rank)) {
                                // We have the number, now get the player name, if any
                                if (children[0]?.Shape != null) {
                                    IList<TextElement> playerElements = children[0].Shape?.Text?.TextElements;
                                    String playerName = GetStringFromElements(playerElements);

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

            // Sort just in case the order of the elements isn't the same
            // as the labeled rank in each.
            _entries.Sort();
            
            return _entries;
        }

        /// <summary>
        /// Submits all the queued requests in one batch and indicates if it succeeded or not.
        /// </summary>
        /// <param name="backup">True to backup before running the commands. False otherwise.</param>
        /// <returns>Returns true if it succeeded, false otherwise.  (TODO: Make it actually return false if it fails)</returns>
        public ErrorCode SubmitRequests(bool backup = true) {
            // Backup first
            if (backup)
                _backupManager.Backup(_entries);

            var requestListUpdate = new BatchUpdatePresentationRequest();
            requestListUpdate.Requests = _requestList;

            PresentationsResource.BatchUpdateRequest batchRequest = _service.Presentations.BatchUpdate(requestListUpdate, _presentationId);
            BatchUpdatePresentationResponse response = batchRequest.Execute();

            // ?? TODO: How to detect an error here?
            _requestList.Clear();
            return ErrorCode.Success;
        }

        /// <summary>
        /// Adds the given entry to the list to be deleted in the next SubmitRequests call.
        /// </summary>
        /// <param name="entry">The entry to erase.</param>
        private void AddDeleteRequest(BeefEntry entry) {
            if (String.IsNullOrEmpty(entry.PlayerName))
                return; // Already deleted

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
        /// <param name="playerName">The name of the player to add.</param>
        private void AddInsertRequest(BeefEntry entry, String playerName) {
            InsertTextRequest insertRequest = new InsertTextRequest();
            insertRequest.InsertionIndex = 0;
            insertRequest.Text = playerName;
            insertRequest.ObjectId = entry.ObjectId;

            var actualRequest = new Request();
            actualRequest.InsertText = insertRequest;
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
                String content = element?.TextRun?.Content?.Trim();
                if (content != null)
                    result += content;
            }

            return result;
        }
    }
}
