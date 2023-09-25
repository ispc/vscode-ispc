cmake_minimum_required(VERSION 3.12)
project(ispc.tree-sitter LANGUAGES C)

add_library(ispc.tree-sitter SHARED src/parser.c)

target_include_directories(ispc.tree-sitter PUBLIC src)