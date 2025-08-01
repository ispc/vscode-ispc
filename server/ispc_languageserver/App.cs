﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace ispc_languageserver
{
    internal class App
    {
        public App()
        {
        }

        public async Task StartAsync()
        {
            var server = await LanguageServer.From(options => ConfigureServer(options)).ConfigureAwait(false);
            await server.WaitForExit.ConfigureAwait(false);
        }

        private static void ConfigureServer(LanguageServerOptions options)
        {
            Log.Logger = new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                        .MinimumLevel.Verbose()
                        .CreateLogger();

            options
               .WithInput(Console.OpenStandardInput())
               .WithOutput(Console.OpenStandardOutput())
               .ConfigureLogging(
                    x => x
                        .AddSerilog(Log.Logger)
                        .AddLanguageProtocolLogging()
                        .SetMinimumLevel(LogLevel.Debug)
                )
               .WithHandler<TextDocumentHandler>()
               .WithHandler<DidChangeWatchedFilesHandler>()
               .WithHandler<DefinitionHandler>()
               .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
               .WithServices(
                    services =>
                    {
                        services
                            .AddSingleton<ICompiler, Compiler>()
                            .AddSingleton<IAbstractSyntaxTreeManager, AbstractSyntaxTreeManager>()
                            .AddSingleton<ITextDocumentManager, TextDocumentManager>()
                            .Configure<IspcSettings>("ispc")
                            ;
                    }
               )
               .WithConfigurationSection("ispc")
               .OnInitialized(
                    async (server, request, response, token) =>
                    {
                        await Console.Error.WriteLineAsync("[ispc] - Initilized");
                    }
                )
               .OnStarted(
                    async (languageServer, token) =>
                    {
                        var compiler = languageServer.GetService<ICompiler>();
                        if(compiler != null)
                            compiler.Initialize();
                        var treeParser = languageServer.GetService<IAbstractSyntaxTreeManager>();
                        if(treeParser != null)
                            treeParser.Initialize();
                        await Console.Error.WriteLineAsync("[ispc] - Server Started");
                    }
                );
        }

    }
}
