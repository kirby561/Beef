using System;

namespace Beef {
    class Application {
        public void Run() {
            // TODO: 
            //  1) Proper error messaging and messaging in general
            //  2) Command interpretation
            //  3) Discord integration/server
            //  4) Auto bracket backup
            //  5) Bracket restore from backup
            //  6) Undo/Redo

            PresentationManager manager = new PresentationManager("credentials.json");
            if (!manager.Authenticate()) {
                Console.WriteLine("Could not authenticate.");
            }

            if (!manager.RenamePlayer("gamerrichy2", "gamerrichy"))
                Console.WriteLine("Rename failed");

            Console.ReadLine();
        }
    }
}
