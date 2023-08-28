using System;

namespace ispc_languageserver
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            App app = new App();
            app.StartAsync().Wait();
        }
    }
}