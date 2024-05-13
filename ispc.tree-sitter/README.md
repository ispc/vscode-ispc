tree-sitter-ispc
================

[![build](https://github.com/ispc/ispc.tree-sitter/actions/workflows/ci.yml/badge.svg)](https://github.com/ispc/ispc.tree-sitter/actions/workflows/ci.yml)

[ISPC](https://ispc.github.io/ispc.html) grammar for
[tree-sitter](https://github.com/maxbrunsfeld/tree-sitter) (based on
[tree-sitter-c](https://github.com/tree-sitter/tree-sitter-c)).

## TODO
* [ ] Fix file headers
* [ ] Rust crate.io (yes or no)
* [ ] Resolve licensing -- project dependencies:
  - `tree-sitter` (MIT)   -> generate the parser code from `grammar.js`
  - `tree-sitter-c` (MIT) -> dependency in `grammar.js`
  - There is no code copied from other projects and modified in this project.
    The code for the ISPC parser is written from scratch; the
    `tree-sitter-c` grammar is used as a dependency that is only included at the
    top of `grammar.js`.  The ISPC extensions are then added in `grammar.js`.
  - The `tree-sitter` dependency is used to generate the C code for the ISPC
    parser (`src/parser.c`) as well as running the test suite.  The `parser.c`
    code is pre-generated and included in the Git history, external applications
    (e.g. text editors) will then fetch the pre-generated code to generate the
    parser module in the application (common practice as far as I know).
  - Files with license information: `Cargo.toml`, `package(-lock).json`,
    `LICENSE`, `.ispc` files in `examples/` are taken from the ISPC repository
    and are licensed under BSD3 (examples could be removed or referred to ISPC
    repo examples).

### Authors:
Fabian Wermelinger