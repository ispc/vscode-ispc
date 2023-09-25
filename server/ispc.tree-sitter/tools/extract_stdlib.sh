#!/usr/bin/env bash
# File       : extract_stdlib.sh
# Created    : Fri May 19 2023 17:14:02 (-0400)
# Author     : Fabian Wermelinger
# Description: Extract built-in standard library functions from stdlib.ispc
# Copyright 2023 Fabian Wermelinger. All Rights Reserved.

if [[ $# -ne 1 ]]; then
    cat <<EOF
USAGE: $0 <path to ISPC stdlib.ispc>
EOF
    exit 1
fi

src=$1; shift

ISPC_MASK_BITS=16
cpp -DISPC_MASK_BITS=${ISPC_MASK_BITS} ${src} | tr { '\n' |
    sed -n -r 's/^\s*(static|__declspec).*\W([^_ ]\w+)\s*\(.*$/"\2"/p' >/tmp/stdlib_ispc

# missing
echo '"ISPCAlloc"' >>/tmp/stdlib_ispc
echo '"ISPCLaunch"' >>/tmp/stdlib_ispc
echo '"ISPCSync"' >>/tmp/stdlib_ispc
echo '"alloca"' >>/tmp/stdlib_ispc
echo '"assert"' >>/tmp/stdlib_ispc
echo '"invoke_sycl"' >>/tmp/stdlib_ispc
echo '"print"' >>/tmp/stdlib_ispc

cat /tmp/stdlib_ispc | sort | uniq
rm /tmp/stdlib_ispc
