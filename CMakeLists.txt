#[[The MIT License (MIT)

Copyright (c) 2023 Intel Corporation

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.]]

cmake_minimum_required(VERSION 3.8)

find_program(MSBUILD_EXECUTABLE NAMES MSBuild.exe)
if(MSBUILD_EXECUTABLE)
    message(STATUS "MSBuild found at ${MSBUILD_EXECUTABLE}")
else()
    message(FATAL_ERROR "MSBuild not found. Please ensure it is installed and on the PATH.")
endif()

# LANGUAGE SERVER BUILD =========================================
project(ispc_languageserver)

set(SOLUTION_FILE "${CMAKE_SOURCE_DIR}/server/ispc_languageserver.sln")

set(MSBUILD_RESTORE_COMMAND "${MSBUILD_EXECUTABLE}" "${SOLUTION_FILE}" "/t:Restore" "/p:Platform=Any CPU")

execute_process(
    COMMAND ${MSBUILD_RESTORE_COMMAND}
    RESULT_VARIABLE MSBUILD_RESTORE_RESULT
    OUTPUT_VARIABLE MSBUILD_RESTORE_OUTPUT
)

if(MSBUILD_RESTORE_RESULT EQUAL 0)
    message(STATUS "MSBuild NuGet package restore succeeded:\n${MSBUILD_RESTORE_OUTPUT}")
else()
    message(FATAL_ERROR "MSBuild NuGet package restore failed:\n${MSBUILD_RESTORE_OUTPUT}")
endif()

set(MSBUILD_COMMAND "${MSBUILD_EXECUTABLE}" "${SOLUTION_FILE}" "/p:Configuration=Release" "/p:Platform=Any CPU")

execute_process(
    COMMAND ${MSBUILD_COMMAND}
    RESULT_VARIABLE MSBUILD_RESULT
    OUTPUT_VARIABLE MSBUILD_OUTPUT
)

if(MSBUILD_RESULT EQUAL 0)
    message(STATUS "MSBuild build succeeded:\n${MSBUILD_OUTPUT}")
else()
    message(FATAL_ERROR "MSBuild build failed:\n${MSBUILD_OUTPUT}")
endif()

# TREE-SITTER INIT AND BUILD =========================================
if(NOT EXISTS "${CMAKE_SOURCE_DIR}/tree-sitter/.git")
  message(STATUS "Initializing tree-sitter submodule")
  execute_process(COMMAND git submodule update --init --recursive
                  WORKING_DIRECTORY ${CMAKE_SOURCE_DIR})
endif()


execute_process(COMMAND git submodule update --remote --merge
                WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}/tree-sitter/.git)

if(${CMAKE_SYSTEM_NAME} MATCHES "Linux" OR ${CMAKE_SYSTEM_NAME} MATCHES "Darwin")
    execute_process(
        COMMAND make
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}/tree-sitter
        RESULT_VARIABLE make_result
        OUTPUT_VARIABLE make_output
    )

    if(NOT make_result EQUAL 0)
        message(FATAL_ERROR "Make failed: ${make_output}")
    endif()
else()
    configure_file(
        ${CMAKE_SOURCE_DIR}/tree-sitter-CMakeLists.txt
        ${CMAKE_SOURCE_DIR}/tree-sitter/CMakeLists.txt
        COPYONLY
    )

    file(MAKE_DIRECTORY ${CMAKE_SOURCE_DIR}/tree-sitter/build)
    execute_process(
        COMMAND ${CMAKE_COMMAND} -DCMAKE_WINDOWS_EXPORT_ALL_SYMBOLS=TRUE -DBUILD_SHARED_LIBS=TRUE -S ${CMAKE_SOURCE_DIR}/tree-sitter -B ${CMAKE_SOURCE_DIR}/tree-sitter/build
    )

    set(SOLUTION_FILE "${CMAKE_SOURCE_DIR}/tree-sitter/build/tree-sitter.sln")
    set(MSBUILD_COMMAND "${MSBUILD_EXECUTABLE}" "${SOLUTION_FILE}" "/p:Configuration=Release")

    execute_process(
        COMMAND ${MSBUILD_COMMAND}
        RESULT_VARIABLE MSBUILD_RESULT
        OUTPUT_VARIABLE MSBUILD_OUTPUT
    )

    if(MSBUILD_RESULT EQUAL 0)
        message(STATUS "MSBuild build succeeded:\n${MSBUILD_OUTPUT}")
    else()
        message(FATAL_ERROR "MSBuild build failed:\n${MSBUILD_OUTPUT}")
    endif()
endif()

# ispc.tree-sitter GRAMMAR BUILD =========================================
if(${CMAKE_SYSTEM_NAME} MATCHES "Linux" OR ${CMAKE_SYSTEM_NAME} MATCHES "Darwin")
    execute_process(
        COMMAND make
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}/ispc.tree-sitter
        RESULT_VARIABLE make_result
        OUTPUT_VARIABLE make_output
    )

    if(NOT make_result EQUAL 0)
        message(FATAL_ERROR "Make failed: ${make_output}")
    endif()
else()
    file(MAKE_DIRECTORY ${CMAKE_SOURCE_DIR}/ispc.tree-sitter/build)
    execute_process(
        COMMAND ${CMAKE_COMMAND} -DCMAKE_WINDOWS_EXPORT_ALL_SYMBOLS=TRUE -DBUILD_SHARED_LIBS=TRUE -S ${CMAKE_SOURCE_DIR}/ispc.tree-sitter -B ${CMAKE_SOURCE_DIR}/ispc.tree-sitter/build
    )

    set(SOLUTION_FILE "${CMAKE_SOURCE_DIR}/ispc.tree-sitter/build/ispc.tree-sitter.sln")
    set(MSBUILD_COMMAND "${MSBUILD_EXECUTABLE}" "${SOLUTION_FILE}" "/p:Configuration=Release")

    execute_process(
        COMMAND ${MSBUILD_COMMAND}
        RESULT_VARIABLE MSBUILD_RESULT
        OUTPUT_VARIABLE MSBUILD_OUTPUT
    ) 

    if(MSBUILD_RESULT EQUAL 0)
        message(STATUS "MSBuild build succeeded:\n${MSBUILD_OUTPUT}")
    else()
        message(FATAL_ERROR "MSBuild build failed:\n${MSBUILD_OUTPUT}")
    endif()
endif()