cmake_minimum_required(VERSION 3.20)
project(ispc.tree-sitter)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED True)

add_library(ispc.tree-sitter SHARED src/parser.c)
target_include_directories(ispc.tree-sitter PRIVATE src)

add_custom_command(
    TARGET ispc.tree-sitter
    POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_FILE:ispc.tree-sitter>
            ../../client-vscode/server
    COMMENT "Copying ispc.tree_sitter.dll to destination directory"
)