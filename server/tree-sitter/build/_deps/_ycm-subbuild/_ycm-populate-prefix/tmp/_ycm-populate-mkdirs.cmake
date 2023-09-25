# Distributed under the OSI-approved BSD 3-Clause License.  See accompanying
# file Copyright.txt or https://cmake.org/licensing for details.

cmake_minimum_required(VERSION 3.5)

file(MAKE_DIRECTORY
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-src"
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-build"
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix"
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix/tmp"
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix/src/_ycm-populate-stamp"
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix/src"
  "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix/src/_ycm-populate-stamp"
)

set(configSubDirs Debug)
foreach(subDir IN LISTS configSubDirs)
    file(MAKE_DIRECTORY "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix/src/_ycm-populate-stamp/${subDir}")
endforeach()
if(cfgdir)
  file(MAKE_DIRECTORY "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/_deps/_ycm-subbuild/_ycm-populate-prefix/src/_ycm-populate-stamp${cfgdir}") # cfgdir has leading slash
endif()
