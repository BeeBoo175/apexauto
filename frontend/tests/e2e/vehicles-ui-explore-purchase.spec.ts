import {
  expect,
  test,
  type APIResponse,
  type Browser,
  type BrowserContext,
  type Page,
} from "@playwright/test";
import { DISCLOSURE_STORAGE_KEY } from "@/lib/demo/disclosure";
import { addCalendarDays, calendarDate } from "@/lib/calendar";
import type { CustomerReservationReceipt } from "@/lib/vehicles/customer-reservation";
import { expectClean, expectNoSidewaysScroll, guardPage } from "./explore-guards";
import { apiLogin, privateHeaders } from "./staff-session";

const origin = process.env.APP_BASE_URL!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";

// The browser logs every refused request as a console error; scenarios that expect a refusal allow it.
// Photo downloads are throttled (429), which also reaches the console as a failed resource.
const photoThrottle = /Failed to load resource.*429/;
const refusedRequest = [/Failed to load resource/];
const droppedRequest = [/Failed to load resource/, /net::ERR/, /Failed to fetch/];
const rawCodes =
  /customer-has-reservation|vehicle-reserved|customer-has-test-drive|test-drive-slot-unavailable|test-drive-vehicle-unavailable/;

const customers = {
  testDrive: "ahmad.faizal@example.invalid",
  staleTestDrive: "priya.nair@example.invalid",
  unverified: "aiman.hakimi@example.invalid",
  waitingRequest: "aiman.abdullah.shahalam@example.invalid",
};

// Verified customers that may reserve. Fixture history leaves some of them holding a reservation, so each
// scenario signs in as the first one that holds none, and releases its own hold afterwards.
const reservingCustomers = [
  "jasmine.tan@example.invalid",
  "tan.hui.min@example.invalid",
  "kavitha.rajan@example.invalid",
  "irfan.abdullah@example.invalid",
  "chong.wei.ling@example.invalid",
  "lim.jia.wei@example.invalid",
  "shafiq.zulkifli@example.invalid",
  "priya.nair@example.invalid",
  "nur.aisyah@example.invalid",
  "ahmad.faizal@example.invalid",
];

type TestDriveReceipt = { id: string; startsAt: string };

test.beforeAll(async ({ browser }) => {
  test.setTimeout(120_000);
  await releaseBranchHolds(browser);
});

// Managers may cancel a sale in any open status, which releases the hold and frees its customer.
async function releaseBranchHolds(browser: Browser) {
  const manager = await browser.newContext({ baseURL: origin });
  try {
    await apiLogin(manager, "siti.aminah@example.invalid");
    for (const view of ["needsApproval", "open", "beingHandled", "approved"]) {
      const listed = await manager.request.get(`/api/staff/sales?view=${view}&pageSize=50`, {
        headers: await privateHeaders(manager),
      });
      expect(listed.status()).toBe(200);
      const sales = (await listed.json()).data.items as { id: string; version: number }[];
      for (const sale of sales) {
        const cancelled = await manager.request.post(`/api/staff/sales/${sale.id}/cancellations`, {
          headers: await privateHeaders(manager),
          data: {
            operationId: crypto.randomUUID(),
            expectedVersion: sale.version,
            reason: "Release fixture hold for the buying scenarios",
          },
        });
        expect(cancelled.status()).toBe(200);
      }
    }
  } finally {
    await manager.close();
  }
}

async function catalogueIds(context: BrowserContext) {
  const response = await context.request.get(`/api/vehicles?branchId=${branch}`);
  expect(response.status()).toBe(200);
  return ((await response.json()).data.items as { id: string }[]).map((item) => item.id);
}

async function firstVehicleId(context: BrowserContext) {
  const ids = await catalogueIds(context);
  expect(ids.length).toBeGreaterThan(0);
  return ids[0];
}

async function vehicleDetail(context: BrowserContext, vehicleId: string) {
  const response = await context.request.get(`/api/vehicles/${vehicleId}`);
  expect(response.status()).toBe(200);
  return (await response.json()).data as {
    publicationId: string;
    price: { total: number };
  };
}

async function holdsReservation(context: BrowserContext) {
  const response = await context.request.get("/api/account/reservations", {
    headers: await privateHeaders(context),
  });
  expect(response.status()).toBe(200);
  const data = (await response.json()).data;
  return Boolean(data.active || data.pendingPayment);
}

// Signs the context in as the first reserving customer without a hold, other than the ones to skip.
async function signInFreeCustomer(context: BrowserContext, skip: string[] = []) {
  for (const email of reservingCustomers.filter((candidate) => !skip.includes(candidate))) {
    const login = await context.request.post("/api/auth/login", {
      headers: { Origin: origin },
      data: { loginKey: crypto.randomUUID(), email, password: "Password123!" },
    });
    if (login.status() === 200 && !(await holdsReservation(context))) {
      return email;
    }
  }
  throw new Error("No reserving customer without a hold is left");
}

// Cancels the signed-in customer's active reservation so the next scenario can reuse the customer and the car.
async function releaseReservation(context: BrowserContext) {
  const response = await context.request.get("/api/account/reservations", {
    headers: await privateHeaders(context),
  });
  if (response.status() !== 200) {
    return;
  }
  const active = (await response.json()).data.active as { receipt: { id: string } } | null;
  if (active) {
    await context.request.post(`/api/account/reservations/${active.receipt.id}/cancellations`, {
      headers: await privateHeaders(context),
      data: { operationId: crypto.randomUUID(), reason: null },
    });
  }
}

test.afterEach(async ({ context }) => {
  if ((await context.cookies()).some((cookie) => cookie.name === "apexauto_token")) {
    await releaseReservation(context);
  }
});

function reserveButton(page: Page) {
  return page.locator("#vehicle-actions").getByRole("button", { name: /^Reserve for / });
}

function reservationDialog(page: Page) {
  return page.getByRole("dialog", { name: "Reserve this car?", exact: true });
}

function confirmDepositButton(page: Page) {
  return reservationDialog(page).getByRole("button", { name: /^Confirm .* simulated deposit$/ });
}

function reservationReceipt(page: Page) {
  return page.getByRole("region", { name: "Reservation confirmation", exact: true });
}

function reservationRequests(page: Page, vehicleId: string) {
  const bodies: string[] = [];
  page.on("request", (request) => {
    const pathname = new URL(request.url()).pathname;
    if (request.method() === "POST" && pathname === `/api/vehicles/${vehicleId}/reservations`) {
      bodies.push(request.postData() ?? "");
    }
  });
  return bodies;
}

async function openVehicle(page: Page, vehicleId: string) {
  await page.goto(`/vehicles/${vehicleId}`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
}

async function openReservationDialog(page: Page) {
  await expect(reserveButton(page)).toBeEnabled();
  await reserveButton(page).click();
  await expect(reservationDialog(page)).toBeVisible();
}

async function applyPromo(page: Page, code: string) {
  await page.getByText("Have a promo code?", { exact: true }).click();
  const input = page.getByLabel("Promo code", { exact: true });
  await input.fill(code);
  await page
    .locator("form")
    .filter({ has: input })
    .getByRole("button", { name: "Apply", exact: true })
    .click();
  await expect(page.locator("#vehicle-promo-status")).toContainText(`${code} applied`);
}

// Reserves the vehicle through the confirmation dialog and returns the receipt the server committed.
async function reserveVehicle(page: Page, vehicleId: string) {
  await openVehicle(page, vehicleId);
  await openReservationDialog(page);
  const answered = page.waitForResponse(
    (response) =>
      response.request().method() === "POST" &&
      new URL(response.url()).pathname === `/api/vehicles/${vehicleId}/reservations`,
  );
  await confirmDepositButton(page).click();
  const response = await answered;
  expect(response.status()).toBe(200);
  const receipt = (await response.json()).data as CustomerReservationReceipt;
  await expect(reservationReceipt(page)).toBeVisible();
  return receipt;
}

function accountReservations(page: Page) {
  return page.getByRole("region", { name: "Your reservations", exact: true });
}

async function openAccountReservations(page: Page) {
  await page.goto("/account");
  await expect(accountReservations(page)).toBeVisible();
}

async function expectNoRawCodes(page: Page) {
  expect(await page.locator("body").innerText()).not.toMatch(rawCodes);
}

async function optionValues(page: Page, label: string) {
  return page
    .getByRole("dialog")
    .getByLabel(label, { exact: true })
    .locator('option[value]:not([value=""])')
    .evaluateAll((items) => items.map((item) => (item as HTMLOptionElement).value));
}

// Opens the booking dialog, moves to the first date from `firstOffset` days ahead that offers a time,
// and selects its first slot. The caller continues to the confirmation step.
async function chooseTestDriveSlot(page: Page, firstOffset = 2) {
  await page.getByRole("button", { name: "Book a test drive", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Book a test drive", exact: true });
  const times = dialog.getByLabel("Available time (Malaysia)", { exact: true });
  await expect(times).toBeEnabled();
  for (let offset = firstOffset; offset < firstOffset + 8; offset++) {
    await dialog
      .getByLabel("Date (Malaysia)", { exact: true })
      .fill(addCalendarDays(calendarDate(), offset));
    await expect(times).toBeEnabled();
    if ((await optionValues(page, "Available time (Malaysia)")).length > 0) {
      break;
    }
  }
  const slots = await optionValues(page, "Available time (Malaysia)");
  expect(slots.length).toBeGreaterThan(0);
  await times.selectOption(slots[0]);
  return { dialog, startsAt: slots[0] };
}

async function bookTestDrive(page: Page, vehicleId: string): Promise<TestDriveReceipt> {
  await openVehicle(page, vehicleId);
  const { dialog, startsAt } = await chooseTestDriveSlot(page);
  await dialog.getByRole("button", { name: "Continue", exact: true }).click();
  const confirmation = page.getByRole("dialog", { name: "Confirm this test drive?", exact: true });
  const answered = page.waitForResponse(
    (response) =>
      response.request().method() === "POST" &&
      new URL(response.url()).pathname === `/api/vehicles/${vehicleId}/test-drives`,
  );
  await confirmation.getByRole("button", { name: "Confirm test drive", exact: true }).click();
  const response = await answered;
  expect(response.status()).toBe(200);
  const receipt = (await response.json()).data as TestDriveReceipt;
  const confirmed = page.getByRole("dialog", { name: "Test drive confirmed", exact: true });
  await expect(confirmed).toContainText(receipt.id);
  expect(Date.parse(receipt.startsAt)).toBe(Date.parse(startsAt));
  return receipt;
}

function testDriveHistory(page: Page) {
  return page.getByRole("region", { name: "Your test drives", exact: true });
}

test("BUY-01 a verified customer reserves a car without a promo and sees it in the account", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const vehicleId = await firstVehicleId(context);
  const detail = await vehicleDetail(context, vehicleId);

  const receipt = await reserveVehicle(page, vehicleId);
  const confirmation = reservationReceipt(page);
  await expect(confirmation).toContainText(receipt.number);
  await expect(confirmation).toContainText("Hold expires");
  await expect(confirmation).toContainText(/RM[ ]?500/);
  expect(receipt.deposit).toBe(500);
  expect(receipt.price.total).toBe(detail.price.total);
  expect(Date.parse(receipt.expiresAt)).toBeGreaterThan(Date.now());

  expect((await context.request.get(`/api/vehicles/${vehicleId}`)).status()).toBe(404);
  expect(await catalogueIds(context)).not.toContain(vehicleId);

  await openAccountReservations(page);
  const reserved = page.getByRole("article", { name: "Your reserved car", exact: true });
  await expect(reserved).toContainText(receipt.number);
  await expect(reserved).toContainText("Your reserved total");
  await expectNoSidewaysScroll(page);
  expectClean(guard, [photoThrottle]);
});

test("BUY-01 a verified customer reserves a car with a promo and the total drops by the promo", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const vehicleId = await firstVehicleId(context);
  const detail = await vehicleDetail(context, vehicleId);

  await openVehicle(page, vehicleId);
  await applyPromo(page, "APEX500");
  await openReservationDialog(page);
  await expect(reservationDialog(page)).toContainText("APEX500");
  const answered = page.waitForResponse(
    (response) =>
      response.request().method() === "POST" &&
      new URL(response.url()).pathname === `/api/vehicles/${vehicleId}/reservations`,
  );
  await confirmDepositButton(page).click();
  const receipt = ((await (await answered).json()).data ?? {}) as CustomerReservationReceipt;
  await expect(reservationReceipt(page)).toContainText("APEX500");
  expect(receipt.price.total).toBe(detail.price.total - 500);
  expect(receipt.deposit).toBe(500);

  expect(await catalogueIds(context)).not.toContain(vehicleId);
  await openAccountReservations(page);
  await expect(page.getByRole("article", { name: "Your reserved car", exact: true })).toContainText(
    receipt.number,
  );
  await expectNoSidewaysScroll(page);
  expectClean(guard, [photoThrottle]);
});

test("BUY-02 a double click, Escape and Back around the confirmation create one reservation", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const vehicleId = await firstVehicleId(context);
  const posts = reservationRequests(page, vehicleId);

  await page.goto("/vehicles");
  await openVehicle(page, vehicleId);
  await openReservationDialog(page);
  await page.keyboard.press("Escape");
  await expect(reservationDialog(page)).toBeHidden();
  await expect(reserveButton(page)).toBeVisible();
  await expect(reservationReceipt(page)).toHaveCount(0);
  expect(posts).toHaveLength(0);

  await openReservationDialog(page);
  await confirmDepositButton(page).dblclick();
  await expect(reservationReceipt(page)).toBeVisible();
  expect(posts.length).toBeGreaterThan(0);
  // A second request, if one was sent at all, replays the same operation rather than starting another.
  expect(new Set(posts).size).toBe(1);
  const sent = posts.length;

  await page.goBack();
  await expect(page).toHaveURL(/\/vehicles$/);
  await page.goForward();
  await expect(page).toHaveURL(new RegExp(`/vehicles/${vehicleId}$`));
  expect(posts).toHaveLength(sent);

  await openAccountReservations(page);
  await expect(accountReservations(page)).toContainText("Reservation VRS-");
  await expect(page.getByRole("article", { name: "Your reserved car", exact: true })).toHaveCount(
    1,
  );
  await expectNoSidewaysScroll(page);
  expectClean(guard, refusedRequest);
});

test("BUY-03 a customer holding a reservation is refused a second one with a pointer to it", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const firstId = await firstVehicleId(context);
  const first = await reserveVehicle(page, firstId);

  const secondId = await firstVehicleId(context);
  expect(secondId).not.toBe(firstId);
  await openVehicle(page, secondId);
  await openReservationDialog(page);
  await confirmDepositButton(page).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "You already have an active reservation" }),
  ).toBeVisible();
  await expect(
    page.getByRole("alert").filter({ hasText: "You already have an active reservation" }),
  ).toContainText("account");
  await expectNoRawCodes(page);

  expect(await catalogueIds(context)).toContain(secondId);
  await openAccountReservations(page);
  await expect(page.getByRole("article", { name: "Your reserved car", exact: true })).toContainText(
    first.number,
  );
  await expectNoSidewaysScroll(page);
  expectClean(guard, refusedRequest);
});

test("BUY-04 two customers pressing Reserve on the same car together leave one winner", async ({
  browser,
}) => {
  test.setTimeout(120_000);
  const firstContext = await browser.newContext({ baseURL: origin });
  const secondContext = await browser.newContext({ baseURL: origin });
  for (const context of [firstContext, secondContext]) {
    await context.addInitScript((key) => {
      window.localStorage.setItem(key, "true");
    }, DISCLOSURE_STORAGE_KEY);
  }
  const firstEmail = await signInFreeCustomer(firstContext);
  await signInFreeCustomer(secondContext, [firstEmail]);
  try {
    const firstPage = await firstContext.newPage();
    const secondPage = await secondContext.newPage();
    const firstGuard = guardPage(firstPage);
    const secondGuard = guardPage(secondPage);
    const vehicleId = await firstVehicleId(firstContext);

    for (const page of [firstPage, secondPage]) {
      await openVehicle(page, vehicleId);
      await openReservationDialog(page);
    }
    const answers = [firstPage, secondPage].map((page) =>
      page.waitForResponse(
        (response) =>
          response.request().method() === "POST" &&
          new URL(response.url()).pathname === `/api/vehicles/${vehicleId}/reservations`,
      ),
    );
    await Promise.all([
      confirmDepositButton(firstPage).click(),
      confirmDepositButton(secondPage).click(),
    ]);
    const statuses = (await Promise.all(answers)).map((response) => response.status());
    expect(statuses.filter((status) => status === 200)).toHaveLength(1);
    expect(statuses.filter((status) => status >= 500)).toHaveLength(0);

    const winner = statuses[0] === 200 ? firstPage : secondPage;
    const loser = statuses[0] === 200 ? secondPage : firstPage;
    await expect(reservationReceipt(winner)).toBeVisible();
    await expect(
      loser
        .getByRole("alert")
        .filter({ hasText: /reserved by another customer|no longer available/ }),
    ).toBeVisible();
    await expect(reservationReceipt(loser)).toHaveCount(0);
    await expectNoRawCodes(loser);
    await expectNoSidewaysScroll(loser);
    expect(await catalogueIds(firstContext)).not.toContain(vehicleId);
    expectClean(firstGuard, refusedRequest);
    expectClean(secondGuard, refusedRequest);
  } finally {
    await releaseReservation(firstContext);
    await releaseReservation(secondContext);
    await firstContext.close();
    await secondContext.close();
  }
});

test("BUY-05 a reservation whose answer is lost offers to reconcile and shows the one committed hold", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const vehicleId = await firstVehicleId(context);
  const endpoint = `**/api/vehicles/${vehicleId}/reservations`;
  const bodies: string[] = [];
  let committed: CustomerReservationReceipt | null = null;

  await page.route(endpoint, async (route) => {
    bodies.push(route.request().postData() ?? "");
    if (bodies.length === 1) {
      // The server commits the reservation but the answer never reaches the page.
      const response: APIResponse = await route.fetch();
      expect(response.status()).toBe(200);
      committed = (await response.json()).data;
      await route.abort("connectionreset");
      return;
    }
    await route.continue();
  });

  await openVehicle(page, vehicleId);
  await openReservationDialog(page);
  await confirmDepositButton(page).click();
  const dialog = reservationDialog(page);
  const reconcile = dialog.getByRole("button", { name: "Reconcile reservation", exact: true });
  await expect(reconcile).toBeEnabled();
  await expect(dialog.getByRole("button", { name: "Keep browsing", exact: true })).toBeDisabled();
  await reconcile.click();

  await expect(reservationReceipt(page)).toBeVisible();
  expect(bodies).toHaveLength(2);
  expect(bodies[1]).toBe(bodies[0]);
  const saved = committed as CustomerReservationReceipt | null;
  expect(saved).not.toBeNull();
  await expect(reservationReceipt(page)).toContainText(saved!.number);

  await openAccountReservations(page);
  await expect(accountReservations(page)).toContainText("Reservation VRS-");
  await expect(page.getByRole("article", { name: "Your reserved car", exact: true })).toHaveCount(
    1,
  );
  await expect(page.getByRole("article", { name: "Your reserved car", exact: true })).toContainText(
    saved!.number,
  );
  await expectNoSidewaysScroll(page);
  expectClean(guard, droppedRequest);
});

test("BUY-06 cancelling a reservation from the account refunds the deposit and returns the car", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const vehicleId = await firstVehicleId(context);
  await reserveVehicle(page, vehicleId);
  expect(await catalogueIds(context)).not.toContain(vehicleId);

  await openAccountReservations(page);
  await page.getByRole("button", { name: /^Cancel and refund/ }).click();
  const dialog = page.getByRole("dialog", { name: "Cancel this reservation?", exact: true });
  await expect(dialog).toContainText("full simulated deposit refund");
  await dialog.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
  await expect(dialog).toBeHidden();

  await expect(accountReservations(page).getByText("No active reservation")).toBeVisible();
  await expect(accountReservations(page)).toContainText("refunded");
  expect(await catalogueIds(context)).toContain(vehicleId);
  expect((await context.request.get(`/api/vehicles/${vehicleId}`)).status()).toBe(200);
  await expectNoSidewaysScroll(page);
  expectClean(guard, [photoThrottle]);
});

test("BUY-07 a customer books a test drive, sees it in the account, cancels it and the slot returns", async ({
  page,
  context,
}) => {
  test.setTimeout(150_000);
  const guard = guardPage(page);
  await apiLogin(context, customers.testDrive);
  const vehicleId = await firstVehicleId(context);
  const booking = await bookTestDrive(page, vehicleId);

  await page.goto("/account/test-drives");
  const history = testDriveHistory(page);
  const row = history.locator("details").filter({ hasText: booking.id });
  await expect(row).toContainText("Booked");

  await row.locator("summary").click();
  await row.getByRole("button", { name: "Request cancellation", exact: true }).click();
  const cancellation = page.getByRole("dialog", { name: "Cancel this test drive?", exact: true });
  await cancellation.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
  await expect(cancellation).toBeHidden();
  await expect(row).toContainText("Cancelled");

  await openVehicle(page, vehicleId);
  await page.getByRole("button", { name: "Book a test drive", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Book a test drive", exact: true });
  await dialog
    .getByLabel("Date (Malaysia)", { exact: true })
    .fill(calendarDate(new Date(booking.startsAt)));
  await expect
    .poll(async () =>
      (await optionValues(page, "Available time (Malaysia)")).map((value) => Date.parse(value)),
    )
    .toContain(Date.parse(booking.startsAt));
  await expectNoSidewaysScroll(page);
  expectClean(guard, [photoThrottle]);
});

test("BUY-08 a second booking from a stale page, a past slot and a past date are refused clearly", async ({
  page,
  context,
}) => {
  test.setTimeout(150_000);
  const guard = guardPage(page);
  await apiLogin(context, customers.staleTestDrive);
  const vehicleId = await firstVehicleId(context);
  const detail = await vehicleDetail(context, vehicleId);

  // Both tabs reach the confirmation step with the same slot before either books.
  const staleTab = await context.newPage();
  const staleGuard = guardPage(staleTab);
  await openVehicle(page, vehicleId);
  await openVehicle(staleTab, vehicleId);
  const current = await chooseTestDriveSlot(page);
  const stale = await chooseTestDriveSlot(staleTab);
  expect(stale.startsAt).toBe(current.startsAt);
  await current.dialog.getByRole("button", { name: "Continue", exact: true }).click();
  await stale.dialog.getByRole("button", { name: "Continue", exact: true }).click();

  await page
    .getByRole("dialog", { name: "Confirm this test drive?", exact: true })
    .getByRole("button", { name: "Confirm test drive", exact: true })
    .click();
  await expect(
    page.getByRole("dialog", { name: "Test drive confirmed", exact: true }),
  ).toBeVisible();

  await staleTab
    .getByRole("dialog", { name: "Confirm this test drive?", exact: true })
    .getByRole("button", { name: "Confirm test drive", exact: true })
    .click();
  await expect(
    staleTab
      .getByRole("alert")
      .filter({ hasText: /outstanding test drive|slot is unavailable|unavailable/ }),
  ).toBeVisible();
  await expect(staleTab.getByRole("dialog", { name: "Test drive confirmed" })).toHaveCount(0);
  await expectNoRawCodes(staleTab);

  // A slot in the past, sent straight to the server, is refused without a server error.
  const pastSlot = new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
  const pastBooking = await context.request.post(`/api/vehicles/${vehicleId}/test-drives`, {
    headers: await privateHeaders(context),
    data: {
      operationId: crypto.randomUUID(),
      publicationId: detail.publicationId,
      startsAt: pastSlot,
    },
  });
  expect(pastBooking.status()).toBeGreaterThanOrEqual(400);
  expect(pastBooking.status()).toBeLessThan(500);

  // A past date offers no times.
  const pastDate = await context.request.get(
    `/api/vehicles/${vehicleId}/test-drive-slots?date=${addCalendarDays(calendarDate(), -3)}`,
  );
  expect(pastDate.status()).toBeLessThan(500);
  if (pastDate.status() === 200) {
    expect((await pastDate.json()).data.slots).toEqual([]);
  }

  // Only one booking exists, so no overlap was created.
  await page.goto("/account/test-drives");
  const booked = testDriveHistory(page).locator("details").filter({ hasText: "Booked" });
  await expect(booked).toHaveCount(1);
  await expectNoSidewaysScroll(page);
  expectClean(guard, refusedRequest);
  expectClean(staleGuard, refusedRequest);
});

test("BUY-09 an unverified customer is asked to verify their email instead of reserving", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await apiLogin(context, customers.unverified);
  const vehicleId = await firstVehicleId(context);
  await openVehicle(page, vehicleId);

  const verify = page.getByRole("link", { name: "Verify email to reserve", exact: true });
  await expect(verify).toBeVisible();
  await expect(reserveButton(page)).toHaveCount(0);
  await expect(
    page.getByRole("link", { name: "Verify email to book a test drive", exact: true }),
  ).toBeVisible();

  const detail = await vehicleDetail(context, vehicleId);
  const direct = await context.request.post(`/api/vehicles/${vehicleId}/reservations`, {
    headers: await privateHeaders(context),
    data: { operationId: crypto.randomUUID(), publicationId: detail.publicationId, promoId: null },
  });
  expect(direct.status()).toBe(403);

  await verify.click();
  await page.getByRole("link", { name: "Account details", exact: true }).click();
  await expect(page).toHaveURL(/[/]account[/]details$/);
  await expect(
    page.getByRole("button", { name: "Resend verification email", exact: true }),
  ).toBeVisible();
  await expectNoSidewaysScroll(page);
  expectClean(guard, refusedRequest);
});

test("BUY-10 the hold countdown on the account ends at the same deadline as the receipt", async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await signInFreeCustomer(context);
  const vehicleId = await firstVehicleId(context);
  const receipt = await reserveVehicle(page, vehicleId);
  const receiptDeadline = (
    await reservationReceipt(page)
      .locator("div")
      .filter({
        has: page.getByText("Hold expires", { exact: true }),
      })
      .locator("dd")
      .first()
      .innerText()
  ).trim();

  await openAccountReservations(page);
  const reserved = page.getByRole("article", { name: "Your reserved car", exact: true });
  await expect(reserved).toContainText(
    `The car stays off sale until ${receiptDeadline} (Malaysia).`,
  );

  const clock = await reserved.locator("time").first().innerText();
  const [hours, minutes, seconds] = clock.split(":").map(Number);
  const secondsShown = hours * 3600 + minutes * 60 + seconds;
  const secondsToDeadline = (Date.parse(receipt.expiresAt) - Date.now()) / 1000;
  expect(Math.abs(secondsShown - secondsToDeadline)).toBeLessThan(120);
  await expectNoSidewaysScroll(page);
  expectClean(guard, [photoThrottle]);
});

test("BUY-11 a customer with a staff-made deposit request sees it waiting in the account", async ({
  page,
  context,
}) => {
  test.fail(
    true,
    "PB-14: a staff-made pending deposit request refuses a new reservation as an active reservation while the account shows none",
  );
  test.setTimeout(120_000);
  const guard = guardPage(page);
  await apiLogin(context, customers.waitingRequest);
  const vehicleId = await firstVehicleId(context);

  await openVehicle(page, vehicleId);
  await openReservationDialog(page);
  await confirmDepositButton(page).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "You already have an active reservation" }),
  ).toBeVisible();

  await openAccountReservations(page);
  await expect(accountReservations(page).getByText("No active reservation")).toHaveCount(0);
  await expect(page.getByRole("article", { name: "Payment waiting", exact: true })).toBeVisible();
  await expectNoSidewaysScroll(page);
  expectClean(guard, refusedRequest);
});
