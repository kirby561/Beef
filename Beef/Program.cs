using System;

namespace Beef {
    class Program {
         static void Main(string[] args) {
            String exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            Application app = new Application(exePath);
            app.Run().GetAwaiter().GetResult();
        }
    }
}