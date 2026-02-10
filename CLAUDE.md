# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a VS Code language extension for ISPC (Intel® Implicit SPMD Program Compiler), featuring:
- Syntax highlighting for `.ispc` and `.isph` files
- Real-time validation and diagnostics via Language Server Protocol
- Auto-completion and signature help for ISPC standard library
- Integration with tree-sitter for AST parsing

The project consists of three main components:
1. **VS Code Client** (TypeScript): `client-vscode/` - Extension activation and LSP client
2. **Language Server** (C#/.NET 8): `server/ispc_languageserver/` - LSP server implementation
3. **Tree-Sitter Grammar**: `ispc.tree-sitter/` - ISPC syntax parser

## Build System

The build process follows the GitHub Actions workflow (`.github/workflows/build.yml`).

### Complete Build (Recommended)

This is the standard build process used in CI/CD:

```bash
# 1. Build native components (C# language server, tree-sitter)
mkdir build
cd build
cmake ..

# This single CMake command will:
# - Restore NuGet packages for C# language server
# - Build the language server with MSBuild (Windows) or Make (Linux/macOS)
# - Initialize tree-sitter submodule if needed
# - Build tree-sitter library
# - Build ispc.tree-sitter grammar
# - Copy server DLL to client-vscode/server/ (via PostBuild event)

# 2. Install npm dependencies (use 'npm ci' for clean install)
cd ../client-vscode
npm ci  # or 'npm install' for development

# 3. Compile TypeScript
npm run compile

# 4. (Optional) Package extension
npx vsce package --no-git-tag-version
```

### Development Workflow

**Quick TypeScript Changes:**
```bash
cd client-vscode

# Watch mode for TypeScript development
npm run compile  # or: tsc -p ./ --watch
```

**Rebuild Everything (After C# or Native Changes):**
```bash
# Clean rebuild
rm -rf build
mkdir build
cd build
cmake ..

# Then recompile TypeScript
cd ../client-vscode
npm run compile
```

**Language Server Only (Advanced):**
```bash
# Note: Building the language server directly with dotnet may fail due to
# PostBuild event expecting $(SolutionDir) variable. Use CMake instead.

# If you must build manually (not recommended):
cd server
dotnet build ispc_languageserver.sln -c Release

# Then manually copy to client folder:
cp -r ispc_languageserver/bin/Release/net8.0/* ../client-vscode/server/
```

**Tree-Sitter Grammar Changes:**
- Grammar source: `ispc.tree-sitter/grammar.js`
- Rebuild with CMake as shown above
- Native library auto-copies to `client-vscode/server/`

### Running the Extension

**In VS Code (for debugging):**
1. Open `client-vscode/` folder in VS Code
2. Press `Ctrl+Shift+B` to start TypeScript watch task
3. Press `F5` or use Debug view → "Launch Extension"
4. In Extension Development Host, create `test.ispc` file to test

**As Packaged Extension:**
```bash
cd client-vscode
npx vsce package --no-git-tag-version
# Install the generated .vsix file in VS Code
```

## Architecture

### Language Server (C# Component)

**Entry Point:** `server/ispc_languageserver/Program.cs`
- Initializes the LSP server via `App.cs`

**Key Components:**

1. **App.cs** - Server bootstrap and dependency injection
   - Configures OmniSharp Language Server
   - Registers handlers: TextDocument, Completion, SignatureHelp, DidChangeWatchedFiles
   - Sets up Serilog logging to `log.txt`
   - Wires up services: `ICompiler`, `IAbstractSyntaxTreeManager`, `ITextDocumentManager`

2. **TextDocumentHandler.cs** - Document lifecycle management
   - Handles open/close/change/save events
   - Synchronizes document state with `TextDocumentManager`
   - Clears diagnostics when documents close
   - Forces revalidation on save

3. **Compiler.cs** - ISPC compiler integration for diagnostics
   - Spawns background thread (`CompilerProc`) to process document queue
   - Invokes ISPC compiler with stdin/stdout pipes
   - Parses compiler output via regex to extract diagnostics
   - Publishes diagnostics (errors/warnings) to VS Code
   - Configuration: `compilerPath`, `compilerTarget`, `compilerArchitecture`, `compilerCPU`, `compilerTargetOS`

4. **AbstractSyntaxTreeManager.cs** - Tree-sitter integration
   - P/Invokes into `ispc.tree-sitter` native library via `binding.cs`
   - Parses documents to AST for semantic analysis
   - Extracts definitions (identifiers) using tree queries
   - Used for code navigation features

5. **TextDocumentManager.cs** - Document state management
   - Maintains dictionary of open documents with AST and definitions
   - Manages compile queue for validation
   - Thread-safe document access

6. **CompletionHandler.cs** / **SignatureHelpHandler.cs**
   - Provides IntelliSense for ISPC standard library functions
   - Reads embedded resource `ispc_stdlib.txt`

### VS Code Client (TypeScript Component)

**Entry Point:** `client-vscode/src/extension.ts`
- `activate()` function starts the language client
- Configures server executable: `dotnet <extensionPath>/server/ispc_languageserver.dll`
- Registers document selectors: `**/*.ispc`, `**/*.isph`
- Synchronizes `ispc.*` configuration to server

### Document Flow

```
User edits .ispc file
  → VS Code client (extension.ts)
    → Language Server: TextDocumentHandler.Handle(DidChangeTextDocumentParams)
      → TextDocumentManager.Change() updates document state
      → Document enqueued to compiler queue
        → Compiler background thread processes queue
          → Spawns ISPC compiler process with document as stdin
          → Parses compiler stderr/stdout for diagnostics
          → Publishes diagnostics back to VS Code
      → AbstractSyntaxTreeManager.ParseDocument() (if needed)
        → Builds AST via tree-sitter
        → Extracts definitions for IntelliSense
```

### Configuration

All settings are prefixed with `ispc.` in VS Code settings:
- `ispc.compilerPath` - Path to ISPC compiler executable (default: "ispc" in PATH)
- `ispc.compilerTarget` - Target ISA (e.g., "avx2-i32x8", "sse4", "neon")
- `ispc.compilerArchitecture` - CPU arch (x86, x86-64, arm, aarch64, xe64)
- `ispc.compilerCPU` - Specific CPU model (e.g., "sapphirerapids", "skylake")
- `ispc.compilerTargetOS` - OS target (windows, linux, macos, etc.)
- `ispc.maxNumberOfProblems` - Max diagnostics to display
- `ispc.trace.server` - LSP trace verbosity

## Debugging

**Debug Language Server (C#):**
- Attach external debugger (Visual Studio, VS Code C# extension, or Rider)
- Language server runs as `dotnet` child process spawned by VS Code extension
- Log file: `client-vscode/log.txt` (Serilog output)

**Debug VS Code Extension (TypeScript):**
- Launch configuration in `client-vscode/.vscode/launch.json`
- Use Debug view → "Launch Extension"

## Dependencies

**Server (C#):**
- .NET 8.0 SDK
- OmniSharp Language Server Protocol libraries
- Tree-sitter ISPC grammar (native library)
- Serilog for logging

**Client (TypeScript):**
- VS Code Engine: ^1.82.0
- vscode-languageclient: ^9.0.1
- TypeScript: ^5.8.3

**Native:**
- Tree-sitter (C library) as git submodule in `tree-sitter/`
- ISPC Tree-sitter grammar as submodule/source in `ispc.tree-sitter/`

## Platform Notes

**Windows:**
- Requires MSBuild on PATH (via VS Build Tools or Visual Studio)
- CMake generates Visual Studio solution files

**macOS/Linux:**
- Requires Mono Runtime to run .NET executables (if using older .NET, .NET 8 is cross-platform)
- CMake uses Make

## Extension Requirements

- **All platforms**: ISPC compiler must be installed and accessible
  - Set `ispc.compilerPath` to fully qualified path if not in PATH
  - Download from: http://ispc.github.io/downloads.html
  - Minimum version: v1.13.0+ (for validation features)

## Common Issues

**"Compiler not found" crashes:**
- Ensure `ispc.compilerPath` points to valid ISPC executable
- Test with: `ispc --version`

**Diagnostics not appearing:**
- Check `ispc.trace.server` setting is enabled
- Review `client-vscode/log.txt` for errors
- Verify document is saved as `.ispc` or `.isph`

**Tree-sitter native library errors:**
- Ensure CMake build completed successfully
- Check `ispc.tree-sitter` built library is in expected location
- Native library name: `ispc.tree-sitter.dll` (Windows) / `ispc.tree-sitter.so` (Linux) / `ispc.tree-sitter.dylib` (macOS)
