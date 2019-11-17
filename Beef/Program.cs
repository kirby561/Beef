using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Beef {
    class Program {
         static void Main(string[] args) {
            String exePath = System.Reflection.Assembly.GetEntryAssembly().Location;

            // Check for a config file
            String parentDirectory = Directory.GetParent(exePath).FullName;
            BeefConfig config = ConfigFileManager.ReadConfigFile(parentDirectory + "/config.json");
            if (config == null) {
                Console.WriteLine("You need a file called \"config.json\" in the same directory as your executable.  Modify the config.json.example file with your credentials and bot information and rename it to config.json.");
                return;
            }

            Application app = new Application(config, exePath);
            Task appRunLoop = app.Run();

            // Make a dispatcher frame to execute the events
            var frame = new DispatcherFrame();
            Dispatcher.PushFrame(frame);

            // We'll never get here
            appRunLoop.GetAwaiter().GetResult();
        }
    }
}