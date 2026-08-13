import { defineConfig, devices } from "@playwright/test";

const browserJourneys = Boolean(process.env.BROWSER_EVIDENCE_DIR);
const appBaseUrl = process.env.APP_BASE_URL;
const nextOrigin = process.env.BROWSER_NEXT_ORIGIN;
const intakeUi = Boolean(process.env.BROWSER_INTAKE);
// Mobile runs only the golden purchase journey; every other journey is desktop-only.
const goldenMobileTests = "e2e/vehicles-ui-sales-completion.spec.ts";

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
      name: "edit-guards",
      testMatch: "ui/edit-guards.spec.ts",
    },
    {
      name: "contracts",
      testMatch: [
        "auth/contracts.spec.ts",
        "auth/browser-transport-contracts.spec.ts",
        "accounts/staff-accounts-contracts.spec.ts",
        "accounts/relay-contracts.spec.ts",
        "accounts/accounts-location-contracts.spec.ts",
        "accounts/account-sections-contracts.spec.ts",
        "settings/settings-location-contracts.spec.ts",
        "home/home-location-contracts.spec.ts",
        "intake/contracts.spec.ts",
        "intake/work-calendar-contracts.spec.ts",
        "intake/no-show-contracts.spec.ts",
        "intake/staff-sell-notice-contracts.spec.ts",
        "intake/taxonomy-contracts.spec.ts",
        "intake/seller-steps-contracts.spec.ts",
        "home/workspace-names-contracts.spec.ts",
        "intake/assisted-customer-contracts.spec.ts",
        "intake/assisted-conflict-contracts.spec.ts",
        "performance/performance-contracts.spec.ts",
        "performance/performance-location-contracts.spec.ts",
        "inspection/contracts.spec.ts",
        "vehicles/contracts.spec.ts",
        "vehicles/photo-contracts.spec.ts",
        "vehicles/publication-contracts.spec.ts",
        "vehicles/withdrawal-contracts.spec.ts",
        "vehicles/account-reservation-contracts.spec.ts",
        "vehicles/public-detail-contracts.spec.ts",
        "vehicles/financing-contracts.spec.ts",
        "vehicles/promo-contracts.spec.ts",
        "vehicles/trade-in-contracts.spec.ts",
        "sales/contracts.spec.ts",
        "sales/completion-contracts.spec.ts",
        "sales/footer-contracts.spec.ts",
        "sales/history-contracts.spec.ts",
        "sales/table-contracts.spec.ts",
        "test-drives/contracts.spec.ts",
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
            testMatch: goldenMobileTests,
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
