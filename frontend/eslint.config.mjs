import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    files: ["src/**/*.{ts,tsx}"],
    rules: {
      curly: ["error", "all"],
      "no-nested-ternary": "error",
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              regex: "^(?:\\.\\./)+(?!\\.\\./|public/)",
              message:
                "Use @/ outside the current folder; public assets are the relative-path exception.",
            },
          ],
        },
      ],
    },
  },
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    ".next-*/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
]);

export default eslintConfig;
