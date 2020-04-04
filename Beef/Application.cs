using Beef.MmrReader;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Beef {
    class Application : ProfileInfoProvider, MmrListener {
        private readonly String _version = "1.4.1";
        private BeefConfig _config;
        private String _botPrefix;
        private BeefUserConfigManager _userManager;
        private PresentationManager _presentationManager;
        private String[] _leaderRoles;
        private String _exePath;
        private MmrReader.MmrReader _mmrReader;
        private DispatcherSynchronizationContext _mainContext;

        private DiscordSocketClient _discordClient;

        public Application(BeefConfig config, String exePath) {
            _exePath = Directory.GetParent(exePath).FullName;
            _discordClient = new DiscordSocketClient();

            _discordClient.Log += LogAsync;
            _discordClient.Ready += ReadyAsync;
            _discordClient.MessageReceived += MessageReceivedAsync;
            _discordClient.Disconnected += DisconnectedAsync;

            _config = config;
            _botPrefix = config.BotPrefix;
            _leaderRoles = config.LeaderRoles;

            _presentationManager = new PresentationManager(_config, _exePath + "/Backups");
            _userManager = new BeefUserConfigManager(_exePath + "/Users");

            if (SynchronizationContext.Current == null || !(SynchronizationContext.Current is DispatcherSynchronizationContext)) {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            }
            _mainContext = SynchronizationContext.Current as DispatcherSynchronizationContext;

            _mmrReader = new MmrReader.MmrReader(_config.MmrReaderConfig);
            _mmrReader.StartThread(this, this);
        }

        public async Task Run() {
            // Log in to discord
            String token = _config.DiscordBotToken;
            await _discordClient.LoginAsync(TokenType.Bot, token);
            await _discordClient.StartAsync();

            // Block until someone tells us to quit
            await Task.Delay(-1);
        }

        /// <summary>
        /// Checks if the given user is a Battle Cruiser, Executor (Mod), or Ghost (Leader)
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <returns>True if they have the are priviledged</returns>
        private Boolean IsLeader(SocketUser socketUser) {
            var user = socketUser as SocketGuildUser;

            if (user != null) {
                foreach (SocketRole role in user.Roles) {
                    foreach (String leaderRole in _leaderRoles) {
                        if (leaderRole.Equals(role.Name)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void HandleCommand(SocketMessage userInput) {
            // This is for Cloud...
            if (userInput.Content.StartsWith("I love you Beef bot!", StringComparison.CurrentCultureIgnoreCase)) {
                MessageChannel(userInput.Channel, "I love you too babe.").GetAwaiter().GetResult();
                return;
            }

            // Make sure it starts with ".beef" as an optimization
            if (String.IsNullOrEmpty(userInput.Content) || !userInput.Content.StartsWith(_botPrefix + "beef"))
                return;

            String[] arguments = ParseArguments(userInput.Content);
            if (arguments.Length == 0) {
                return;
            }

            if (arguments[0] == _botPrefix + "beef") {
                SocketUser author = userInput.Author;
                ISocketMessageChannel channel = userInput.Channel;
                ErrorCode code = ErrorCode.CommandNotRecognized;

                if (!_presentationManager.Authenticate().Ok()) {
                    Console.WriteLine("Could not authenticate.");
                    return;
                }

                if (arguments.Length == 1) {
                    // Just print the ladder link
                    code = ErrorCode.Success;
                    String sc2Beef = "<:sc2:527907155815432202> <:beef:530066963209256973>";
                    String beefLink = sc2Beef + " **Settle the Beef** " + sc2Beef + "\n";
                    beefLink += _config.BeefLadderLink;
                    MessageChannel(channel, beefLink).GetAwaiter().GetResult();
                } else if (arguments[1] == "register") {
                    if (!IsLeader(author)) {
                        MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                        return;
                    }

                    if (arguments.Length == 4) {
                        String beefName = arguments[2];
                        String discordName = arguments[3];

                        // Make sure the names are valid
                        if (beefName.Contains("#") || !discordName.Contains("#")) {
                            MessageChannel(channel, "The first name should be the beef name, the second name should be the Discord ID.  Try **" + _botPrefix + "beef help**").GetAwaiter().GetResult();
                            return;
                        }

                        // Make sure it doesn't exist
                        BeefUserConfig existingUser = _userManager.GetUserByName(beefName);
                        if (existingUser != null) {
                            MessageChannel(channel, "That beef name is already registered with the discord name **" + existingUser.DiscordName + "**.").GetAwaiter().GetResult();
                            return;
                        }

                        existingUser = _userManager.GetUserByDiscordId(discordName);
                        if (existingUser != null) {
                            MessageChannel(channel, "That discord name is already registered with the beef name **" + existingUser.BeefName + "**.").GetAwaiter().GetResult();
                            return;
                        }

                        // Ok everything seems legit
                        code = _userManager.RegisterUser(beefName, discordName);

                        if (code.Ok())
                            MessageChannel(channel, "Registered **" + beefName + "** with discord name **" + discordName + "**.").GetAwaiter().GetResult();
                    } else {
                        MessageChannel(channel, "Uh, that's not how this command is used.  No wonder people talk about you when you're not around.  Try **" + _botPrefix + "beef help**").GetAwaiter().GetResult();
                        return;
                    }
                } else if (arguments[1] == "unregister") {
                    if (!IsLeader(author)) {
                        MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                        return;
                    }

                    if (arguments.Length == 3) {
                        String beefName = arguments[2];

                        // Make sure it doesn't exist
                        BeefUserConfig existingUser = _userManager.GetUserByName(beefName);
                        if (existingUser == null) {
                            MessageChannel(channel, "**" + beefName + "** is not registered.").GetAwaiter().GetResult();
                            return;
                        }

                        code = _userManager.DeleteUser(beefName);

                        if (code.Ok())
                            MessageChannel(channel, "Unregistered **" + beefName + "** (Discord name **" + existingUser.DiscordName + "**).").GetAwaiter().GetResult();
                    } else {
                        MessageChannel(channel, "Uh, that's not how this command is used.  No wonder people talk about you when you're not around.  Try **" + _botPrefix + "beef help**").GetAwaiter().GetResult();
                        return;
                    }
                } else if (arguments[1] == "link") {
                    if (!IsLeader(author)) {
                        MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                        return;
                    }

                    if (arguments.Length == 4) {
                        String beefName = arguments[2];
                        String battleNetAccountUrl = arguments[3];

                        // Make sure it doesn't exist
                        BeefUserConfig existingUser = _userManager.GetUserByName(beefName);
                        if (existingUser == null) {
                            MessageChannel(channel, $"No user by the name of **{beefName}** found.").GetAwaiter().GetResult();
                            return;
                        }
                        
                        // Ok everything seems legit
                        code = _userManager.LinkUserToBattleNetAccount(beefName, battleNetAccountUrl);

                        // Do an immediate update so the player can see their mmr right away
                        _mmrReader.RequestUpdate();

                        if (code.Ok())
                            MessageChannel(channel, "Linked **" + beefName + "** to account **" + battleNetAccountUrl + "**.").GetAwaiter().GetResult();
                    } else {
                        MessageChannel(channel, "You weren't even close.  Try **" + _botPrefix + "beef help**").GetAwaiter().GetResult();
                        return;
                    }
                } else if (arguments[1] == "unlink") {
                    if (!IsLeader(author)) {
                        MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                        return;
                    }

                    if (arguments.Length == 3) {
                        String beefName = arguments[2];

                        // Make sure it doesn't exist
                        BeefUserConfig existingUser = _userManager.GetUserByName(beefName);
                        if (existingUser == null) {
                            MessageChannel(channel, $"No user by the name of **{beefName}** found.").GetAwaiter().GetResult();
                            return;
                        }

                        String battleNetAccountUrl = existingUser.ProfileInfo.GetBattleNetAccountUrl();

                        // Ok everything seems legit
                        code = _userManager.UnlinkUserFromBattleNetAccount(beefName);

                        // Do an immediate update so the update is seen right away
                        _mmrReader.RequestUpdate();

                        if (code.Ok())
                            MessageChannel(channel, "Unlinked **" + beefName + "** from account **" + battleNetAccountUrl + "**.").GetAwaiter().GetResult();
                    } else {
                        MessageChannel(channel, "You weren't even close.  Try **" + _botPrefix + "beef help**").GetAwaiter().GetResult();
                        return;
                    }
                } else if (arguments[1] == "users") {
                    bool sendToAll = arguments.Length == 3 && arguments[2] == "all";
                    List<BeefUserConfig> users = _userManager.GetUsers();
                    String userList;

                    if (users.Count > 0) {
                        userList = "";
                        foreach (BeefUserConfig config in users) {
                            userList += config.BeefName + " -> " + config.DiscordName;

                            if (config.ProfileInfo != null) {
                                userList += " -> <" + config.ProfileInfo.GetBattleNetAccountUrl() + ">\n";
                            } else {
                                userList += "\n";
                            }
                        }
                        userList.Remove(userList.Length - 1, 1); // Remove the last "\n"
                    } else {
                        userList = "There are no registered users.";
                    }

                    if (sendToAll) {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        MessageChannel(channel, userList).GetAwaiter().GetResult();
                    } else {
                        MessageUser(userInput.Author, userList).GetAwaiter().GetResult();
                    }

                    code = ErrorCode.Success;
                } else if (arguments[1] == "challenge") {
                    if (arguments.Length != 3) {
                        MessageChannel(channel, "You need to specify who you want to challenge.  You'd think that would have been obvious...").GetAwaiter().GetResult();
                        return;
                    }

                    BeefUserConfig challengersConfig = _userManager.GetUserByDiscordId(userInput.Author.Username + "#" + userInput.Author.Discriminator);
                    if (challengersConfig == null) {
                        MessageChannel(channel, "You aren't registered yet.  Get an admin to register your discord ID to your beef ladder name with the register command.").GetAwaiter().GetResult();
                        return;
                    }

                    BeefUserConfig challengedConfig = null;
                    String challengedDiscordOrBeefName = arguments[2];

                    if (challengedDiscordOrBeefName.Contains("#")) {
                        challengedConfig = _userManager.GetUserByDiscordId(challengedDiscordOrBeefName);
                    } else {
                        challengedConfig = _userManager.GetUserByName(challengedDiscordOrBeefName);
                    }

                    if (challengedConfig == null) {
                        MessageChannel(channel, "I don't recognize that opponent.  Make sure they are on the ladder and have their discord name is registered.  See the list with **" + _botPrefix + "beef users**").GetAwaiter().GetResult();
                        return;
                    }

                    // Challenge
                    IssueBeefChallenge(challengersConfig, challengedConfig);
                    code = ErrorCode.Success;
                } else if (arguments.Length == 4) {
                    if (arguments[2] == "beat" || arguments[2] == "beats") {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        String winningPlayer = arguments[1];
                        String losingPlayer = arguments[3];

                        // Check if they're ranks or names
                        int winningRank;
                        if (!int.TryParse(winningPlayer, out winningRank)) winningRank = -1;

                        int losingRank;
                        if (!int.TryParse(losingPlayer, out losingRank)) losingRank = -1;

                        if (winningRank != -1 && losingRank != -1) {
                            code = _presentationManager.ReportWin(winningRank, losingRank);
                        } else if (winningRank == -1 && losingRank != -1) {
                            code = _presentationManager.ReportWin(winningPlayer, losingRank);
                        } else {
                            code = _presentationManager.ReportWin(winningPlayer, losingPlayer);
                        }

                        if (code.Ok())
                            MessageChannel(channel, "The Ladder has been updated.").GetAwaiter().GetResult();
                    } else if (arguments[1] == "rename") {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        String playerOrRankToRename = arguments[2];
                        String newName = arguments[3];
                        int rank;
                        if (!int.TryParse(playerOrRankToRename, out rank)) rank = -1;

                        if (rank != -1) {
                            BeefEntry existingPlayer;
                            code = _presentationManager.RenamePlayer(rank - 1, newName, out existingPlayer);

                            if (existingPlayer != null) {
                                playerOrRankToRename = existingPlayer.PlayerName;
                                if (String.IsNullOrEmpty(playerOrRankToRename)) {
                                    playerOrRankToRename = "Rank " + rank;
                                }
                            }
                        } else {
                            code = _presentationManager.RenamePlayer(playerOrRankToRename, newName);
                        }

                        if (code.Ok()) {
                            // We want to rename any registered users too
                            BeefUserConfig existingUser = _userManager.GetUserByName(playerOrRankToRename);
                            if (existingUser != null) {
                                _userManager.ModifyUser(existingUser.BeefName, newName, existingUser.DiscordName);
                            }

                            MessageChannel(channel, "**" + playerOrRankToRename + "** has been renamed to **" + newName + "**").GetAwaiter().GetResult();
                        }
                    }
                } else if (arguments.Length >= 2) {
                    if (arguments[1] == "version" && arguments.Length == 2) {
                        MessageChannel(channel, "BeefBot version " + _version).GetAwaiter().GetResult();
                        return;
                    } else if (arguments[1] == "remove") {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        if (arguments.Length != 3) {
                            MessageChannel(channel, "Read the instructions for once in your life, that's not how you use this command.").GetAwaiter().GetResult();
                            return;
                        }

                        String playerToRemove = arguments[2];
                        int rank;
                        if (!int.TryParse(playerToRemove, out rank)) rank = -1;

                        if (rank != -1) {
                            BeefEntry removedPlayerEntry;
                            code = _presentationManager.RemovePlayer(rank - 1, out removedPlayerEntry);

                            if (code.Ok())
                                playerToRemove = removedPlayerEntry.PlayerName;
                        } else {
                            code = _presentationManager.RemovePlayer(playerToRemove);
                        }

                        if (code.Ok()) {
                            MessageChannel(channel, "Removed **" + playerToRemove + "** from the ladder.").GetAwaiter().GetResult();
                        }
                    } else if (arguments[1] == "bracket" || arguments[1] == "list" || arguments[1] == "ladder") {
                        IsLeader(userInput.Author);

                        List<BeefEntry> entries = _presentationManager.ReadBracket();

                        if (entries.Count == 0)
                            code = ErrorCode.CouldNotReadTheLadder;
                        else {
                            code = ErrorCode.Success;

                            // Print bracket
                            String bracket = "";
                            foreach (BeefEntry entry in entries) {
                                bracket += entry.PlayerRank + ". " + entry.PlayerName + "\n";
                            }
                            // Send the help message to all if requested.  Otherwise
                            // just DM it to the user that asked.
                            if (arguments.Length >= 3 && arguments[2] == "all")
                                MessageChannel(channel, bracket).GetAwaiter().GetResult();
                            else
                                MessageUser(userInput.Author, bracket).GetAwaiter().GetResult();
                        }
                    } else if (arguments[1].Equals("undo")) {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        code = _presentationManager.Undo();
                        if (code.Ok()) {
                            MessageChannel(channel, "Undid what you should have done undid 10 minutes ago.").GetAwaiter().GetResult();
                        }
                    } else if (arguments[1].Equals("quit") || arguments[1].Equals("exit")) {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        code = ErrorCode.Success;
                    } else if (arguments[1] == "help") {
                        code = ErrorCode.Success;

                        String help = "";
                        help += "The Beef ladder is maintained on Google Docs as a Slide presentation.  This bot makes it more convenient to update it.  Each Beef command is prefixed with \"%beef%\".\n";
                        help += "Note, commands that have parameters in them surrounded in [] means that parameter is optional.  As an example \"all\" is a common suffix you can add to make a command print to the chat instead of whispering the response to you.\n";
                        help += "The following commands are available:\n";
                        help += "\t **%beef%** - Prints the link to the ladder.\n";
                        help += "\t **%beef% help [all]** - Prints this message to the user who typed it or the channel if [all] is specified.\n";
                        help += "\t **%beef% list [all]** - Prints out the current ranks of everyone in the ladder to the user who typed it or the current channel if [all] is specified.\n";
                        help += "\t **%beef% ladder [all]** - Same as %beef% list\n";
                        help += "\t **%beef% bracket [all]** - Same as %beef% list\n";
                        help += "\t **%beef% challenge <PlayerLadderNameOrDiscordName>** - Challenges the player to Settle the Beef!  Each player must register their discord account first (see an admin and the beef register command).  This will @ each player in each settle-the-beef channel they are in across all servers.\n";
                        help += "\t **%beef% users [all]** - Prints all the users who have been registered with **%beef% register**\n";
                        help += "\nThe following commands are admin only:\n";
                        help += "\t **%beef% _<WinningPlayerOrRank>_ beat _<LosingPlayerOrRank>_** - Updates the ladder such that the winning player is placed in the losing player's position and everyone inbetween is shuffled to the right by one.\n";
                        help += "\t\t\t You can specifiy a rank or a name for each player.  Names are case sensitive.  Examples:\n";
                        help += "\t\t\t\t **%beef% 4 beat 3**  --  Will swap the player in rank 4 with player in rank 3.\n";
                        help += "\t\t\t\t **%beef% 4 beat 1**  --  Will put the rank 4 player in rank 1, the rank 1 player in rank 2, and the rank 3 player in rank 4\n";
                        help += "\t\t\t\t **%beef% bum beat GamerRichy**  --  Will put bum in rank 1, GamerRichy in rank 2, and shuffle everyone else accordingly.\n";
                        help += "\t **%beef% _<WinningPlayerOrRank>_ beats _<LosingPlayerOrRank>_**. - Same as %beef% X beat Y (It accepts beats and beat)\n";
                        help += "\t **%beef% rename _<OldPlayerName>_ _<NewPlayerName>_**. - Renames a player on the ladder to the new name.\n";
                        help += "\t **%beef% remove _<PlayerOrRank>_** - Removes the given player or rank from the ladder..\n";
                        help += "\t **%beef% register <PlayerLadderName> <PlayerDiscordName#1234>** - Registers the given ladder name with the given discord name for use with the challenge command.\n";
                        help += "\t **%beef% unregister <PlayerLadderName>** - Unregisters the given ladder name.\n";
                        help += "\t **%beef% link <PlayerLadderName> <PlayerBattleNetProfileLink>** - Links the given ladder name to the given Battle.net profile link.  This will enable their best MMR and race to be displayed on the ladder.\n";
                        help += "\t **%beef% unlink <PlayerLadderName>** - Unlinks the given ladder name from their associated Battle.net Profile.\n";
                        help += "\t **%beef% undo** - Undoes the last change to the ladder (renames, wins, etc..).\n";
                        help += "\t **%beef% version** - Prints the version of BeefBot\n";
                        help = help.Replace("%beef%", _botPrefix + "beef");

                        // Send the help message to all if requested.  Otherwise
                        // just DM it to the user that asked.
                        if (arguments.Length >= 3 && arguments[2] == "all")
                            MessageChannel(channel, help).GetAwaiter().GetResult();
                        else
                            MessageUser(userInput.Author, help).GetAwaiter().GetResult();
                    }
                }

                if (!code.Ok()) {
                    MessageErrorToChannel(channel, code).GetAwaiter().GetResult();
                }
            } // end beef
        }

        private Task LogAsync(LogMessage log) {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the bot is ready.
        /// </summary>
        /// <returns>An async task.</returns>
        private Task ReadyAsync() {
            Console.WriteLine($"{_discordClient.CurrentUser} is connected!");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the bot disconnected.
        /// </summary>
        /// <param name="ex">The exception that happened</param>
        /// <returns>An async task.</returns>
        private Task DisconnectedAsync(Exception ex) {
            Console.WriteLine($"{ex.Message} is disconnected!");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a message is received from discord.
        /// </summary>
        /// <param name="message">The message that was sent.</param>
        /// <returns>An async task.</returns>
        private async Task MessageReceivedAsync(SocketMessage message) {
            // The bot should never respond to itself.
            if (message.Author.Id == _discordClient.CurrentUser.Id)
                return;

            HandleCommand(message);
        }

        /// <summary>
        /// Parses the command into an array of separate arguments and recognizes things in quotes.
        /// It does not do anything fancy and should probably be replaced by a more generic parser.
        /// </summary>
        /// <param name="command">A command line string</param>
        /// <returns>Returns an array with each argument of the command split out</returns>
        private static String[] ParseArguments(string command) {
            char[] parmChars = command.ToCharArray();
            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++) {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }
            return new string(parmChars).Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void IssueBeefChallenge(BeefUserConfig challenger, BeefUserConfig challenged) {
            IReadOnlyCollection<SocketGuild> guilds = _discordClient.Guilds;

            // Find the bot
            SocketGuild bot = null;
            foreach (SocketGuild guild in guilds) {
                if (guild.CurrentUser.IsBot) {
                    bot = guild;
                    break;
                }
            }

            if (bot == null) {
                Console.WriteLine("Somethings wrong.  There are no bots in the list of guilds!");
                return;
            }

            String challengerDiscordName = challenger.DiscordName;
            String challengedDiscordName = challenged.DiscordName;

            // Find the beef channel
            List<Task> tasks = new List<Task>();
            foreach (SocketTextChannel channel in bot.TextChannels) {
                if (channel.Name == "settle-the-beef") {
                    // Check if any of the users we're challenging are in the channel
                    foreach (SocketUser user in channel.Users) {
                        String userId = user.Username + "#" + user.Discriminator;
                        if (userId.Equals(challenged.DiscordName)) {
                            challengedDiscordName = user.Mention;
                        } else if (userId.Equals(challenger.DiscordName)) {
                            challengerDiscordName = user.Mention;
                        }
                    }

                    String message = challengerDiscordName + " (" + challenger.BeefName + ") has challenged " + challengedDiscordName + " (" + challenged.BeefName + ") to Settle the Beef!";
                    tasks.Add(channel.SendMessageAsync(message));
                }
            }
            
            foreach (Task task in tasks)
                task.GetAwaiter().GetResult();
        }

        private async Task MessageChannel(ISocketMessageChannel channel, String message) {
            Console.WriteLine(message);

            List<String> messages = GetBrokenUpMessages(message);

            foreach (String messageSection in messages) {
                await channel.SendMessageAsync(messageSection);
            }
        }

        private List<String> GetBrokenUpMessages(String message) {
            // Messages can't be more than 2000 characters
            // Do 1000 just to be safe if it doesn't count 2 byte characters or something
            int charsLeft = message.Length;
            int lastIndex = 0;
            List<String> messages = new List<String>();
            while (charsLeft > 1000) {
                int nextIndex = lastIndex + 1000;

                // Go see if there's a newline within 100 characters and split there if so
                for (int i = 0; i < 200; i++) {
                    char c = message[nextIndex - i];
                    if (c == '\n') {
                        nextIndex = nextIndex - i;
                        break;
                    }
                }

                int numberOfChars = nextIndex - lastIndex;
                messages.Add(message.Substring(lastIndex, numberOfChars));
                lastIndex = nextIndex;
                charsLeft -= numberOfChars;
            }
            messages.Add(message.Substring(lastIndex));

            return messages;
        }

        private async Task MessageUser(SocketUser user, String message) {
            Console.WriteLine("To " + user.Username + ": " + message);

            List<String> messages = GetBrokenUpMessages(message);

            foreach (String messageSection in messages) {
                await user.SendMessageAsync(messageSection);
            }
        }

        private async Task MessageErrorToChannel(ISocketMessageChannel channel, ErrorCode code) {
            String errorMessage = code.GetUserMessage(_botPrefix);
            Console.WriteLine(errorMessage);
            await channel.SendMessageAsync(errorMessage);
        }

        public List<ProfileInfo> GetLadderUsers() {
            List<BeefUserConfig> users = _userManager.GetUsersCopy();
            List<ProfileInfo> profileInfoList = new List<ProfileInfo>();

            foreach (BeefUserConfig user in users) {
                if (user.ProfileInfo != null)
                    profileInfoList.Add(user.ProfileInfo);
            }
            
            return profileInfoList;
        }

        public void OnMmrRead(List<Tuple<ProfileInfo, LadderInfo>> mmrList) {
            _mainContext.Post((_) => {
                if (!_presentationManager.Authenticate().Ok()) {
                    Console.WriteLine("Could not authenticate when reading MMR.");
                    return;
                }

                // Make the list a dictionary so we can quickly lookup MMR
                var profileIdToMaxMmrDict = new Dictionary<String, Tuple<ProfileInfo, LadderInfo>>();
                foreach (Tuple<ProfileInfo, LadderInfo> entry in mmrList) {
                    BeefUserConfig user = _userManager.GetUserByProfileId(entry.Item1.ProfileId);

                    // If the user was removed they can be null
                    if (user == null)
                        continue;

                    profileIdToMaxMmrDict.Add(user.BeefName, entry);
                }

                _presentationManager.UpdateMmrDictionary(profileIdToMaxMmrDict);

                Console.WriteLine($"Read MMR.  Users:");
                foreach (Tuple<ProfileInfo, LadderInfo> entry in mmrList) {
                    if (entry.Item2 != null)
                        Console.WriteLine(entry.Item1.ProfileId + ": " + entry.Item2.Mmr + " as " + entry.Item2.Race);
                    else {
                        Console.WriteLine(entry.Item1.ProfileId + ": " + "No ladder info");
                    }
                }
            }, null);
        }
    }
}
