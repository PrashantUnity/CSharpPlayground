import js from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['out/**', 'out-test/**', 'dist/**', 'node_modules/**', 'esbuild.js', 'eslint.config.mjs'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['src/**/*.ts', 'test/**/*.ts'],
    languageOptions: { parserOptions: { projectService: { allowDefaultProject: [] }, tsconfigRootDir: import.meta.dirname } },
    rules: {
      // A promise nobody awaits is a failure nobody sees.
      '@typescript-eslint/no-floating-promises': ['error', { ignoreVoid: true }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      '@typescript-eslint/no-explicit-any': 'off', // VS Code's metadata and JSON from files are untyped by nature
      'no-empty': ['error', { allowEmptyCatch: true }]
    }
  },
  {
    // node:test's describe() and it() return promises that the runner awaits; the stub of 'vscode' hooks require itself.
    files: ['test/**/*.ts'],
    rules: { '@typescript-eslint/no-floating-promises': 'off', '@typescript-eslint/no-require-imports': 'off' }
  }
);
