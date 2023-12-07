using System;

namespace ispc_languageserver
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            App app = new App();
            await app.StartAsync();
        }
    }
}