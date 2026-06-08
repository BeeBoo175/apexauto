import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { addCalendarDays, calendarDate } from "@/lib/calendar";
import { closeStaffWorkspaces, openStaffWorkspace } from "./intake-ui-controls";

test.use({ actionTimeout: 15_000, timezoneId: "America/New_York" });

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const origin = process.env.APP_BASE_URL!;
const branch = "68f371ad-223a-43d6-b45b-b878eece79ae";
const agentId = "86ad2123-6c9b-49b6-ae90-12e23bac7bd3";
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function login(context: BrowserContext, email: string) {
  const response = await context.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: { loginKey: crypto.randomUUID(), email, password: "Password123!" },
  });
  expect(response.status()).toBe(200);
}

async function headers(context: BrowserContext) {
  const cookie = (await context.cookies()).find((item) => item.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}`, Origin: origin };
}

async function prepareBooking(page: Page, occupied: { startsAt: string; endsAt: string }[] = []) {
  await page.getByRole("button", { name: "Book a test drive", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Book a test drive", exact: true });
  const times = dialog.getByLabel("Available time (Malaysia)", { exact: true });
  await expect(times).toBeEnabled();
  for (let day = 1; day <= 7; day++) {
    if ((await times.locator('option[value]:not([value=""])').count()) > 0) {
      break;
    }
    await dialog
      .getByLabel("Date (Malaysia)", { exact: true })
      .fill(addCalendarDays(calendarDate(), day));
    await expect(times).toBeEnabled();
  }
  const options = await times
    .locator('option[value]:not([value=""])')
    .evaluateAll((items) => items.map((item) => (item as HTMLOptionElement).value));
  const startsAt = options.find(
    (start) =>
      !occupied.some(
        (item) =>
          Date.parse(item.startsAt) < Date.parse(start) + 30 * 60_000 &&
          Date.parse(start) < Date.parse(item.endsAt),
      ),
  );
  expect(startsAt).toBeTruthy();
  await times.selectOption(startsAt!);
  await dialog.getByRole("button", { name: "Continue", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(1);
  return page.getByRole("dialog", { name: "Confirm this test drive?", exact: true });
}

async function confirmStaffAction(page: Page, label: string) {
  const record = page.getByRole("region", { name: "Selected test drive", exact: true });
  await record.locator("footer").getByRole("button", { name: label, exact: true }).click();
  await page
    .getByRole("dialog", { name: label, exact: true })
    .getByRole("button", { name: "Confirm action", exact: true })
    .click();
}

test("customer booking and cancellation reconcile exact receipts before manager assignment and same-day handling", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(180_000);
  await login(context, "ahmad.faizal@example.invalid");
  await page.goto(`/vehicles?branchId=${branch}`);
  const destination = await page
    .getByRole("link", { name: /^View details for / })
    .last()
    .getAttribute("href");
  const vehicleId = destination!.split("/").at(-1)!;
  const slotPath = `/api/vehicles/${vehicleId}/test-drive-slots`;
  const beforeSlots = counts()[slotPath] ?? 0;
  const documentResponse = await page.goto(destination!);
  expect(await documentResponse!.text()).toContain("Book a test drive");
  expect(counts()[slotPath] ?? 0).toBe(beforeSlots);
  const bookingBodies: string[] = [];
  const slotReads: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname === slotPath) {
      slotReads.push(request.url());
    }
  });
  let firstBooking = "";
  let firstStartsAt = "";
  const bookingRoute = `**/api/vehicles/${vehicleId}/test-drives`;
  await page.route(bookingRoute, async (route) => {
    bookingBodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    const receipt = (await response.json()).data;
    firstBooking = receipt.id;
    firstStartsAt = receipt.startsAt;
    if (bookingBodies.length === 1) {
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          uncertain: true,
          message: "Controlled lost booking receipt",
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  const confirmation = await prepareBooking(page);
  expect(slotReads.length).toBeGreaterThan(0);
  expect(counts()[slotPath] - beforeSlots).toBe(slotReads.length);
  await confirmation.getByRole("button", { name: "Confirm test drive", exact: true }).click();
  await expect(
    confirmation.getByRole("button", { name: "Choose another time", exact: true }),
  ).toBeDisabled();
  await confirmation.getByRole("button", { name: "Reconcile booking", exact: true }).click();
  await expect(
    page.getByRole("dialog", { name: "Test drive confirmed", exact: true }),
  ).toContainText(firstBooking);
  expect(bookingBodies).toHaveLength(2);
  expect(bookingBodies[1]).toBe(bookingBodies[0]);
  await page.unroute(bookingRoute);
  await page.getByRole("link", { name: "Your test drives", exact: true }).click();
  const history = page.getByRole("region", { name: "Your test drives", exact: true });
  await expect(history).toContainText(firstBooking);
  const initialReads = counts()["/api/account/test-drives"];
  await expect(
    history.getByRole("button", { name: "Refresh test drives", exact: true }),
  ).toHaveCount(0);
  expect(counts()["/api/account/test-drives"]).toBe(initialReads);
  const articles = await history.locator("details").allTextContents();
  const from = history.getByLabel("From (Malaysia)", { exact: true });
  await from.fill("2026-01-01");
  await history.getByLabel("To (Malaysia)", { exact: true }).fill(calendarDate());
  await page.route("**/api/account/test-drives?**", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, uncertain: true, message: "Controlled history failure" },
    }),
  );
  await history.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Test drives could not be refreshed" }),
  ).toBeVisible();
  expect(await history.locator("details").allTextContents()).toEqual(articles);
  await expect(from).toHaveValue("2026-01-01");
  await page.unroute("**/api/account/test-drives?**");
  await history.getByLabel("Search test drives", { exact: true }).fill(firstBooking);
  await history.getByLabel("From (Malaysia)", { exact: true }).fill("");
  await history.getByLabel("To (Malaysia)", { exact: true }).fill("");
  await history.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(history.locator("details")).toHaveCount(1);
  await expect(page).toHaveURL(new RegExp(`testDriveSearch=${firstBooking}`));
  const cancellationBodies: string[] = [];
  const cancelRoute = `**/api/account/test-drives/${firstBooking}/cancellations`;
  await page.route(cancelRoute, async (route) => {
    cancellationBodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    await route.fulfill(
      cancellationBodies.length === 1
        ? {
            status: 502,
            json: {
              status: 502,
              data: null,
              uncertain: true,
              message: "Controlled lost cancellation receipt",
            },
          }
        : { response },
    );
  });
  await history.locator("details").filter({ hasText: firstBooking }).locator("summary").click();
  await history
    .locator("details")
    .filter({ hasText: firstBooking })
    .getByRole("button", { name: "Request cancellation", exact: true })
    .click();
  const cancellation = page.getByRole("dialog", { name: "Cancel this test drive?", exact: true });
  await cancellation.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
  await cancellation.getByRole("button", { name: "Reconcile cancellation", exact: true }).click();
  await expect(cancellation).toBeHidden();
  await expect(history.locator("details").filter({ hasText: firstBooking })).toContainText(
    "Cancelled",
  );
  expect(cancellationBodies).toHaveLength(2);
  expect(cancellationBodies[1]).toBe(cancellationBodies[0]);
  await page.unroute(cancelRoute);
  writeFileSync(
    path.join(evidence, `test-drive-customer-${info.project.name}.json`),
    JSON.stringify({
      bookingId: firstBooking,
      startsAt: firstStartsAt,
      exactBookingReplay: bookingBodies[0] === bookingBodies[1],
      exactCancellationReplay: cancellationBodies[0] === cancellationBodies[1],
      searchedReference: firstBooking,
      sameDayHandlingAvailable: calendarDate(new Date(firstStartsAt)) === calendarDate(),
    }),
  );

  if (calendarDate(new Date(firstStartsAt)) !== calendarDate()) {
    info.annotations.push({
      type: "acceptance scope",
      description:
        "Customer booking, search and exact cancellation verified; same-day handling window unavailable, prior staff lifecycle evidence retained",
    });
    await page.screenshot({
      path: path.join(evidence, `test-drive-customer-${info.project.name}.png`),
      fullPage: true,
    });
    return;
  }

  const manager = await browser.newContext({ ...info.project.use, baseURL: origin });
  const agent = await browser.newContext({ ...info.project.use, baseURL: origin });
  let completedBooking = "";
  try {
    await login(manager, "lim.mei.ling@example.invalid");
    const today = calendarDate();
    const schedule = await manager.request.get(
      `/api/staff/work-calendar?from=${today}&to=${today}&employee=daniel.wong`,
      { headers: await headers(manager) },
    );
    expect(schedule.status()).toBe(200);
    await page.goto(destination!);
    const nextConfirmation = await prepareBooking(page, (await schedule.json()).data.items);
    const booked = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/vehicles/${vehicleId}/test-drives` &&
        response.status() === 200,
    );
    await nextConfirmation.getByRole("button", { name: "Confirm test drive", exact: true }).click();
    const booking = (await (await booked).json()).data;
    completedBooking = booking.id;
    expect(calendarDate(new Date(booking.startsAt))).toBe(calendarDate());
    await expect(
      page.getByRole("dialog", { name: "Test drive confirmed", exact: true }),
    ).toBeVisible();
    const managerPage = await manager.newPage();
    managerPage.setDefaultTimeout(15_000);
    await managerPage.goto(`/dashboard/test-drives?booking=${booking.id}`);
    const managerRecord = managerPage.getByRole("region", {
      name: "Selected test drive",
      exact: true,
    });
    await managerRecord
      .getByLabel("Test drive action", { exact: true })
      .selectOption("assignments");
    await managerRecord.getByLabel("Handling staff", { exact: true }).selectOption(agentId);
    const assigned = managerPage.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/staff/test-drives/${booking.id}/assignments`,
    );
    await confirmStaffAction(managerPage, "Assign handler");
    expect((await assigned).status()).toBe(200);
    await expect(managerRecord.getByLabel("Test drive action", { exact: true })).toHaveValue("");
    await login(agent, "daniel.wong@example.invalid");
    const agentPage = await agent.newPage();
    agentPage.setDefaultTimeout(15_000);
    agentPage.on("dialog", (dialog) => dialog.accept());
    await agentPage.goto(`/dashboard/test-drives?booking=${booking.id}`);
    const record = agentPage.getByRole("region", { name: "Selected test drive", exact: true });
    await expect(record).toBeVisible();
    await record.getByLabel("Test drive action", { exact: true }).selectOption("starts");
    await record.getByLabel("Customer attendance", { exact: true }).selectOption("present");
    await record.getByLabel("Driving licence check", { exact: true }).selectOption("checked");
    await agentPage.getByRole("button", { name: "Refresh test drives", exact: true }).click();
    await expect(record.getByLabel("Driving licence check", { exact: true })).toHaveValue(
      "checked",
    );
    await openStaffWorkspace(agentPage, "Work calendar");
    await agentPage
      .getByRole("dialog", { name: "Discard unsaved changes?", exact: true })
      .getByRole("button", { name: "Keep editing", exact: true })
      .click();
    await closeStaffWorkspaces(agentPage);
    await expect(record.getByLabel("Customer attendance", { exact: true })).toHaveValue("present");
    await confirmStaffAction(agentPage, "Start test drive");
    await expect(record).toContainText("Actual start");
    await expect(record.getByLabel("Test drive action", { exact: true })).toHaveValue("");
    await record.getByLabel("Test drive action", { exact: true }).selectOption("outcomes");
    await record.getByLabel("Customer attendance", { exact: true }).selectOption("present");
    await record
      .getByLabel("Outcome notes (optional)", { exact: true })
      .fill("Customer completed the branch test drive.");
    await confirmStaffAction(agentPage, "Record outcome");
    await expect(record).toContainText("Actual outcome");
    await expect(record).toContainText("Completed");
    const persisted = await agent.request.get(`/api/staff/test-drives/${booking.id}`, {
      headers: await headers(agent),
    });
    expect(persisted.status()).toBe(200);
    const final = (await persisted.json()).data;
    expect(final.start.drivingLicenceChecked).toBe(true);
    expect(final.outcome.kind).toBe("Completed");
    expect(final.outcome.staffId).toBe(agentId);
    writeFileSync(
      path.join(evidence, `test-drive-handling-${info.project.name}.json`),
      JSON.stringify(final, null, 2),
    );
    await agentPage.screenshot({
      path: path.join(evidence, `test-drive-staff-${info.project.name}.png`),
    });
    expect(await agentPage.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(
      false,
    );
  } finally {
    await manager.close();
    await agent.close();
  }
  // The combined multi-actor journey shares the local ingress budget with public photos.
  await new Promise((resolve) => setTimeout(resolve, 61_000));
  await page.goto("/account#test-drives");
  await expect(history.locator("details").filter({ hasText: completedBooking })).toContainText(
    "Completed",
  );
  await page.screenshot({
    path: path.join(evidence, `test-drive-customer-${info.project.name}.png`),
    fullPage: true,
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
});
