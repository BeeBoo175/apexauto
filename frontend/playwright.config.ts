import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: "line",
  use: { baseURL: "http://localhost:3000", trace: "retain-on-failure" },
  projects: [
    { name: "desktop-chromium", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 1024 } } },
    { name: "mobile-chromium", use: { ...devices["Pixel 7"] } },
  ],
  webServer: {
    command: "node node_modules/next/dist/bin/next dev --hostname localhost",
    url: "http://localhost:3000/sell",
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
