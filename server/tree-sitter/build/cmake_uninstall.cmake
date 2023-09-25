if(NOT EXISTS "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/install_manifest.txt")
  message(WARNING "Cannot find install manifest: \"C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/install_manifest.txt\"")
  return()
endif()

file(READ "C:/Users/ethandav/OneDrive - Intel Corporation/Documents/GitHub/vscode-ispc/server/tree-sitter/build/install_manifest.txt" files)
string(STRIP "${files}" files)
string(REGEX REPLACE "\n" ";" files "${files}")
list(REVERSE files)
foreach(file ${files})
  if(IS_SYMLINK "$ENV{DESTDIR}${file}" OR EXISTS "$ENV{DESTDIR}${file}")
    message(STATUS "Uninstalling: $ENV{DESTDIR}${file}")
    execute_process(
      COMMAND ${CMAKE_COMMAND} -E remove "$ENV{DESTDIR}${file}"
      OUTPUT_VARIABLE rm_out
      RESULT_VARIABLE rm_retval)
    if(NOT "${rm_retval}" EQUAL 0)
      message(FATAL_ERROR "Problem when removing \"$ENV{DESTDIR}${file}\"")
    endif()
  else()
    message(STATUS "Not-found: $ENV{DESTDIR}${file}")
  endif()
endforeach(file)
