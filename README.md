# vscode-languageserver-csharp-example

This repository contains a sample language server implemented in C#
and a sample VS code extension that demonstrates an extension that runs the server.

The extension observes all 'plaintext' documents (documents from all editors not associated with a language)
and uses the server to provide validation and completion proposals.

The code for the extension is in the 'client' folder. It uses the 'vscode-languageclient' node module to launch the language server.

The language server is located in the 'server' folder. 

# Modification from the original
Sample language server is implemented in C#.

# How to run locally
* The build process uses CMake and MSBuild. Make sure MSBuild is accessible on $PATH
* Run `cmake ..` from a build directory off root.
* `cd ../client-vscode` to move the client folder.
* `npm install` to initialize the extension.
* open `client` folder in VS Code.
* 'Ctrl+Shift+B' to build the client and watch files.
* In the Debug viewlet, run 'Launch Extension' from drop-down to launch the extension and attach to the extension.
* create a file `test.ispc`, and type `test`. You should see a validation error.
* to debug the server use external debugger such as Visual Studio.
