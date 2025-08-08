using System;

namespace ispc_languageserver
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Handle command line arguments before any service initialization
            // to prevent hanging when just checking --help
            if (args.Length > 0)
            {
                foreach (var arg in args)
                {
                    if (arg == "--help" || arg == "-h")
                    {
                        Console.WriteLine("ISPC Language Server");
                        Console.WriteLine("Usage: dotnet ispc_languageserver.dll");
                        Console.WriteLine("This is a Language Server Protocol implementation for ISPC.");
                        Environment.Exit(0); // Force immediate exit
                    }
                    else if (arg == "--version" || arg == "-v")
                    {
                        Console.WriteLine("ISPC Language Server v1.2.0");
                        Environment.Exit(0); // Force immediate exit
                    }
                }
            }

            Console.Error.WriteLine("[ispc] - Starting language server");

            try
            {
                App app = new App();
                await app.StartAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL] Language server crashed: {ex.Message}");
                Console.Error.WriteLine($"[FATAL] Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}