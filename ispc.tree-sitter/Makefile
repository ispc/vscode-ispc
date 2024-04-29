SRC := src/parser.c
HEADER := src/tree-sitter/parser.h
OUTPUT_NAME := mylibrary
CC := gcc
CFLAGS := -Wall -fPIC

ifeq ($(shell uname), Darwin)
    LIB_EXTENSION := .dylib
    LDFLAGS := -dynamiclib
else
    LIB_EXTENSION := .so
    LDFLAGS := -shared
endif

OUTPUT := $(OUTPUT_NAME)$(LIB_EXTENSION)

all: $(OUTPUT)

$(OUTPUT): $(SRC) $(HEADER)
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ $(SRC)

clean:
	rm -f $(OUTPUT)

.PHONY: all clean
