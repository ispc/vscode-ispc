# Language extension for ISPC, the Intel&reg; SPMD Program Compiler

## This extension provides

* Syntax highlighting
* Auto completion of ISPC standard library functions
* Function parameter help for standard library functions
* Real-time file validation and problem (error/warning) reporting

These features are activated in files with the extensions "\*.ispc" and '\*.isph'.

## Installation notes

* macOS&reg; and Linux users will need to have the Mono Runtime installed.
* Be sure to edit ispc.compilerPath and provide the fully qualified path to the compiler executable.
* Version 1.1.0 and above requires ISPC v1.13.0 or greater if using the edit time validation feature.

## For more information

* [ISPC Downloads](http://ispc.github.io/downloads.html)
* [ISPC Documentation](http://ispc.github.io/)
* [ISPC Github Repo](https://github.com/ispc/ispc)
* [ISPC Users - Google Group](https://groups.google.com/forum/#!forum/ispc-users)

## Release Notes:

### 1.2.0

* Added support for breakpoints and debugging with C/C++ Extension and MSVC on Windows
* Added Sapphire Rapids as option for Compiler CPU and Target
* Removed autosurround for '<' and '>'
* Fix for repreated crashes when compiler is not found

### 1.1.0

* Added support for remote editing and GitHub Codespaces.  Currently this extension is defined as a "ui" extension, and does not run on the server.
* Added new keywords to the grammar file for new language keywords and types like int8, uint8, int16, uint16, int32, uint32, etc.
* Updated the available standard library functions.
* Added a Target OS setting.
* Added a CPU setting.
* Updated the availble choices for the compiler architecture setting.
* Allow the extension to function without a fully qualified path for the compiler if the compiler is in %PATH%
* Fix for an extremely rare crash in the server on shutdown.

### 1.0.0

* Initial release.

### Authors:
Pete Brubaker, Intel&reg;
Ethan Davis, Intel&reg;

pete.brubaker@intel.com

[@pbrubaker](https://twitter.com/pbrubaker)