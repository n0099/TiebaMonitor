module.exports = {
    root: true,
    parserOptions: {
        project: ['./tsconfig.json', './tsconfig.node.json'],
        tsconfigRootDir: __dirname,
    },
    overrides: [{ // https://stackoverflow.com/questions/57107800/eslint-disable-extends-in-override
        files: '*.ts',
        parser: 'typescript-eslint-parser-for-extra-files',
        settings: { 'import/resolver': { typescript: true } },
    }, {
        files: '*.vue',
        parser: 'vue-eslint-parser',
        parserOptions: {
            parser: 'typescript-eslint-parser-for-extra-files',
            project: ['./tsconfig.json', './tsconfig.node.json'],
            tsconfigRootDir: __dirname,
        },
        settings: {
            'import/resolver': {
                typescript: true,

                // https://github.com/pzmosquito/eslint-import-resolver-vite/issues/12#issuecomment-1858743165
                vite: { viteConfig: require('import-sync')('./vite.config.ts').default },
            },
        },
    }, {
        files: '.eslintrc.cjs',
        plugins: ['@stylistic', '@stylistic/migrate'],
        rules: {
            '@stylistic/migrate/migrate-js': 'error',
            '@stylistic/migrate/migrate-ts': 'error',
            '@stylistic/comma-dangle': ['error', 'always-multiline'],
            '@typescript-eslint/naming-convention': 'off',
        },
    }],
    plugins: ['@stylistic'],
    extends: [
        'eslint:recommended',
        'plugin:vue/vue3-recommended',
        '@vue/typescript/recommended',
        'plugin:@typescript-eslint/strict-type-checked',
        'plugin:@typescript-eslint/stylistic-type-checked',
        'plugin:import/recommended',
        'plugin:import/typescript',
    ],
    rules: {
        'import/no-useless-path-segments': 'error',
        'import/extensions': ['error', 'always', { ts: 'never' }],

        // as of eslint@8.56.0
        'no-await-in-loop': 'error',
        'no-promise-executor-return': 'error',
        'no-template-curly-in-string': 'error',
        'no-unreachable-loop': 'error',
        'require-atomic-updates': 'error',
        'accessor-pairs': 'error',
        'array-callback-return': 'error',
        complexity: ['error', { max: 30 }],
        'consistent-return': 'error',
        curly: ['error', 'multi-or-nest', 'consistent'],
        'default-case-last': 'error',
        '@stylistic/dot-location': ['error', 'property'],
        eqeqeq: 'error',
        'grouped-accessor-pairs': ['error', 'getBeforeSet'],
        'guard-for-in': 'error',
        'max-classes-per-file': 'error',
        'no-alert': 'error',
        'no-case-declarations': 'error',
        'no-constructor-return': 'error',
        'no-else-return': 'error',

        // 'no-empty-function': 'error',
        'no-eval': 'error',
        'no-extra-bind': 'error',
        '@stylistic/no-floating-decimal': 'error',
        'no-implicit-coercion': 'error',
        'no-implicit-globals': 'error',
        'no-labels': 'error',
        'no-lone-blocks': 'error',
        '@stylistic/no-multi-spaces': 'error',
        'no-multi-str': 'error',
        'no-new': 'error',
        'no-new-func': 'error',
        'no-new-wrappers': 'error',
        'no-octal-escape': 'error',
        'no-param-reassign': 'error',
        'no-proto': 'error',
        'no-return-assign': 'error',
        'no-script-url': 'error',
        'no-self-compare': 'error',
        'no-sequences': 'error',
        'no-unmodified-loop-condition': 'error',
        'no-useless-call': 'error',
        'no-useless-concat': 'error',
        'no-useless-escape': 'error',
        'no-useless-return': 'error',
        'prefer-promise-reject-errors': 'error',
        'prefer-regex-literals': ['error', { disallowRedundantWrapping: true }],
        radix: ['error', 'as-needed'],
        'require-unicode-regexp': 'error',
        'vars-on-top': 'error',
        '@stylistic/wrap-iife': ['error', 'inside'],
        yoda: 'error',
        strict: 'error',
        'no-undef-init': 'error',
        '@stylistic/array-bracket-newline': ['error', 'consistent'],
        '@stylistic/array-bracket-spacing': 'error',
        '@stylistic/array-element-newline': ['error', 'consistent'],
        '@stylistic/block-spacing': 'error',
        'capitalized-comments': ['error', 'never'],
        '@stylistic/comma-style': 'error',
        '@stylistic/computed-property-spacing': 'error',
        'consistent-this': 'error',
        '@stylistic/eol-last': 'error',
        'func-name-matching': 'error',
        '@stylistic/function-call-argument-newline': ['error', 'consistent'],
        '@stylistic/function-paren-newline': ['error', 'consistent'],
        '@stylistic/jsx-quotes': 'error',
        '@stylistic/key-spacing': ['error', {
            beforeColon: false,
            afterColon: true,
            mode: 'strict',
        }],
        '@stylistic/linebreak-style': 'error',
        '@stylistic/max-statements-per-line': ['error', { max: 2 }],
        'multiline-comment-style': ['error', 'separate-lines'],
        '@stylistic/multiline-ternary': ['error', 'always-multiline'],
        'new-cap': 'error',
        '@stylistic/new-parens': 'error',

        // 'newline-per-chained-call': ['error', { 'ignoreChainWithDepth': 3 }],
        'no-array-constructor': 'error',
        'no-bitwise': 'error',
        'no-continue': 'error',
        'no-lonely-if': 'error',
        '@stylistic/no-mixed-operators': 'error',
        'no-multi-assign': 'error',
        '@stylistic/no-multiple-empty-lines': 'error',
        'no-negated-condition': 'error',
        'no-nested-ternary': 'error',
        'no-object-constructor': 'error',
        '@stylistic/no-tabs': 'error',
        '@stylistic/no-trailing-spaces': 'error',
        'no-unneeded-ternary': 'error',
        '@stylistic/no-whitespace-before-property': 'error',
        '@stylistic/nonblock-statement-body-position': ['error', 'below'],
        '@stylistic/object-curly-newline': ['error', {
            multiline: true,
            consistent: true,
        }],
        '@stylistic/object-property-newline': ['error', { allowAllPropertiesOnSameLine: true }],
        'one-var': ['error', 'never'],
        'operator-assignment': 'error',
        '@stylistic/operator-linebreak': ['error', 'before'],
        '@stylistic/padded-blocks': ['error', 'never'],
        'prefer-exponentiation-operator': 'error',
        'prefer-object-spread': 'error',
        '@stylistic/quote-props': ['error', 'as-needed'],
        '@stylistic/semi-spacing': 'error',
        '@stylistic/semi-style': 'error',

        // 'sort-keys': ['error', 'asc', { 'caseSensitive': false, 'natural': true }],
        'sort-vars': ['error', { ignoreCase: true }],
        '@stylistic/space-before-blocks': 'error',
        '@stylistic/space-in-parens': 'error',
        '@stylistic/space-unary-ops': ['error', {
            words: true,
            nonwords: false,
        }],

        // https://github.com/typescript-eslint/typescript-eslint/issues/600#issuecomment-499979248
        '@stylistic/spaced-comment': ['error', 'always', { markers: ['/'] }],
        '@stylistic/switch-colon-spacing': 'error',
        '@stylistic/template-tag-spacing': 'error',
        'unicode-bom': 'error',
        'arrow-body-style': 'error',
        '@stylistic/arrow-parens': ['error', 'as-needed'],
        '@stylistic/arrow-spacing': 'error',
        '@stylistic/generator-star-spacing': ['error', {
            before: false,
            after: true,
            method: {
                before: true,
                after: false,
            },
        }],
        '@stylistic/no-confusing-arrow': 'error',
        'no-new-symbol': 'error',
        'no-useless-computed-key': ['error', { enforceForClassMembers: true }],
        'no-useless-rename': 'error',
        'no-var': 'error',
        'object-shorthand': 'error',
        'prefer-arrow-callback': 'error',
        'prefer-const': 'error',
        'prefer-numeric-literals': 'error',
        'prefer-rest-params': 'error',
        'prefer-spread': 'error',
        'prefer-template': 'error',
        '@stylistic/rest-spread-spacing': 'error',
        'sort-imports': ['error', { ignoreDeclarationSort: true }],
        'symbol-description': 'error',
        '@stylistic/template-curly-spacing': 'error',
        '@stylistic/yield-star-spacing': 'error',
        'prefer-object-has-own': 'error',
        'no-constant-binary-expression': 'error',
        'logical-assignment-operators': ['error', 'always', { enforceForIfStatements: true }],
        'no-empty-static-block': 'error',
        'no-new-native-nonconstructor': 'error',
        '@stylistic/lines-around-comment': ['error', {
            beforeBlockComment: true,
            beforeLineComment: true,
            allowBlockStart: true,
            allowObjectStart: true,
            allowArrayStart: true,
            allowClassStart: true,
        }],

        // as of @typescript-eslint@6.14.0
        '@typescript-eslint/no-empty-function': 'error',

        'no-void': 'off',
        '@stylistic/brace-style': ['error', '1tbs', { allowSingleLine: true }],
        '@stylistic/comma-dangle': 'error',
        '@stylistic/comma-spacing': 'error',
        'default-param-last': 'off',
        '@typescript-eslint/default-param-last': 'error',
        'dot-notation': 'off',
        '@typescript-eslint/dot-notation': 'error',
        '@stylistic/func-call-spacing': 'error',
        '@stylistic/indent': 'error',
        'init-declarations': 'off',
        '@typescript-eslint/init-declarations': 'error',
        '@stylistic/keyword-spacing': 'error',
        '@stylistic/lines-between-class-members': 'error',
        'no-dupe-class-members': 'off',
        '@typescript-eslint/no-dupe-class-members': 'error',
        '@stylistic/no-extra-parens': ['error', 'all', {
            ignoreJSX: 'multi-line',
            enforceForArrowConditionals: false,
        }],
        'no-invalid-this': 'off',
        '@typescript-eslint/no-invalid-this': 'error',
        'no-loop-func': 'off',
        '@typescript-eslint/no-loop-func': 'error',
        'no-loss-of-precision': 'off',
        '@typescript-eslint/no-loss-of-precision': 'error',
        'no-redeclare': 'off',
        '@typescript-eslint/no-redeclare': 'error',
        'no-shadow': 'off',
        '@typescript-eslint/no-shadow': ['error', {
            builtinGlobals: true,
            hoist: 'all',
            allow: ['name'],
        }],
        'no-throw-literal': 'off',
        '@typescript-eslint/no-throw-literal': 'error',
        'no-unused-expressions': 'off',
        '@typescript-eslint/no-unused-expressions': 'error',
        'no-use-before-define': 'off',
        '@typescript-eslint/no-use-before-define': 'error',
        'no-useless-constructor': 'off',
        '@typescript-eslint/no-useless-constructor': 'error',
        '@stylistic/object-curly-spacing': ['error', 'always'],
        '@stylistic/quotes': ['error', 'single', { avoidEscape: true }],
        'no-return-await': 'off',
        '@typescript-eslint/return-await': 'error',
        '@stylistic/semi': ['error', 'always', { omitLastInOneLineBlock: true }],
        '@stylistic/space-before-function-paren': ['error', {
            anonymous: 'always',
            named: 'never',
            asyncArrow: 'always',
        }],
        '@stylistic/space-infix-ops': ['error', { int32Hint: false }],
        'require-await': 'off',
        '@typescript-eslint/require-await': 'error',
        '@stylistic/padding-line-between-statements': ['error'],
        'class-methods-use-this': 'off',
        '@typescript-eslint/class-methods-use-this': 'error',
        'prefer-destructuring': 'off',
        '@typescript-eslint/prefer-destructuring': 'error',

        '@typescript-eslint/array-type': ['error', {
            default: 'array-simple',
            readonly: 'array-simple',
        }],
        '@typescript-eslint/class-literal-property-style': 'error',
        '@typescript-eslint/consistent-indexed-object-style': 'error',
        '@typescript-eslint/consistent-type-assertions': 'error',
        '@typescript-eslint/consistent-type-definitions': ['error', 'interface'],
        '@typescript-eslint/consistent-type-imports': 'error',
        '@typescript-eslint/explicit-member-accessibility': 'error',
        '@stylistic/member-delimiter-style': ['error', {
            multiline: {
                delimiter: 'comma',
                requireLast: false,
            },
            singleline: {
                delimiter: 'comma',
                requireLast: false,
            },
        }],
        '@typescript-eslint/member-ordering': 'error',
        '@typescript-eslint/method-signature-style': 'error',
        camelcase: 'off',
        '@typescript-eslint/naming-convention': ['error', {
            selector: 'default',
            format: ['camelCase'],
        }, {
            selector: 'objectLiteralProperty',
            format: ['camelCase', 'PascalCase'], // vue component
        }, {
            selector: ['objectLiteralProperty', 'objectLiteralMethod'],
            format: null,
            filter: { regex: '^\\w+:\\w+$', match: true }, // vue event names in component.emit
        }, {
            selector: ['function', 'variable', 'import'],
            format: ['camelCase', 'PascalCase'], // vue component
        }, {
            selector: 'import',
            format: null,
            filter: { regex: '^_$', match: true }, // lodash
        }, {
            selector: 'parameter',
            format: ['camelCase'],
            leadingUnderscore: 'allow',
        }, {
            selector: 'memberLike',
            modifiers: ['private'],
            format: ['camelCase'],
            leadingUnderscore: 'require',
        }, {
            selector: 'typeLike',
            format: ['PascalCase'],
        }],
        '@typescript-eslint/no-base-to-string': 'error',
        '@typescript-eslint/no-confusing-void-expression': 'error',
        '@typescript-eslint/no-dynamic-delete': 'error',
        '@typescript-eslint/no-extraneous-class': 'error',
        '@typescript-eslint/no-invalid-void-type': 'error',
        '@typescript-eslint/no-require-imports': 'error',
        '@typescript-eslint/no-unnecessary-boolean-literal-compare': 'error',
        '@typescript-eslint/no-unnecessary-condition': 'error',
        '@typescript-eslint/no-unnecessary-qualifier': 'error',
        '@typescript-eslint/no-unnecessary-type-arguments': 'error',
        '@typescript-eslint/no-unnecessary-type-constraint': 'error',
        '@typescript-eslint/non-nullable-type-assertion-style': 'error',
        '@typescript-eslint/prefer-enum-initializers': 'error',
        '@typescript-eslint/prefer-for-of': 'error',
        '@typescript-eslint/prefer-function-type': 'error',
        '@typescript-eslint/prefer-includes': 'error',
        '@typescript-eslint/prefer-literal-enum-member': 'error',
        '@typescript-eslint/prefer-nullish-coalescing': 'error',
        '@typescript-eslint/prefer-optional-chain': 'error',
        '@typescript-eslint/prefer-readonly': 'error',
        '@typescript-eslint/prefer-reduce-type-parameter': 'error',
        '@typescript-eslint/prefer-string-starts-ends-with': 'error',
        '@typescript-eslint/prefer-ts-expect-error': 'error',
        '@typescript-eslint/promise-function-async': 'error',
        '@typescript-eslint/require-array-sort-compare': 'error',
        '@typescript-eslint/strict-boolean-expressions': 'error',
        '@typescript-eslint/switch-exhaustiveness-check': 'error',
        '@stylistic/type-annotation-spacing': 'error',
        '@typescript-eslint/unified-signatures': 'error',
        '@typescript-eslint/no-unsafe-argument': 'error',
        '@typescript-eslint/prefer-return-this-type': 'error',
        '@typescript-eslint/no-non-null-asserted-nullish-coalescing': 'error',
        '@typescript-eslint/consistent-type-exports': 'error',
        '@typescript-eslint/no-useless-empty-export': 'error',
        '@typescript-eslint/no-redundant-type-constituents': 'error',
        '@typescript-eslint/no-import-type-side-effects': 'error',
        '@typescript-eslint/no-mixed-enums': 'error',
        '@typescript-eslint/no-duplicate-type-constituents': ['error', { ignoreUnions: true }],
        '@typescript-eslint/no-unsafe-enum-comparison': 'error',
        '@typescript-eslint/no-unsafe-unary-minus': 'error',

        // as of eslint-plugin-vue@9.19.2
        'vue/html-indent': ['error', 4],
        'vue/max-attributes-per-line': 'off',
        'vue/no-reserved-component-names': 'off', // for component in antdv
        'vue/attribute-hyphenation': ['error', 'never'],
        'vue/singleline-html-element-content-newline': 'off',
        'vue/attributes-order': ['error', {
            order: [
                'DEFINITION',
                ['LIST_RENDERING', 'UNIQUE'],
                'CONDITIONALS',
                'RENDER_MODIFIERS',
                'TWO_WAY_BINDING',
                'EVENTS',
                'SLOT',
                'OTHER_DIRECTIVES',
                'OTHER_ATTR',
                'GLOBAL',
                'CONTENT',
            ],
        }],
        'vue/multi-word-component-names': 'off',
        'vue/first-attribute-linebreak': ['error', {
            singleline: 'beside',
            multiline: 'beside',
        }],
        'vue/html-closing-bracket-newline': ['error', {
            singleline: 'never',
            multiline: 'never',
        }],
        'vue/html-self-closing': ['error', { html: { void: 'always' } }],
        'vue/v-on-event-hyphenation': ['error', 'never', { autofix: true }],
        'vue/require-default-prop': 'off',
        'vue/multiline-html-element-content-newline': 'off',

        'vue/block-tag-newline': 'error',
        'vue/component-api-style': ['error', ['script-setup', 'composition']],
        'vue/component-name-in-template-casing': 'error',
        'vue/component-options-name-casing': 'error',
        'vue/custom-event-name-casing': ['error', 'camelCase'],
        'vue/html-comment-content-spacing': 'error',
        'vue/html-comment-indent': ['error', 4],
        'vue/no-duplicate-attr-inheritance': 'error',
        'vue/no-empty-component-block': 'error',
        'vue/no-invalid-model-keys': 'error',
        'vue/no-multiple-objects-in-class': 'error',
        'vue/no-undef-components': 'error',
        'vue/no-useless-mustaches': 'error',
        'vue/no-useless-v-bind': 'error',
        'vue/no-v-text': 'error',
        'vue/padding-line-between-blocks': 'error',
        'vue/prefer-separate-static-class': 'error',
        'vue/require-direct-export': 'error',
        'vue/require-emit-validator': 'error',

        // 'vue/require-expose': 'error',
        // 'vue/static-class-names-order': 'error',
        'vue/v-for-delimiter-style': 'error',
        'vue/array-bracket-newline': ['error', 'consistent'],
        'vue/array-bracket-spacing': 'error',
        'vue/arrow-spacing': 'error',
        'vue/block-spacing': 'error',
        'vue/brace-style': ['error', '1tbs', { allowSingleLine: true }],
        'vue/comma-dangle': 'error',
        'vue/comma-spacing': 'error',
        'vue/comma-style': 'error',
        'vue/dot-location': ['error', 'property'],
        'vue/dot-notation': 'error',
        'vue/eqeqeq': 'error',
        'vue/func-call-spacing': 'error',
        'vue/key-spacing': ['error', {
            beforeColon: false,
            afterColon: true,
            mode: 'strict',
        }],
        'vue/keyword-spacing': 'error',
        'vue/no-constant-condition': 'error',
        'vue/no-empty-pattern': 'error',
        'vue/no-extra-parens': ['error', 'all', {
            ignoreJSX: 'multi-line',
            enforceForArrowConditionals: false,
        }],
        'vue/no-irregular-whitespace': 'error',
        'vue/no-loss-of-precision': 'error',
        'vue/no-sparse-arrays': 'error',
        'vue/no-useless-concat': 'error',
        'vue/object-curly-newline': ['error', {
            multiline: true,
            consistent: true,
        }],
        'vue/object-curly-spacing': ['error', 'always'],
        'vue/object-property-newline': ['error', { allowAllPropertiesOnSameLine: true }],
        'vue/operator-linebreak': ['error', 'before'],
        'vue/prefer-template': 'error',
        'vue/space-in-parens': 'error',
        'vue/space-infix-ops': ['error', { int32Hint: false }],
        'vue/space-unary-ops': ['error', {
            words: true,
            nonwords: false,
        }],
        'vue/template-curly-spacing': 'error',
        'vue/quote-props': ['error', 'as-needed'],
        'vue/object-shorthand': 'error',
        'vue/prefer-true-attribute-shorthand': 'error',
        'vue/prefer-prop-type-boolean-first': 'error',
        'vue/define-macros-order': ['error', {
            order: [
                'defineOptions',
                'defineProps',
                'defineEmits',

                // 'defineModel', https://github.com/vuejs/eslint-plugin-vue/issues/2130
                'defineSlots',
            ],
        }],
        'vue/match-component-import-name': 'error',
        'vue/define-props-declaration': 'error',
        'vue/define-emits-declaration': 'error',
        'vue/no-required-prop-with-default': 'error',
        'vue/v-on-handler-style': ['error', 'inline-function'],
        'vue/multiline-ternary': 'error',
        'vue/array-element-newline': ['error', 'consistent'],
        'vue/prefer-define-options': 'error',
        'vue/valid-define-options': 'error',
        'vue/require-macro-variable-name': 'error',
        'vue/require-typed-ref': 'error',
        'vue/no-deprecated-model-definition': 'error',
        'vue/require-typed-object-prop': 'error',
        'vue/no-use-v-else-with-v-for': 'error',
        'vue/no-unused-emit-declarations': 'error',
        'vue/no-ref-object-reactivity-loss': 'error',
    },
};
