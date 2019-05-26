using System;

namespace Beef {
    class Application {
        public void Run() {
            PresentationManager manager = new PresentationManager("credentials.json");
            if (!manager.Authenticate()) {
                Console.WriteLine("Could not authenticate.");
            }

            if (!manager.ReportWin("newguy", "enro")) {
                Console.WriteLine("Failed to report the win.");
            }

            Console.ReadLine();
        }
    }
}
