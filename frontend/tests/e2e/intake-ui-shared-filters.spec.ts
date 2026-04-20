import { expect, test } from "@playwright/test";
import { calendarDate, calendarRange, addCalendarDays, addCalendarMonths } from "@/lib/calendar";
import { parsePage } from "@/lib/api";
import { parseWorklistItem } from "@/lib/staff/parse";

test("applied appointment filters preserve confirmed results on failure and survive view switches", async ({
  page,
}) => {
  test.setTimeout(120000);
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  const today = calendarDate();
  const from = addCalendarDays(today, -1);
  const to = addCalendarDays(today, 40);
  await page.goto(
    `/dashboard/intake?view=list&from=${from}&to=${to}&status=All&pageSize=10&page=3`,
  );
  await expect(page.getByRole("button", { name: "Refresh worklist" })).toBeEnabled();
  const table = page.getByRole("table", { name: "Scoped intake appointments", exact: true });
  await page.getByRole("link", { name: "Calendar", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar" })).toBeEnabled();
  await expect(page.getByRole("button", { name: /^Appointment dates:/ })).toHaveCount(0);
  const calendarRead = page.waitForRequest((request) =>
    request.url().includes("/api/staff/intake/calendar?"),
  );
  await page.getByRole("button", { name: "Next month", exact: true }).click();
  const nextRequest = new URL((await calendarRead).url());
  const visibleMonth = calendarRange(addCalendarMonths(from, 1));
  expect(nextRequest.searchParams.get("to")).toBe(visibleMonth.to < to ? visibleMonth.to : to);
  await expect(page.getByRole("button", { name: "Refresh calendar" })).toBeEnabled();
  const tableLink = new URL(
    (await page.getByRole("link", { name: "Table", exact: true }).getAttribute("href"))!,
    page.url(),
  );
  expect(tableLink.searchParams.get("from")).toBe(from);
  expect(tableLink.searchParams.get("to")).toBe(to);
  expect(tableLink.searchParams.get("page")).toBe("3");
  expect(tableLink.searchParams.get("pageSize")).toBe("10");

  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist" })).toBeEnabled();
  await page.getByRole("button", { name: /^Filter Inspection time/ }).click();
  const dates = page.getByRole("region", { name: "Inspection time", exact: true });
  const single = addCalendarDays(today, 1);
  await dates.getByLabel("From", { exact: true }).fill(single);
  await dates.getByLabel("To", { exact: true }).fill(single);
  expect(new URL(page.url()).searchParams.get("from")).toBe(from);
  await page.route("**/api/staff/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled date failure" },
    }),
  );
  await dates.getByRole("button", { name: "Apply", exact: true }).click();
  await expect(page.getByText(/The worklist could not be refreshed/).first()).toBeVisible();
  await expect(dates).toBeVisible();
  await expect(dates.getByLabel("From", { exact: true })).toHaveValue(single);
  expect(new URL(page.url()).searchParams.get("from")).toBe(from);
  await page.unroute("**/api/staff/intake/appointments?**");
  const appliedRequest = page.waitForRequest((request) =>
    request.url().includes("/api/staff/intake/appointments?"),
  );
  await dates.getByRole("button", { name: "Apply", exact: true }).click();
  const appliedUrl = new URL((await appliedRequest).url());
  expect(appliedUrl.searchParams.get("from")).toBe(single);
  expect(appliedUrl.searchParams.get("to")).toBe(single);
  await expect(dates).not.toBeVisible();
  await page.getByRole("link", { name: "Calendar", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar" })).toBeEnabled();
  await expect(page.getByRole("button", { name: "Previous month", exact: true })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Next month", exact: true })).toBeDisabled();

  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist" })).toBeEnabled();
  const returned = new URL(page.url());
  expect(returned.searchParams.get("from")).toBe(single);
  expect(returned.searchParams.get("to")).toBe(single);
  expect(returned.searchParams.has("page")).toBe(false);
  await expect(table.locator("tbody tr td:nth-child(2)")).toHaveCount(6);

  await page.getByLabel("Search appointments", { exact: true }).fill("VCA 6200");
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist" })).toBeEnabled();
  const clearedRead = page.waitForRequest((request) =>
    request.url().includes("/api/staff/intake/appointments?"),
  );
  const resetResponse = page.waitForResponse((response) =>
    response.url().includes("/api/staff/intake/appointments?"),
  );
  await page.getByRole("button", { name: "Reset filters", exact: true }).click();
  const clearQuery = new URL((await clearedRead).url()).searchParams;
  expect(clearQuery.has("from")).toBe(false);
  expect(clearQuery.has("to")).toBe(false);
  expect(clearQuery.get("search")).toBe("VCA 6200");
  await resetResponse;
  await expect(page.getByRole("button", { name: "Refresh worklist" })).toBeEnabled();
  await expect(page.getByLabel("Search appointments", { exact: true })).toHaveValue("VCA 6200");
  await expect(page.getByRole("button", { name: /^Filter Inspection time/ })).toBeVisible();
  expect(new URL(page.url()).searchParams.has("from")).toBe(false);
  const clearedResponse = page.waitForResponse((response) =>
    response.url().includes("/api/staff/intake/appointments?"),
  );
  await page.getByRole("button", { name: "Clear search", exact: true }).click();
  await expect(page.getByLabel("Search appointments", { exact: true })).toHaveValue("");
  await expect(table.locator("tbody tr td:nth-child(2)")).toHaveCount(10);
  const cleared = parsePage((await (await clearedResponse).json()).data, parseWorklistItem);
  expect(cleared).not.toBeNull();
  expect(cleared!.items.some((row) => calendarDate(new Date(row.startsAt)) < from)).toBe(true);
  await expect(table.locator("tbody tr td:nth-child(2)")).toHaveText(
    cleared!.items.map((row) => row.registrationPlate),
  );
});
