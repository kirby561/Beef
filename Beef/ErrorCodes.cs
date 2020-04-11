using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beef {
    public enum ErrorCode {
        // Successful
        Success, 

        // Authentication
        AuthenticationFailed,

        // Win Reporting
        LoserRankInvalid,
        DuplicateWinnerEntriesWithSameName,
        DuplicateLoserEntriesWithSameName,
        CouldNotReadTheLadder,
        WinnerCantBeHigherThanLoser,
        WinnerRankInvalid,
        YouCantBeatYourself,
        LoserIsNotOnTheLadder,

        // Renaming/Removing
        NoExistingPlayerByThatName,
        DuplicatePlayerNameWhenRenaming,

        // Removing
        RankNotOnLadder,

        // Undo
        NothingToUndo,
        LadderDifferentSize,
        CouldNotRevertBackupFile,

        // Switching
        Player1DoesNotExist,
        Player2DoesNotExist,

        // Commands
        CommandNotRecognized,

        // Registration
        DiscordNameExists,
        BeefNameAlreadyExists,
        BeefNameDoesNotExist,
        BeefNameContainsInvalidCharacters,
        BeefFileAlreadyExists, // This shouldnt happen if there isn't also a name collision.
        BeefFileCouldNotSerialize, // What??
        BeefFileCouldNotWriteFile,
        CouldNotDeleteFile,

        // Linking
        InvalidBattleNetUrlFormat,

        // Presentation API
        RequestException,

    }

    public static class ErrorCodeMethods {
        public static Boolean Ok(this ErrorCode code) {
            return code == ErrorCode.Success;
        }

        public static String GetUserMessage(this ErrorCode code, String botPrefix) {
            switch (code) {
                case ErrorCode.Success: 
                    return "All good.";
                case ErrorCode.AuthenticationFailed:
                    return "Yo we couldn\'t authenticate with the server.  Is your internet on?  Is your bot token right?  Do you even know what that is?  What a scrub.";
                case ErrorCode.LoserRankInvalid:
                    return "That loser rank doesn\'t even exist, wtf?  Are you high?";
                case ErrorCode.DuplicateWinnerEntriesWithSameName:
                    return "The ladder's got 2 of those winner names.  We celebrate diversity here.  Please use a unique name for each person.";
                case ErrorCode.DuplicateLoserEntriesWithSameName:
                    return "The ladder's got 2 of those loser names.  We celebrate diversity here.  Please use a unique name for each person.";
                case ErrorCode.CouldNotReadTheLadder:
                    return "Ugh I can't read the ladder.  Is the presentation ID right?  Do you have internet?  I don't know man.  I give up.";
                case ErrorCode.WinnerCantBeHigherThanLoser:
                    return "Bro.  Think about what you just typed.  You just asked me to move the winner of the beef to the loser's spot.  How does that even make sense?  Maybe I'll do it next time just to make you feel bad.";
                case ErrorCode.WinnerRankInvalid:
                    return "That winner rank is interesting.  Know what's interesting about it?  It doesn't exist on the ladder.  Have you tried actually reading the ladder before modifying it?";
                case ErrorCode.YouCantBeatYourself:
                    return "Stop hitting yourself.";
                case ErrorCode.LoserIsNotOnTheLadder:
                    return "That loser isn't even on the ladder.  Is this the level of proficiency you have in other things in life too?";
                case ErrorCode.NoExistingPlayerByThatName:
                    return "How can I rename a player that doesn't exist?  Seriously, check the ladder.  That name's not on it.";
                case ErrorCode.DuplicatePlayerNameWhenRenaming:
                    return "There's already a player with that name on the ladder.  I bet it's you.  You're so bad you want there to be some ambiguity when you lose.";
                case ErrorCode.RankNotOnLadder:
                    return "Someone can't read numbers.  Pick a rank that actually is on the ladder.";
                case ErrorCode.NothingToUndo:
                    return "You haven't done anything yet wtf!?";
                case ErrorCode.LadderDifferentSize:
                    return "Okok I'll give.  This one is my bad.  I can't restore a backup that's a different size than the current ladder because that's way too much work.";
                case ErrorCode.CommandNotRecognized:
                    return "Not a command bro.  Try \"**" + botPrefix + "beef help**\" so you have some clue what you're doing next time.";
                case ErrorCode.CouldNotRevertBackupFile:
                    return "There was a problem moving the undone backup file to the archive.  This indicates a problem with the file system.";
                case ErrorCode.DiscordNameExists:
                    return "That discord name is already registered.";
                case ErrorCode.BeefNameAlreadyExists:
                    return "That beef name is already registered.";
                case ErrorCode.BeefFileAlreadyExists:
                    return "The beef file already exists!";
                case ErrorCode.BeefNameDoesNotExist:
                    return "That beef name does not exist.";
                case ErrorCode.BeefNameContainsInvalidCharacters:
                    return "That beef name contains invalid characters!";
                case ErrorCode.BeefFileCouldNotSerialize:
                    return "I couldn't serialize the beef file.";
                case ErrorCode.BeefFileCouldNotWriteFile:
                    return "I couldn't write the beef file for some reason!";
                case ErrorCode.CouldNotDeleteFile:
                    return "I couldn't delete the file for some reason!";
                default:
                    return "Well I don't know what happened but it can't be good.";
            }
        }
    }
}
