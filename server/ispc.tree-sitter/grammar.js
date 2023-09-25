const C = require("tree-sitter-c/grammar")

const PREC = Object.assign(C.PREC, {
  NEW: C.PREC.CALL + 1,
})

module.exports = grammar(C, {
    name: 'ispc',

    conflicts: ($, original) => original.concat([
        [$.template_function, $._expression],
        [$.template_function, $._expression_not_binary],
        [$.call_expression, $.llvm_expression],
        [$._declaration_modifiers, $.ms_call_modifier],
    ]),

    rules: {
        _top_level_item: ($, original) => choice(
            original,
            $.template_declaration,
            $.template_instantiation,
        ),

        _block_item: ($, original) => choice(
            original,
            $.template_declaration,
            $.template_instantiation,
        ),

        _top_level_statement: ($, original) => choice(
            original,
            $.foreach_statement,
            $.foreach_instance_statement,
            $.unmasked_statement,
        ),

        // storage duration and types

        storage_class_specifier: ($, original) => choice(
            original,
            field("ispc", 'export'),
            field("ispc", 'noinline'),
        ),

        type_qualifier: ($, original) => prec(1, choice(
            original,
            field("ispc", $._ispc_qualifier),
        )),

        _ispc_qualifier: $ => choice(
            'varying',
            'uniform',
            'unmasked',
            'task',
            $.soa_qualifier,
        ),

        soa_qualifier: $ => seq(
            'soa',
            '<',
            $.number_literal,
            '>'
        ),

        _type_specifier: ($, original) => choice(
            original,
            $.short_vector,
        ),

        _type_declarator: ($, original) => choice(
            original,
            $.short_vector,
        ),

        sized_type_specifier: $ => seq(
            repeat1(choice(
                'signed',
                'unsigned',
                'long',
                'short'
            )),
            optional(field("ispc", choice('varying', 'uniform'))),
            field('type', optional(choice(
                prec.dynamic(-1, $._type_identifier),
                $.primitive_type,
                $.short_vector,
            )))
        ),

        short_vector: $ => prec(1, seq(
            $.primitive_type,
            '<',
            choice($.number_literal, $.identifier),
            '>'
        )),

        enum_specifier: ($, original) => prec.right(original),
        struct_specifier: ($, original) => prec.right(original),
        union_specifier: ($, original) => prec.right(original),

        primitive_type: (_, original) => choice(
            original,
            'int8',
            'int16',
            'int32',
            'int64',
            'uint8',
            'uint16',
            'uint32',
            'uint64',
            'float16',
            'ptrdiff_t',
        ),

        _declaration_modifiers: ($, original) => choice(
            original,
            '__vectorcall',
            '__regcall',
        ),

        ms_declspec_modifier: $ => seq(
            '__declspec',
            '(',
            $.identifier, repeat(seq(',', $.identifier)),
            ')',
        ),

        type_definition: $ => seq(
          'typedef',
          repeat($.type_qualifier),
          field('type', $._type_specifier),
          repeat($.type_qualifier),
          commaSep1(field('declarator', $._type_declarator)),
          ';',
        ),

        // default function arguments

        parameter_declaration: $ => seq(
            $._declaration_specifiers,
            optional(field('declarator', choice(
                $._declarator,
                $._abstract_declarator,
                $.init_declarator
            )))
        ),

        // number literal extensions

        number_literal: $ => {
            const hex = /[0-9a-fA-F]/;
            const decimal = /[0-9]/;
            const binary = /[0-1]/;
            const hexDigits = repeat1(hex);
            const decimalDigits = repeat1(decimal);
            const binaryDigits = repeat1(binary);
            return token(seq(
                optional(/[-\+]/),
                optional(choice('0x', '0X', '0b')),
                choice(
                    seq(
                        choice(
                            decimalDigits,
                            seq('0b', binaryDigits),
                            seq('0x', hexDigits),
                            seq('0X', hexDigits),
                        ),
                        optional(seq('.', optional(hexDigits)))
                    ),
                    seq('.', decimalDigits)
                ),
                optional(seq(
                    /[eEdDpP]/,
                    optional(seq(
                        optional(/[-\+]/),
                        hexDigits
                    ))
                )),
                optional(choice('k', 'M', 'G')),
                repeat(choice('u', 'l', 'U', 'L', 'f16', 'f', 'd', 'F16', 'F', 'D')),
            ))
        },

        // statements

        _non_case_statement: ($, original) => choice(
            original,
            $.foreach_statement,
            $.foreach_instance_statement,
            $.unmasked_statement,
        ),

        if_statement: $ => prec.right(seq(
            choice('if', 'cif'),
            field('condition', $.parenthesized_expression),
            field('consequence', $._statement),
            optional(seq(
                'else',
                field('alternative', $._statement)
            ))
        )),

        while_statement: $ => seq(
            choice('while', 'cwhile'),
            field('condition', $.parenthesized_expression),
            field('body', $._statement)
        ),

        do_statement: $ => seq(
            choice('do', 'cdo'),
            field('body', $._statement),
            'while',
            field('condition', $.parenthesized_expression),
            ';'
        ),

        for_statement: $ => seq(
            choice('for', 'cfor'),
            '(',
            choice(
                field('initializer', $.declaration),
                seq(field('initializer', optional(choice($._expression, $.comma_expression))), ';')
            ),
            field('condition', optional(choice($._expression, $.comma_expression))), ';',
            field('update', optional(choice($._expression, $.comma_expression))),
            ')',
            field('body', $._statement)
        ),

        foreach_statement: $ => seq(
            choice('foreach', 'foreach_tiled'),
            '(',
            $._foreach_range, repeat(seq(',', $._foreach_range)),
            ')',
            field('body', $._statement)
        ),

        foreach_instance_statement: $ => seq(
            choice('foreach_active', 'foreach_unique'),
            '(',
            field('initializer', $._expression),
            optional(
                seq(
                    field('in_operator', 'in'),
                    field('condition', $._expression),
                )
            ),
            ')',
            field('body', $._statement)
        ),

        _foreach_range: $ => seq(
            field('range_start', $._expression),
            field('range_operator', '...'),
            field('range_end', $._expression),
        ),

        unmasked_statement: $ => seq(
            'unmasked',
            field('body', $.compound_statement),
        ),

        // expressions and declarators

        _expression_not_binary: ($, original) => choice(
            original,
            $.new_expression,
            $.delete_expression,
            $.launch_expression,
            $.sync_expression,
            $.llvm_expression,
            $.template_function,
        ),

        new_expression: $ => prec.right(PREC.NEW, seq(
            optional(repeat(field("ispc", $._ispc_qualifier))),
            'new',
            field('placement', optional($.argument_list)),
            optional(repeat(field("ispc", $._ispc_qualifier))),
            field('type', $._type_specifier),
            field('declarator', optional($.new_declarator)),
            field('arguments', optional(choice(
                $.argument_list,
                $.initializer_list
            )))
        )),

        new_declarator: $ => prec.right(seq(
            '[',
            field('length', $._expression),
            ']',
            optional($.new_declarator)
        )),

        delete_expression: $ => seq(
            'delete',
            optional(seq('[', ']')),
            $._expression
        ),

        launch_expression: $ => prec.left(1, seq(
            'launch',
            field('launch_config', optional(choice(
                repeat1(seq('[', $._expression, ']')),
                seq('[', $._expression, repeat(seq(',', $._expression)), ']'),
            ))),
            $._expression,
        )),

        sync_expression: $ => 'sync',

        // function overloading and C++ references

        _declarator: ($, original) => choice(
            original,
            $.overload_declarator,
            $.reference_declarator,
            $.template_function,
        ),

        _field_declarator: ($, original) => choice(
            original,
            alias($.reference_field_declarator, $.reference_declarator),
        ),

        _abstract_declarator: ($, original) => choice(
            original,
            $.abstract_reference_declarator
        ),

        overload_declarator: $ => prec(1, seq(
            field('name', 'operator'),
            field('operator', choice(
                '*',
                '/',
                '%',
                '+',
                '-',
                '>>',
                '<<',
            )),
            field('parameters', $.parameter_list),
            repeat($.attribute_specifier),
          )),

        reference_declarator: $ => prec.dynamic(1, prec.right(1,
            seq('&', optional(field('declarator', $._declarator)))
        )),
        reference_field_declarator: $ => prec.dynamic(1, prec.right(
            seq('&', field('declarator', optional($._field_declarator)))
        )),
        abstract_reference_declarator: $ => prec.dynamic(1, prec.right(
            seq('&', field('declarator', optional($._abstract_declarator)))
        )),

        // C++ templates support

        template_declaration: $ => seq(
            'template',
            field('parameters', $.template_parameter_list),
            choice(
                $._empty_declaration,
                $.declaration,
                $.template_declaration,
                $.function_definition,
            )
        ),

        template_instantiation: $ => seq(
            'template',
            optional($._declaration_specifiers),
            field('declarator', $._declarator),
            ';'
        ),

        template_parameter_list: $ => seq(
            '<',
            commaSep(choice(
                $.parameter_declaration,
                $.type_parameter_declaration,
            )),
            alias(token(prec(1, '>')), '>')
        ),

        type_parameter_declaration: $ => prec(1, seq(
            choice('typename', 'class'),
            optional($._type_identifier)
        )),

        template_function: $ => seq(
            field('name', $.identifier),
            field('arguments', $.template_argument_list)
        ),

        template_argument_list: $ => seq(
            '<',
            commaSep(choice(
                prec.dynamic(3, $.type_descriptor),
                prec.dynamic(1, $._expression)
            )),
            alias(token(prec(1, '>')), '>')
        ),

        // LLVM intrinsics support

        llvm_expression: $ => prec(PREC.CALL, seq(
            field('function', $.llvm_identifier),
            field('arguments', $.argument_list)
        )),

        llvm_identifier: $ => /[%@][-a-zA-Z$._][-a-zA-Z$._0-9]*/,
    }
});

function commaSep(rule) {
  return optional(commaSep1(rule));
}

function commaSep1(rule) {
  return seq(rule, repeat(seq(',', rule)));
}
