import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { calendarDate, calendarRange } from "@/lib/calendar";

test.use({ timezoneId: "America/New_York", actionTimeout: 15000 });
const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branchId = "c77708bf-1a31-42f5-9a69-0c8766697a52";
test("month calendar retains its snapshot, supports overflow and refreshes the shared drawer after check-in", async ({
  page,
  context,
}, testInfo) => {
  test.setTimeout(120000);
  const mobile = testInfo.project.name === "mobile";
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  const region = page.getByRole("region", { name: "Intake calendar", exact: true });
  const today = calendarDate();
  const range = calendarRange(today);
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  const headers = { Cookie: `apexauto_token=${cookie.value}` };
  const response = await context.request.get(
    `/api/staff/intake/calendar?from=${range.from}&to=${range.to}&status=All`,
    { headers },
  );
  expect(response.status()).toBe(200);
  const value = await response.json();
  const refs = JSON.parse(readFileSync(path.join(evidence, "references.json"), "utf8")) as {
    id: string;
    branchId: string;
    startsAt: string;
  }[];
  const own = refs.filter(
    (row) =>
      row.branchId === branchId &&
      calendarDate(new Date(row.startsAt)) >= range.from &&
      calendarDate(new Date(row.startsAt)) <= range.to,
  );
  expect(value.data.total).toBe(own.length);
  expect(value.data.total).toBeGreaterThanOrEqual(12);
  expect(value.data.items.map((item: { id: string }) => item.id).sort()).toEqual(
    own.map((row) => row.id).sort(),
  );
  const confirmed = await region.innerText();
  const confirmedUrl = page.url();
  let releaseFailure = () => {};
  const failureGate = new Promise<void>((resolve) => {
    releaseFailure = resolve;
  });
  await page.route("**/api/staff/intake/calendar?**", async (route) => {
    await failureGate;
    await route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled calendar failure", uncertain: true },
    });
  });
  await region.getByRole("button", { name: "Next month", exact: true }).click();
  const refresh = page.getByRole("button", { name: "Refresh calendar", exact: true });
  await expect(refresh).toHaveAttribute("aria-busy", "true");
  releaseFailure();
  await expect(
    page.getByRole("alert").filter({ hasText: "The last confirmed month" }),
  ).toBeVisible();
  expect(await region.innerText()).toBe(confirmed);
  await expect(page).toHaveURL(confirmedUrl);
  await page.unroute("**/api/staff/intake/calendar?**");
  await expect(refresh).toHaveAttribute("aria-busy", "false");
  await region.getByRole("button", { name: "Next month", exact: true }).click();
  await expect(page).toHaveURL(/date=\d{4}-\d{2}-01/);
  await region.getByRole("button", { name: "Today", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`date=${today}`));

  await page.getByLabel("Status", { exact: true }).selectOption("Booked");
  await expect(page).toHaveURL(/status=Booked/);
  const panel = region.getByRole("region", { name: "Selected date events", exact: true });
  const appointment = mobile
    ? panel.getByRole("button", { name: /^Open VCA 970/ }).first()
    : region
        .getByRole("button", { name: /^Open VCA 970/ })
        .filter({ visible: true })
        .first();
  const appointmentName = await appointment.getAttribute("aria-label");
  await appointment.press("Enter");
  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  const vin = `PL1BT65R84B${Date.now().toString().slice(-6)}`;
  await dialog.getByLabel("VIN", { exact: true }).fill(vin);
  await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();

  await appointment.press("Enter");
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue(vin);
  await dialog.getByLabel("Actual odometer (km)", { exact: true }).fill("92000");
  for (const checkbox of await dialog.getByRole("checkbox").all()) {
    await checkbox.check();
  }
  await dialog.getByRole("button", { name: "Confirm check-in", exact: true }).click();
  const confirmation = page.getByRole("dialog", { name: "Check in this vehicle?", exact: true });
  await expect(confirmation).toBeVisible();
  const receipt = page.waitForResponse(
    (result) => result.url().includes("/check-ins") && result.request().method() === "POST",
  );
  await confirmation.getByRole("button", { name: "Confirm check-in", exact: true }).click();
  const saved = await (await receipt).json();
  await expect(
    dialog.locator("footer").getByText("Check-in confirmed", { exact: true }),
  ).toBeVisible();
  writeFileSync(path.join(evidence, "check-in.json"), JSON.stringify(saved.data));
  await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();
  await expect(region.getByRole("button", { name: appointmentName!, exact: true })).toHaveCount(0);

  const dense = value.data.items.reduce(
    (days: Record<string, number>, item: { startsAt: string }) => {
      const date = calendarDate(new Date(item.startsAt));
      days[date] = (days[date] ?? 0) + 1;
      return days;
    },
    {} as Record<string, number>,
  );
  const denseDate = Object.keys(dense).find((date) => date !== today && dense[date] > 3)!;
  expect(denseDate).toBeTruthy();
  if (mobile) {
    await region.locator(`[data-date="${denseDate}"]`).locator("[data-date-control]").click();
    await expect(panel.getByRole("button", { name: /^Open / })).toHaveCount(dense[denseDate]);
  } else {
    const cell = region.locator(`[data-date="${denseDate}"]`);
    await cell.locator("[data-date-control]").click();
    const overflow = page
      .getByRole("dialog")
      .filter({ has: page.getByRole("button", { name: /^Open / }) });
    await expect(overflow.getByRole("button", { name: /^Open / })).toHaveCount(dense[denseDate]);
    const event = overflow.getByRole("button", { name: /^Open / }).last();
    await event.press("Enter");
    await expect(
      dialog.getByRole("button", { name: "Refresh appointment", exact: true }),
    ).toBeEnabled();
    await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();

    await overflow.getByRole("button", { name: /Close/ }).click();
  }

  const calendarUrl = page.url();
  await page.reload();
  await expect(page).toHaveURL(calendarUrl);

  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(
    page.getByRole("table", { name: "Scoped intake appointments", exact: true }),
  ).toBeVisible();

  await page.getByRole("link", { name: "Calendar", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  const denied = await context.request.get(
    `/api/staff/intake/calendar?from=${range.from}&to=${range.to}&branchId=68f371ad-223a-43d6-b45b-b878eece79ae`,
    { headers },
  );
  expect(denied.status()).toBe(403);
});

test("group month reads every branch and applies a branch filter without availability discovery", async ({
  page,
  context,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("farah.aziz@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  const today = calendarDate();
  const range = calendarRange(today);
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  const response = await context.request.get(
    `/api/staff/intake/calendar?from=${range.from}&to=${range.to}&status=All`,
    {
      headers: { Cookie: `apexauto_token=${cookie.value}` },
    },
  );
  expect(response.status()).toBe(200);
  const snapshot = (await response.json()).data;
  const refs = JSON.parse(readFileSync(path.join(evidence, "references.json"), "utf8")) as {
    id: string;
    branchId: string;
    startsAt: string;
  }[];
  const expected = refs.filter((row) => {
    const date = calendarDate(new Date(row.startsAt));
    return date >= range.from && date <= range.to;
  });
  expect(snapshot.total).toBe(expected.length);
  expect(snapshot.items.map((item: { id: string }) => item.id).sort()).toEqual(
    expected.map((row) => row.id).sort(),
  );
  expect(new Set(snapshot.items.map((item: { branchId: string }) => item.branchId)).size).toBe(3);
  const filteredResponse = page.waitForResponse(
    (result) =>
      result.url().includes("/api/staff/intake/calendar?") &&
      result.url().includes(`branchId=${branchId}`),
  );
  await page.getByLabel("Branch", { exact: true }).selectOption(branchId);
  const filtered = (await (await filteredResponse).json()).data;
  await expect(page).toHaveURL(new RegExp(`branchId=${branchId}`));
  expect(filtered.total).toBe(expected.filter((row) => row.branchId === branchId).length);
  expect(filtered.items.every((item: { branchId: string }) => item.branchId === branchId)).toBe(
    true,
  );
});
