#----------------------------------------------------------------
# Generated CMake target import file for configuration "Release".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "tree-sitter::tree_sitter" for configuration "Release"
set_property(TARGET tree-sitter::tree_sitter APPEND PROPERTY IMPORTED_CONFIGURATIONS RELEASE)
set_target_properties(tree-sitter::tree_sitter PROPERTIES
  IMPORTED_IMPLIB_RELEASE "${_IMPORT_PREFIX}/lib/tree_sitter.lib"
  IMPORTED_LOCATION_RELEASE "${_IMPORT_PREFIX}/bin/tree_sitter.dll"
  )

list(APPEND _cmake_import_check_targets tree-sitter::tree_sitter )
list(APPEND _cmake_import_check_files_for_tree-sitter::tree_sitter "${_IMPORT_PREFIX}/lib/tree_sitter.lib" "${_IMPORT_PREFIX}/bin/tree_sitter.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
