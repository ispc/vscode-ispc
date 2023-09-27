/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */
// tslint:disable
"use strict";

import * as path from "path";

import {
    workspace,
    Disposable,
    ExtensionContext,
    languages,
    TextDocument,
    Position,
    CancellationToken,
    Definition,
    Location,
    Range,
    ProviderResult,
    window
} from "vscode";
import {
    LanguageClient,
    LanguageClientOptions,
    SettingMonitor,
    ServerOptions,
    TransportKind,
    InitializeParams,
    StreamInfo,
    createServerPipeTransport,
} from "vscode-languageclient/node";
import { Trace, createClientPipeTransport } from "vscode-jsonrpc/node";
import { createConnection } from "net";

export function activate(context: ExtensionContext) {
    // The server is implemented in node
    let serverExe = "dotnet";

    // Construct the path to the ispc_languageserver.dll
    const extensionPath = context.extensionPath;
    const serverDllPath = path.join(extensionPath, 'server', 'ispc_languageserver.dll');

    // If the extension is launched in debug mode then the debug server options are used
    // Otherwise the run options are used
    let serverOptions: ServerOptions = {
        // run: { command: serverExe, args: ['-lsp', '-d'] },
        run: {
            command: serverExe,
            args: [serverDllPath],
            transport: TransportKind.pipe,
        },
        // debug: { command: serverExe, args: ['-lsp', '-d'] }
        debug: {
            command: serverExe,
            args: [serverDllPath],
            transport: TransportKind.pipe,
            runtime: "",
        },
    };

    // Options to control the language client
    let clientOptions: LanguageClientOptions = {
        // Register the server for ispc documents
        documentSelector: [
            {
                pattern: "**/*.ispc",
            },
        ],
        progressOnInitialization: true,
        synchronize: {
            // Synchronize the setting section 'ispc' to the server
            configurationSection: "ispc",
            fileEvents: workspace.createFileSystemWatcher("**/*.ispc"),
        },
    };

    // Create the language client and start the client.
    const client = new LanguageClient("ispc", "ISPC Language Server", serverOptions, clientOptions);
    client.registerProposedFeatures();
    client.trace = Trace.Verbose;
    let disposable = client.start();

    context.subscriptions.push(registerDefinitionProvider(client));
    // Push the disposable to the context's subscriptions so that the
    // client can be deactivated on extension deactivation
    context.subscriptions.push(disposable);
}

function registerDefinitionProvider(client: LanguageClient): Disposable {
	return languages.registerDefinitionProvider(
		{ scheme: 'file', language: 'ispc' },
		{
			provideDefinition(
				document: TextDocument,
				position: Position,
				token: CancellationToken
			): ProviderResult<Definition> {
				return client.sendRequest<Location[]>(
					'textDocument/definition',
					client.code2ProtocolConverter.asTextDocumentPositionParams(document, position),
					token
				).then((locations: Location[]) => {
                    const [firstLocation] = locations;
                    const uri = firstLocation.uri;
                    const range = firstLocation.range;

                    window.showTextDocument(uri, {
                        selection: range
                    });

                    return locations;
				});
			}
		}
	)
}

