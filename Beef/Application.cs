using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Beef {
    class Application {
        private BeefConfig _config;
        private String _botPrefix;
        private PresentationManager _manager;
        private String[] _leaderRoles;
        private String _exePath;

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
        }

        public async Task Run() {
            _manager = new PresentationManager(_config, _exePath + "/Backups");

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

                if (!_manager.Authenticate().Ok()) {
                    Console.WriteLine("Could not authenticate.");
                    return;
                }

                if (arguments.Length == 4) {
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
                            code = _manager.ReportWin(winningRank, losingRank);
                        } else if (winningRank == -1 && losingRank != -1) {
                            code = _manager.ReportWin(winningPlayer, losingRank);
                        } else {
                            code = _manager.ReportWin(winningPlayer, losingPlayer);
                        }

                        if (code.Ok())
                            MessageChannel(channel, "The Ladder has been updated.").GetAwaiter().GetResult();
                    } else if (arguments[1] == "rename") {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        code = _manager.RenamePlayer(arguments[2], arguments[3]);
                        if (code.Ok())
                            MessageChannel(channel, "**" + arguments[2] + "** has been renamed to **" + arguments[3] + "**").GetAwaiter().GetResult();
                    }
                } else if (arguments.Length == 2) {
                    if (arguments[1] == "bracket" || arguments[1] == "list" || arguments[1] == "ladder") {
                        IsLeader(userInput.Author);

                        List<BeefEntry> entries = _manager.ReadBracket();

                        if (entries.Count == 0)
                            code = ErrorCode.CouldNotReadTheLadder;
                        else {
                            code = ErrorCode.Success;

                            // Print bracket
                            String bracket = "";
                            foreach (BeefEntry entry in entries) {
                                bracket += entry.PlayerRank + ". " + entry.PlayerName + "\n";
                            }
                            MessageChannel(channel, bracket).GetAwaiter().GetResult();
                        }
                    } else if (arguments[1].Equals("undo")) {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        code = _manager.Undo();
                        if (code.Ok()) {
                            MessageChannel(channel, "Undid what you should have done undid 10 minutes ago.").GetAwaiter().GetResult();
                        }
                    } else if (arguments[1].Equals("quit") || arguments[1].Equals("exit")) {
                        if (!IsLeader(author)) {
                            MessageChannel(channel, "You don't have permission to do that.").GetAwaiter().GetResult();
                            return;
                        }

                        code = ErrorCode.Success;
                    }
                }

                if (arguments.Length >= 2 && arguments[1] == "help") {
                    code = ErrorCode.Success;

                    String help = "";
                    help += "The Beef ladder is maintained on Google Docs as a Slide presentation.  This bot makes it more convenient to update it.  Each Beef command is prefixed with \"%beef%\".\n";
                    help += "The following commands are available:\n";
                    help += "\t **%beef% help** - DMs this message to the user who typed it\n";
                    help += "\t **%beef% help all** - Sends this message to the current channel\n";
                    help += "\t **%beef% list**. - Prints out the current ranks of everyone in the ladder.\n";
                    help += "\t **%beef% ladder**. - Same as %beef% list\n";
                    help += "\t **%beef% bracket**. - Same as %beef% list\n";
                    help += "\nThe following commands are admin only:\n";
                    help += "\t **%beef% _<WinningPlayerOrRank>_ beat _<LosingPlayerOrRank>_** - Updates the ladder such that the winning player is placed in the losing player's position and everyone inbetween is shuffled to the right by one.\n";
                    help += "\t\t\t You can specifiy a rank or a name for each player.  Names are case sensitive.  Examples:\n";
                    help += "\t\t\t\t **%beef% 4 beat 3**.  --  Will swap the player in rank 4 with player in rank 3.\n";
                    help += "\t\t\t\t **%beef% 4 beat 1**.  --  Will put the rank 4 player in rank 1, the rank 1 player in rank 2, and the rank 3 player in rank 4\n";
                    help += "\t\t\t\t **%beef% bum beat GamerRichy**.  --  Will put bum in rank 1, GamerRichy in rank 2, and shuffle everyone else accordingly.\n";
                    help += "\t **%beef% _<WinningPlayerOrRank>_ beats _<LosingPlayerOrRank>_**. - Same as %beef% X beat Y (It accepts beats and beat)\n";
                    help += "\t **%beef% rename _<OldPlayerName>_ _<NewPlayerName>_**. - Renames a player on the ladder to the new name.\n";
                    help += "\t **%beef% undo**. - Undoes the last change to the ladder (renames, wins, etc..).\n";
                    help = help.Replace("%beef%", _botPrefix + "beef");

                    // Send the help message to all if requested.  Otherwise
                    // just DM it to the user that asked.
                    if (arguments.Length >= 3 && arguments[2] == "all")
                        MessageChannel(channel, help).GetAwaiter().GetResult();
                    else
                        MessageUser(userInput.Author, help).GetAwaiter().GetResult();
                }

                if (!code.Ok()) {
                    MessageErrorToChannel(channel, code);
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

        private async Task MessageChannel(ISocketMessageChannel channel, String message) {
            Console.WriteLine(message);
            await channel.SendMessageAsync(message);
        }

        private async Task MessageUser(SocketUser user, String message) {
            Console.WriteLine("To " + user.Username + ": " + message);
            await user.SendMessageAsync(message);
        }

        private async Task MessageErrorToChannel(ISocketMessageChannel channel, ErrorCode code) {
            String errorMessage = code.GetUserMessage();
            Console.WriteLine(errorMessage);
            await channel.SendMessageAsync(errorMessage);
        }
    }
}
