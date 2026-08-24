import js from '@eslint/js';
import globals from 'globals';
import tsPlugin from '@typescript-eslint/eslint-plugin';
import tsParser from '@typescript-eslint/parser';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';

/** @type {import('eslint').Linter.FlatConfig[]} */
export default [
  // ─── Base JS rules ────────────────────────────────────────────────────────
  js.configs.recommended,

  // ─── TypeScript rules ────────────────────────────────────────────────────
  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        ecmaVersion: 2020,
        sourceType: 'module',
        ecmaFeatures: { jsx: true },
      },
      // FIX LINT: Add browser + ES2020 globals so TypeScript DOM types (HTMLSelectElement,
      // fetch, URL, URLSearchParams, document, localStorage, etc.) are recognised by the
      // no-undef rule without suppressing the rule entirely.
      // React is added explicitly because files use React.FC / React.ReactNode type references
      // without a local import (correct with the react-jsx JSX transform — TypeScript resolves
      // the types via tsconfig; ESLint's no-undef just needs to know it's in scope globally).
      // process is added for runtime feature detection guarded by import.meta.env / process.env.
      globals: {
        ...globals.browser,
        ...globals.es2020,
        React: 'readonly',
        process: 'readonly',
      },
    },
    plugins: {
      '@typescript-eslint': tsPlugin,
      'react-hooks':        reactHooks,
      'react-refresh':      reactRefresh,
    },
    rules: {
      // TypeScript
      ...tsPlugin.configs['recommended'].rules,
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars':  ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/explicit-function-return-type': 'off',
      '@typescript-eslint/explicit-module-boundary-types': 'off',
      '@typescript-eslint/ban-ts-comment': 'warn',

      // React Hooks
      ...reactHooks.configs.recommended.rules,

      // React Refresh (Vite HMR)
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],

      // General quality
      'no-console':            ['warn', { allow: ['warn', 'error'] }],
      'prefer-const':          'error',
      'no-var':                'error',
      'eqeqeq':                ['error', 'always', { null: 'ignore' }],
      'no-implicit-coercion':  'error',

      // FIX LINT: Disable no-undef for TypeScript files. TypeScript's type-checker
      // already catches references to undefined identifiers at compile time; ESLint's
      // no-undef rule does not understand TypeScript type-only references (RequestInit,
      // RequestInfo, RequestCredentials, GeolocationPosition, etc.) and produces
      // false-positive errors for TypeScript interface names that are not JS runtime globals.
      // This is the officially recommended approach for @typescript-eslint projects.
      'no-undef':              'off',
    },
  },

  // ─── Test files — relaxed rules ──────────────────────────────────────────
  {
    files: ['src/__tests__/**/*.{ts,tsx}', 'src/setupTests.ts'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      'no-console': 'off',
    },
  },

  // ─── Ignored paths ───────────────────────────────────────────────────────
  {
    ignores: ['dist/**', 'coverage/**', 'node_modules/**'],
  },
];
