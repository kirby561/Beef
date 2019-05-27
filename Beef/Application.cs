using System;
using System.Collections.Generic;

namespace Beef {
    class Application {
        private String m_BotIndicator = ".";
        private PresentationManager _manager;
        private Boolean _shouldRun = true;

        public void Run() {
            // TODO: 
            //  1) Proper error messaging and messaging in general
            //  2) Command interpretation
            //  3) Discord integration/server
            //  4) Auto bracket backup
            //  5) Bracket restore from backup
            //  6) Undo/Redo
            //  7) Specify bracket in config file.

            _manager = new PresentationManager("credentials.json");
            
            String nextLine;
            while (_shouldRun) {
                nextLine = Console.ReadLine();
                HandleCommand(nextLine);
            }
        }

        private void HandleCommand(String userInput) {
            String[] arguments = ParseArguments(userInput);
            if (arguments.Length == 0) {
                Console.WriteLine("Invalid number of arguments.");
                return;
            }

            if (arguments[0] == ".beef") {
                if (!_manager.Authenticate()) {
                    Console.WriteLine("Could not authenticate.");
                    return;
                }

                if (arguments.Length == 4) {
                    if (arguments[2] == "beat" || arguments[2] == "beats") {
                        String winningPlayer = arguments[1];
                        String losingPlayer = arguments[3];

                        // Check if they're ranks or names
                        int winningRank;
                        if (!int.TryParse(winningPlayer, out winningRank)) winningRank = -1;

                        int losingRank;
                        if (!int.TryParse(losingPlayer, out losingRank)) losingRank = -1;

                        if (winningRank != -1 && losingRank != -1) {
                            _manager.ReportWin(winningRank, losingRank);
                        } else if (winningRank == -1 && losingRank != -1) {
                            _manager.ReportWin(winningPlayer, losingRank);
                        } else {
                            _manager.ReportWin(winningPlayer, losingPlayer);
                        }
                    } else if (arguments[1] == "rename") {
                        _manager.RenamePlayer(arguments[2], arguments[3]);
                    }
                } else if (arguments.Length == 2) {
                    if (arguments[1] == "bracket" || arguments[1] == "list") {
                        List<BeefEntry> entries = _manager.ReadBracket();

                        // Print bracket
                        foreach (BeefEntry entry in entries) {
                            Console.WriteLine("\t" + entry.PlayerRank + ". " + entry.PlayerName);
                        }
                    }
                }
            } else if (arguments[0].Equals("quit") || arguments[0].Equals("exit")) {
                _shouldRun = false;
            }
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
    }
}
