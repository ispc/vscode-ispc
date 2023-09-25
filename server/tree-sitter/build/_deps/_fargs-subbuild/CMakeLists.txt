# Distributed under the OSI-approved BSD 3-Clause License.  See accompanying
# file Copyright.txt or https://cmake.org/licensing for details.

cmake_minimum_required(VERSION 3.26.4)

# We name the project and the target for the ExternalProject_Add() call
# to something that will highlight to the user what we are working on if
# something goes wrong and an error message is produced.

project(_fargs-populate NONE)



include(ExternalProject)
ExternalProject_Add(_fargs-populate
                     "UPDATE_DISCONNECTED" "False" "DOWNLOAD_EXTRACT_TIMESTAMP" "FALSE" "URL" "https://github.com/polysquare/cmake-forward-arguments/archive/refs/tags/v1.0.0.zip"
                    SOURCE_DIR          "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_fargs-src"
                    BINARY_DIR          "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_fargs-build"
                    CONFIGURE_COMMAND   ""
                    BUILD_COMMAND       ""
                    INSTALL_COMMAND     ""
                    TEST_COMMAND        ""
                    USES_TERMINAL_DOWNLOAD  YES
                    USES_TERMINAL_UPDATE    YES
                    USES_TERMINAL_PATCH     YES
)


