import { defineConfig, devices } from "@playwright/test";

const browserJourneys = Boolean(process.env.BROWSER_EVIDENCE_DIR);
const appBaseUrl = process.env.APP_BASE_URL;
const nextOrigin = process.env.BROWSER_NEXT_ORIGIN;
const intakeUi = Boolean(process.env.BROWSER_INTAKE);

function browserTests() {
  if (intakeUi) {
    return ["e2e/intake-ui*.spec.ts", "e2e/inspection-ui*.spec.ts", "e2e/vehicles-ui*.spec.ts"];
  }
  return "e2e/browser-auth*.spec.ts";
}

if (browserJourneys && (!process.env.API_BASE_URL || !appBaseUrl || !nextOrigin)) {
  throw new Error("Run selected journeys through the disposable BrowserJourneys launcher.");
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
      testMatch: [
        "auth/contracts.spec.ts",
        "intake/contracts.spec.ts",
        "intake/no-show-contracts.spec.ts",
        "inspection/contracts.spec.ts",
        "vehicles/contracts.spec.ts",
        "vehicles/photo-contracts.spec.ts",
        "vehicles/publication-contracts.spec.ts",
        "vehicles/withdrawal-contracts.spec.ts",
      ],
    },
    ...(browserJourneys
      ? [
          {
            name: "transport",
            testMatch: "e2e/auth-transport.spec.ts",
            use: { baseURL: appBaseUrl },
          },
          {
            name: "desktop",
            testMatch: browserTests(),
            use: {
              ...devices["Desktop Chrome"],
              viewport: { width: 1440, height: 1024 },
              baseURL: appBaseUrl,
            },
          },
          {
            name: "mobile",
            testMatch: browserTests(),
            use: { ...devices["Pixel 7"], baseURL: appBaseUrl },
          },
        ]
      : []),
  ],
  webServer: browserJourneys
    ? {
        command: `node node_modules/next/dist/bin/next start --hostname 127.0.0.1 --port ${new URL(nextOrigin!).port}`,
        url: `${nextOrigin}/login`,
        reuseExistingServer: false,
        timeout: 30_000,
      }
    : undefined,
});
