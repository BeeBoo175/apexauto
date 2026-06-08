import { expect, test, type Page } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";
import { calendarDate } from "@/lib/calendar";

test.use({ timezoneId: "America/New_York", actionTimeout: 15_000 });
const branchId = "c77708bf-1a31-42f5-9a69-0c8766697a52";

async function login(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/(work-calendar|inspection)$/);
}

test("own calendar loads once, retains each section on failure and opens the feature workspace", async ({
  page,
}, testInfo) => {
  test.setTimeout(120_000);
  await login(page, "aina.sofia@example.invalid");
  const reads: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/staff/work-calendar")) {
      reads.push(request.url());
    }
  });
  await page.goto("/dashboard/work-calendar");
  const calendar = page.getByRole("region", { name: "Work calendar", exact: true });
  await expect(calendar).toBeVisible();
  await expect(page.getByRole("heading", { name: "Sales", exact: true })).toBeVisible();
  expect(reads).toHaveLength(2);
  const selected = calendar.getByRole("region", { name: "Selected date events", exact: true });
  const today = calendarDate();
  await expect(calendar.locator(`[data-date="${today}"]`)).toBeVisible();
  const confirmed = await calendar.innerText();
  const unscheduled = page.getByRole("region", { name: "Ongoing work", exact: true });
  const confirmedWork = await unscheduled.innerText();
  await page.route("**/api/staff/work-calendar?**", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled read failure", uncertain: true },
    }),
  );
  await page.getByRole("button", { name: "Refresh work calendar", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Appointments could not be refreshed" }),
  ).toBeVisible();
  expect(await calendar.innerText()).toBe(confirmed);
  expect(await unscheduled.innerText()).toBe(confirmedWork);
  await page.unroute("**/api/staff/work-calendar?**");
  await page.route("**/api/staff/work-calendar/unscheduled?**", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled work failure", uncertain: true },
    }),
  );
  await page.getByRole("button", { name: "Refresh work calendar", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Ongoing work could not be loaded" }),
  ).toBeVisible();
  expect(await calendar.innerText()).toBe(confirmed);
  expect(await unscheduled.innerText()).toBe(confirmedWork);
  await page.unroute("**/api/staff/work-calendar/unscheduled?**");
  const beforeMonth = reads.length;
  await calendar.getByRole("button", { name: "Next month", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Refresh work calendar", exact: true }),
  ).toBeEnabled();
  expect(reads.slice(beforeMonth)).toHaveLength(1);
  expect(reads.at(-1)).not.toContain("unscheduled");
  await calendar.getByRole("button", { name: "Today", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Refresh work calendar", exact: true }),
  ).toBeEnabled();
  await page.screenshot({
    path: path.join(
      process.env.BROWSER_EVIDENCE_DIR!,
      `work-calendar-${testInfo.project.name}.png`,
    ),
    fullPage: true,
  });
  await unscheduled.scrollIntoViewIfNeeded();
  await page.screenshot({
    path: path.join(
      process.env.BROWSER_EVIDENCE_DIR!,
      `work-calendar-unscheduled-${testInfo.project.name}.png`,
    ),
    fullPage: true,
  });
  writeFileSync(
    path.join(
      process.env.BROWSER_EVIDENCE_DIR!,
      `work-calendar-traffic-${testInfo.project.name}.json`,
    ),
    JSON.stringify({
      reads: reads.map((url) => new URL(url).pathname),
      overflow: await page.evaluate(() => document.documentElement.scrollWidth > innerWidth),
    }),
  );
  const appointment =
    testInfo.project.name === "mobile"
      ? selected.getByRole("button", { name: /^Open Intake · VCA 970/ }).first()
      : calendar
          .getByRole("button", { name: /^Open Intake · VCA 970/ })
          .filter({ visible: true })
          .first();
  await expect(appointment).toBeVisible();
  await appointment.click();
  await expect(page).toHaveURL(/\/dashboard\/intake\?appointment=/);
  await expect(page.getByLabel("VIN", { exact: true })).toBeVisible();
});

test("group calendar defaults to all branches and shares search without cross-filtering panels", async ({
  page,
}) => {
  test.setTimeout(120_000);
  await login(page, "farah.aziz@example.invalid");
  const reads: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("/api/staff/work-calendar")) {
      reads.push(request.url());
    }
  });
  await page.goto("/dashboard/work-calendar");
  const calendar = page.getByRole("region", { name: "Work calendar", exact: true });
  const ongoing = page.getByRole("region", { name: "Ongoing work", exact: true });
  const refresh = page.getByRole("button", { name: "Refresh work calendar", exact: true });
  await expect(calendar).toBeVisible();
  await expect(refresh).toBeEnabled();
  await expect(page.getByLabel("Branch", { exact: true })).toHaveValue("");
  expect(reads).toHaveLength(2);
  expect(reads.every((url) => !new URL(url).searchParams.has("branchId"))).toBe(true);
  await page.getByLabel("Branch", { exact: true }).selectOption(branchId);
  await expect(refresh).toBeEnabled();
  expect(
    reads.slice(-2).every((url) => new URL(url).searchParams.get("branchId") === branchId),
  ).toBe(true);
  const search = page.getByRole("searchbox", {
    name: "Search ongoing work and appointments",
    exact: true,
  });
  await search.fill("aina.sofia");
  const previousAppointments = await calendar.innerText();
  const previousWork = await ongoing.innerText();
  await page.route("**/api/staff/work-calendar?**", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled shared read failure", uncertain: true },
    }),
  );
  await search.press("Enter");
  await expect(
    page.getByRole("alert").filter({ hasText: "Appointments could not be refreshed" }),
  ).toBeVisible();
  await expect(refresh).toBeEnabled();
  expect(await calendar.innerText()).toBe(previousAppointments);
  expect(await ongoing.innerText()).toBe(previousWork);
  await expect(search).toHaveValue("aina.sofia");
  await page.unroute("**/api/staff/work-calendar?**");
  const filteredRead = page.waitForResponse(
    (response) =>
      response.url().includes("/api/staff/work-calendar?") &&
      response.url().includes("search=aina.sofia"),
  );
  await search.press("Enter");
  const filtered = (await (await filteredRead).json()).data;
  expect(filtered.items.length).toBeGreaterThan(0);
  expect(
    filtered.items.every((item: { handlerEmail: string }) =>
      item.handlerEmail.includes("aina.sofia"),
    ),
  ).toBe(true);
  await expect(refresh).toBeEnabled();
  expect(
    reads.slice(-2).every((url) => new URL(url).searchParams.get("search") === "aina.sofia"),
  ).toBe(true);
  const confirmedWork = await ongoing.innerText();
  const beforeAssignment = reads.length;
  await page
    .getByLabel("Appointment assignment (appointments only)", { exact: true })
    .selectOption("Unassigned");
  await expect(refresh).toBeEnabled();
  expect(reads.slice(beforeAssignment)).toHaveLength(1);
  expect(reads.at(-1)).not.toContain("unscheduled");
  expect(await ongoing.innerText()).toBe(confirmedWork);
  await expect(calendar.getByRole("button", { name: /^Open / })).toHaveCount(0);
  const beforeType = reads.length;
  await page
    .getByRole("navigation", { name: "Ongoing work types", exact: true })
    .getByRole("button", { name: /^Sales\b/ })
    .click();
  await expect(refresh).toBeEnabled();
  expect(reads.slice(beforeType)).toHaveLength(1);
  expect(reads.at(-1)).toContain("unscheduled");
});

test("technician enters Workshop and is denied calendar reads", async ({ page }) => {
  await login(page, "azlan.rahim@example.invalid");
  await expect(page).toHaveURL(/\/dashboard\/inspection$/);
  await expect(page.getByRole("link", { name: "Work calendar", exact: true })).toHaveCount(0);
  const cookie = (await page.context().cookies()).find((item) => item.name === "apexauto_token")!;
  const headers = { Cookie: `apexauto_token=${cookie.value}` };
  const response = await page.request.get(
    "/api/staff/work-calendar?from=2026-11-01&to=2026-11-30",
    { headers },
  );
  expect(response.status()).toBe(403);
  const ongoing = await page.request.get(
    "/api/staff/work-calendar/unscheduled?page=1&pageSize=25",
    { headers },
  );
  expect(ongoing.status()).toBe(403);
});
