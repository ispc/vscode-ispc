#!/usr/bin/env bash
# File       : parse.sh
# Created    : Mon May 29 2023 12:23:53 (-0400)
# Author     : Fabian Wermelinger
# Description: Parsing tests for the ISPC tests located in:
#              * https://github.com/ispc/ispc/tree/main/tests
#              * https://github.com/ispc/ispc/tree/main/tests_errors
# Copyright 2023 Fabian Wermelinger. All Rights Reserved.
src=${ISPC_HOME:-$HOME/code/ispc}

echo "ISPC tests/*.ispc:"
tree-sitter parse -q -s ${src}/tests/*.ispc

echo "ISPC tests/lit-tests/*.ispc:"
tree-sitter parse -q -s ${src}/tests/lit-tests/*.ispc

echo "ISPC tests_errors/*.ispc:"
tree-sitter parse -q -s ${src}/tests_errors/*.ispc

echo "ISPC examples/*:"
tree-sitter parse -q -s --paths <(find ${src}/examples -type f -name "*.ispc")
