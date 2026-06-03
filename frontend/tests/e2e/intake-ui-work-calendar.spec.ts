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
  await expect(page).toHaveURL(/\/dashboard\/(intake|inspection)$/);
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
  await expect(page.getByRole("heading", { name: "Unscheduled work", exact: true })).toBeVisible();
  expect(reads).toHaveLength(2);
  const selected = calendar.getByRole("region", { name: "Selected date events", exact: true });
  const today = calendarDate();
  await expect(calendar.locator(`[data-date="${today}"]`)).toBeVisible();
  const confirmed = await calendar.innerText();
  const unscheduled = page.getByRole("region", { name: "Unscheduled work", exact: true });
  const confirmedWork = await unscheduled.innerText();
  await page.route("**/api/staff/work-calendar?**", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled read failure", uncertain: true },
    }),
  );
  await page.getByRole("button", { name: "Refresh work calendar", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Scheduled work could not be refreshed" }),
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
    page.getByRole("alert").filter({ hasText: "Unscheduled work could not be refreshed" }),
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
      ? selected.getByRole("button", { name: /^Open VCA 970/ }).first()
      : calendar
          .getByRole("button", { name: /^Open VCA 970/ })
          .filter({ visible: true })
          .first();
  await expect(appointment).toBeVisible();
  await appointment.click();
  await expect(page).toHaveURL(/\/dashboard\/intake\?appointment=/);
  await expect(page.getByLabel("VIN", { exact: true })).toBeVisible();
});

test("group calendar requires a branch before reads and filters full-dataset employee responsibility", async ({
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
  await expect(
    page.getByText("Choose a branch to view its work calendar.", { exact: true }),
  ).toBeVisible();
  expect(reads).toHaveLength(0);
  await page.getByLabel("Branch", { exact: true }).selectOption(branchId);
  await expect(page.getByRole("region", { name: "Work calendar", exact: true })).toBeVisible();
  expect(reads).toHaveLength(2);
  expect(reads.every((url) => new URL(url).searchParams.get("branchId") === branchId)).toBe(true);
  await page.getByLabel("Employee email", { exact: true }).fill("aina.sofia");
  const filteredRead = page.waitForResponse(
    (response) =>
      response.url().includes("/api/staff/work-calendar?") &&
      response.url().includes("employee=aina.sofia"),
  );
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  const filtered = (await (await filteredRead).json()).data;
  expect(filtered.items.length).toBeGreaterThan(0);
  expect(
    filtered.items.every((item: { handlerEmail: string }) =>
      item.handlerEmail.includes("aina.sofia"),
    ),
  ).toBe(true);
  await expect(
    page.getByRole("button", { name: "Refresh work calendar", exact: true }),
  ).toBeEnabled();
  await page.getByLabel("Employee scope", { exact: true }).selectOption("unassigned");
  const unassignedRead = page.waitForResponse(
    (response) =>
      response.url().includes("/api/staff/work-calendar/unscheduled?") &&
      response.url().includes("employee=unassigned"),
  );
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  expect((await (await unassignedRead).json()).data.items).toEqual([]);
  await expect(
    page.getByRole("button", { name: "Refresh work calendar", exact: true }),
  ).toBeEnabled();
  await page.getByLabel("Branch", { exact: true }).selectOption("");
  await expect(page.getByRole("region", { name: "Work calendar", exact: true })).toHaveCount(0);
});
