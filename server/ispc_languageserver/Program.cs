using LanguageServer.Infrastructure.JsonDotNet;
using LanguageServer.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISPCLanguageServer
{
    class Program
    {
        static bool useNamedPipes = false;

        static void Main(string[] args)
        {
            // first check if we should use named pipes
            foreach ( string arg in args )
            {
                if ( arg.ToLower() == "--usenamedpipes")
                {
                    useNamedPipes = true;
                }
            }

            Stream input, output;

            if ( useNamedPipes )
            {
                var stdInPipeName = @"input";
                var stdOutPipeName = @"output";

                var pipeAccessRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(pipeAccessRule);

                var inputPipe = new NamedPipeClientStream(stdInPipeName);
                var outputPipe = new NamedPipeClientStream(stdOutPipeName);

                inputPipe.Connect();
                outputPipe.Connect();

                input = inputPipe;
                output = outputPipe;
            }
            else
            {
                input = Console.OpenStandardInput();
                output = Console.OpenStandardOutput();
            }


            Console.OutputEncoding = new UTF8Encoding(); // UTF8N for non-Windows platform
            var app = new App(input, output);
            Logger.Instance.Attach(app);
            try
            {
                app.Listen().Wait();
            }
            catch (AggregateException ex)
            {
                Console.Error.WriteLine(ex.InnerExceptions[0]);
                Environment.Exit(-1);
            }
        }
    }
}
