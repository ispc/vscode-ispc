/*The MIT License (MIT)

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
DEALINGS IN THE SOFTWARE.

Author: Fabian Wermelinger
*/

#ifndef TREE_SITTER_PARSER_H_
#define TREE_SITTER_PARSER_H_

#ifdef __cplusplus
extern "C" {
#endif

#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>

#define ts_builtin_sym_error ((TSSymbol)-1)
#define ts_builtin_sym_end 0
#define TREE_SITTER_SERIALIZATION_BUFFER_SIZE 1024

typedef uint16_t TSStateId;

#ifndef TREE_SITTER_API_H_
typedef uint16_t TSSymbol;
typedef uint16_t TSFieldId;
typedef struct TSLanguage TSLanguage;
#endif

typedef struct {
  TSFieldId field_id;
  uint8_t child_index;
  bool inherited;
} TSFieldMapEntry;

typedef struct {
  uint16_t index;
  uint16_t length;
} TSFieldMapSlice;

typedef struct {
  bool visible;
  bool named;
  bool supertype;
} TSSymbolMetadata;

typedef struct TSLexer TSLexer;

struct TSLexer {
  int32_t lookahead;
  TSSymbol result_symbol;
  void (*advance)(TSLexer *, bool);
  void (*mark_end)(TSLexer *);
  uint32_t (*get_column)(TSLexer *);
  bool (*is_at_included_range_start)(const TSLexer *);
  bool (*eof)(const TSLexer *);
};

typedef enum {
  TSParseActionTypeShift,
  TSParseActionTypeReduce,
  TSParseActionTypeAccept,
  TSParseActionTypeRecover,
} TSParseActionType;

typedef union {
  struct {
    uint8_t type;
    TSStateId state;
    bool extra;
    bool repetition;
  } shift;
  struct {
    uint8_t type;
    uint8_t child_count;
    TSSymbol symbol;
    int16_t dynamic_precedence;
    uint16_t production_id;
  } reduce;
  uint8_t type;
} TSParseAction;

typedef struct {
  uint16_t lex_state;
  uint16_t external_lex_state;
} TSLexMode;

typedef union {
  TSParseAction action;
  struct {
    uint8_t count;
    bool reusable;
  } entry;
} TSParseActionEntry;

struct TSLanguage {
  uint32_t version;
  uint32_t symbol_count;
  uint32_t alias_count;
  uint32_t token_count;
  uint32_t external_token_count;
  uint32_t state_count;
  uint32_t large_state_count;
  uint32_t production_id_count;
  uint32_t field_count;
  uint16_t max_alias_sequence_length;
  const uint16_t *parse_table;
  const uint16_t *small_parse_table;
  const uint32_t *small_parse_table_map;
  const TSParseActionEntry *parse_actions;
  const char * const *symbol_names;
  const char * const *field_names;
  const TSFieldMapSlice *field_map_slices;
  const TSFieldMapEntry *field_map_entries;
  const TSSymbolMetadata *symbol_metadata;
  const TSSymbol *public_symbol_map;
  const uint16_t *alias_map;
  const TSSymbol *alias_sequences;
  const TSLexMode *lex_modes;
  bool (*lex_fn)(TSLexer *, TSStateId);
  bool (*keyword_lex_fn)(TSLexer *, TSStateId);
  TSSymbol keyword_capture_token;
  struct {
    const bool *states;
    const TSSymbol *symbol_map;
    void *(*create)(void);
    void (*destroy)(void *);
    bool (*scan)(void *, TSLexer *, const bool *symbol_whitelist);
    unsigned (*serialize)(void *, char *);
    void (*deserialize)(void *, const char *, unsigned);
  } external_scanner;
  const TSStateId *primary_state_ids;
};

/*
 *  Lexer Macros
 */

#define START_LEXER()           \
  bool result = false;          \
  bool skip = false;            \
  bool eof = false;             \
  int32_t lookahead;            \
  goto start;                   \
  next_state:                   \
  lexer->advance(lexer, skip);  \
  start:                        \
  skip = false;                 \
  lookahead = lexer->lookahead;

#define ADVANCE(state_value) \
  {                          \
    state = state_value;     \
    goto next_state;         \
  }

#define SKIP(state_value) \
  {                       \
    skip = true;          \
    state = state_value;  \
    goto next_state;      \
  }

#define ACCEPT_TOKEN(symbol_value)     \
  result = true;                       \
  lexer->result_symbol = symbol_value; \
  lexer->mark_end(lexer);

#define END_STATE() return result;

/*
 *  Parse Table Macros
 */

#define SMALL_STATE(id) id - LARGE_STATE_COUNT

#define STATE(id) id

#define ACTIONS(id) id

#define SHIFT(state_value)            \
  {{                                  \
    .shift = {                        \
      .type = TSParseActionTypeShift, \
      .state = state_value            \
    }                                 \
  }}

#define SHIFT_REPEAT(state_value)     \
  {{                                  \
    .shift = {                        \
      .type = TSParseActionTypeShift, \
      .state = state_value,           \
      .repetition = true              \
    }                                 \
  }}

#define SHIFT_EXTRA()                 \
  {{                                  \
    .shift = {                        \
      .type = TSParseActionTypeShift, \
      .extra = true                   \
    }                                 \
  }}

#define REDUCE(symbol_val, child_count_val, ...) \
  {{                                             \
    .reduce = {                                  \
      .type = TSParseActionTypeReduce,           \
      .symbol = symbol_val,                      \
      .child_count = child_count_val,            \
      __VA_ARGS__                                \
    },                                           \
  }}

#define RECOVER()                    \
  {{                                 \
    .type = TSParseActionTypeRecover \
  }}

#define ACCEPT_INPUT()              \
  {{                                \
    .type = TSParseActionTypeAccept \
  }}

#ifdef __cplusplus
}
#endif

#endif  // TREE_SITTER_PARSER_H_
