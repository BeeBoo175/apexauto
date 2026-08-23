import { test, expect, type Page } from "@playwright/test";
import { signIn } from "./staff-session";

// Walks every staff page in two situations that are hard to reach by hand and saves a screenshot of each, so the
// no-access page and each page's failed-read reload notice can be reviewed side by side. It runs against the servers
// that are already running (APP_BASE_URL, default http://localhost:3000) and only reads; it creates no data.
const pages = [
  "/dashboard/home",
  "/dashboard/intake",
  "/dashboard/workshop",
  "/dashboard/listings",
  "/dashboard/sales",
  "/dashboard/test-drives",
  "/dashboard/work-calendar",
  "/dashboard/performance",
  "/dashboard/settings",
  "/dashboard/accounts",
];

const technician = "azlan.rahim@example.invalid";
const administrator = "muhammad.hafiz@example.invalid";
const manager = "siti.aminah@example.invalid";
const agent = "aina.sofia@example.invalid";

function slug(path: string) {
  return path.replace("/dashboard/", "");
}

async function capture(page: Page, name: string, outputPath: (file: string) => string) {
  await page.waitForLoadState("networkidle");
  await page.screenshot({ path: outputPath(`${name}.png`), fullPage: true });
}

test("a signed-in account without access sees one generic page that names no role", async ({
  page,
}, testInfo) => {
  await signIn(page, technician);
  await page.waitForURL(/\/dashboard\//);
  const roleWords = /administrator|manager|sales agent|technician|only .* can/i;
  let denied = 0;

  for (const path of pages) {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
    const heading = page.getByRole("heading", {
      name: "You don't have access to this page",
      exact: true,
    });
    if (!(await heading.isVisible().catch(() => false))) {
      continue;
    }
    denied++;
    await expect(page.getByText("You don't have access to this page")).toBeVisible();
    await expect(page.locator("body")).not.toContainText(roleWords);
    await capture(page, `no-access-${slug(path)}`, testInfo.outputPath.bind(testInfo));
  }

  expect(denied).toBeGreaterThan(0);
});

for (const [label, email] of [
  ["administrator", administrator],
  ["manager", manager],
  ["agent", agent],
  ["technician", technician],
] as const) {
  test(`${label}: each page it may open shows its reload notice when the API stops answering`, async ({
    page,
  }, testInfo) => {
    await signIn(page, email);
    await page.waitForURL(/\/dashboard\//);
    await page.route("**/api/**", (route) =>
      route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({ title: "unavailable" }),
      }),
    );

    for (const path of pages) {
      await page.goto(path);
      await page.waitForLoadState("networkidle");
      // A page this account may not open shows the no-access page, which has no read to fail.
      if (
        await page
          .getByRole("heading", { name: "You don't have access to this page", exact: true })
          .isVisible()
      ) {
        continue;
      }
      await capture(page, `api-down-${label}-${slug(path)}`, testInfo.outputPath.bind(testInfo));
    }
  });
}

test("an address that does not exist shows the not-found page", async ({ page }, testInfo) => {
  await page.goto("/this-page-does-not-exist");
  await expect(
    page.getByRole("heading", { name: "We can't find that page", exact: true }),
  ).toBeVisible();
  await capture(page, "not-found", testInfo.outputPath.bind(testInfo));
});

test("a vehicle that is no longer listed shows the unavailable page on the public site", async ({
  page,
}, testInfo) => {
  await page.goto("/vehicles/6f1f3b0a-2b7c-4f55-9d8e-0a6d1c9f4e21");
  await expect(
    page.getByRole("heading", { name: "This vehicle is no longer available" }),
  ).toBeVisible();
  await capture(page, "vehicle-unavailable", testInfo.outputPath.bind(testInfo));
});
