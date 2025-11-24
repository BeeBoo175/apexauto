import { defineConfig, devices } from "@playwright/test";

const browserAuth = Boolean(process.env.AUTH_EVIDENCE_DIR);
const appBaseUrl = process.env.APP_BASE_URL;
const nextOrigin = process.env.AUTH_NEXT_ORIGIN;

if (browserAuth && (!process.env.API_BASE_URL || !appBaseUrl || !nextOrigin)) {
  throw new Error("Run browser auth through the disposable BrowserAuthHarness.");
}

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: "line",
  expect: { timeout: 15_000 },
  timeout: 60_000,
  use: { trace: "off" },
  projects: [
    {
      name: "contracts",
      testMatch: "auth/contracts.spec.ts",
    },
    ...(browserAuth
      ? [
          {
            name: "desktop-auth",
            testMatch: "e2e/browser-auth*.spec.ts",
            use: {
              ...devices["Desktop Chrome"],
              viewport: { width: 1440, height: 1024 },
              baseURL: appBaseUrl,
            },
          },
          {
            name: "mobile-auth",
            testMatch: "e2e/browser-auth*.spec.ts",
            use: { ...devices["Pixel 7"], baseURL: appBaseUrl },
          },
        ]
      : []),
  ],
  webServer: browserAuth
    ? {
        command: `node node_modules/next/dist/bin/next start --hostname 127.0.0.1 --port ${new URL(nextOrigin!).port}`,
        url: `${nextOrigin}/login`,
        reuseExistingServer: false,
        timeout: 30_000,
      }
    : undefined,
});
