namespace Beef {
    class Program {
         static void Main(string[] args) {
            Application app = new Application();
            app.Run().GetAwaiter().GetResult();
        }
    }
}