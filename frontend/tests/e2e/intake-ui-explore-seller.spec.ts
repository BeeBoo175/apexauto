import path from "node:path";
import {
  expect,
  test,
  type Browser,
  type BrowserContext,
  type Locator,
  type Page,
} from "@playwright/test";
import { expectClean, expectNoSidewaysScroll, guardPage } from "./explore-guards";
import { setAppointmentDates } from "./intake-ui-controls";
import { privateHeaders, signIn } from "./staff-session";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const photo = path.join(evidence, "upload.jpg");
const angles = ["Driver", "Passenger", "Front", "Rear"];
const glenmarie = "ApexAuto Glenmarie — Shah Alam";
// Photo downloads are throttled (429), which also reaches the console as a failed resource.
const photoThrottle = /Failed to load resource.*429/;
const rawCodePattern = /^[a-z0-9]+(-[a-z0-9]+)+$/;

const seller = "ahmad.faizal@example.invalid";
const rival = "tan.hui.min@example.invalid";

type Booking = { plate: string; branchId: string; date: string; startsAt: string };

// A plate that is valid and unlikely to repeat across tests in one seeded database.
let plateCounter = 0;
function freshPlate() {
  plateCounter += 1;
  return `WQA ${2000 + plateCounter * 7 + (Date.now() % 700)}`;
}

// A day on or after the offset that is open for appointments (Monday to Saturday, Malaysia time).
function openDay(offsetDays: number) {
  const day = new Date(Date.now() + (offsetDays * 24 + 8) * 60 * 60 * 1000);
  if (day.getUTCDay() === 0) {
    day.setUTCDate(day.getUTCDate() + 1);
  }
  return day.toISOString().slice(0, 10);
}

async function openSellerAs(page: Page, email: string) {
  await signIn(page, email);
  await expect(page).toHaveURL(/\/account/);
  await page.goto("/sell");
  await expect(page.getByRole("heading", { name: "Vehicle details", exact: true })).toBeVisible();
}

async function fillVehicle(page: Page, plate: string, mileage = "90000") {
  await page.getByLabel("Make", { exact: true }).selectOption("Proton");
  await page.getByLabel("Model", { exact: true }).selectOption("Saga");
  await page.getByLabel("Trim", { exact: true }).selectOption("Standard");
  await page.getByLabel("Manufacture year").selectOption("2020");
  await page.getByLabel("Declared mileage (km)").fill(mileage);
  await page.getByLabel("Registration plate").fill(plate);
}

async function requestValuation(page: Page) {
  await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Valuation", exact: true })).toBeVisible();
}

async function reachPhotos(page: Page, plate: string) {
  await fillVehicle(page, plate);
  await requestValuation(page);
  await page.getByRole("button", { name: "Continue to photos", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Exterior photos", exact: true })).toBeVisible();
}

async function choosePhoto(
  page: Page,
  angle: string,
  file: string | { name: string; mimeType: string; buffer: Buffer },
) {
  await page
    .getByRole("region", { name: `${angle} photo upload`, exact: true })
    .getByLabel(`${angle} photo`, { exact: true })
    .setInputFiles(file);
}

async function chooseAllPhotos(page: Page) {
  for (const angle of angles) {
    await choosePhoto(page, angle, photo);
  }
}

async function reachAppointment(page: Page, plate: string) {
  await reachPhotos(page, plate);
  await chooseAllPhotos(page);
  await page.getByRole("button", { name: "Choose appointment", exact: true }).click();
  await expect(page.getByLabel("Preferred branch", { exact: true })).toBeVisible();
}

async function offerSlots(page: Page, date: string) {
  await page.getByLabel("Preferred branch", { exact: true }).selectOption({ label: glenmarie });
  await page.getByLabel("Appointment date (Malaysia)").fill(date);
  await page.getByRole("button", { name: "Check available slots", exact: true }).click();
  await expect(page.getByRole("radio").first()).toBeVisible();
}

async function pickSlot(page: Page, index: number) {
  const radio = page.getByRole("radio").nth(index);
  await radio.check();
  return (await radio.getAttribute("value"))!;
}

async function confirmBooking(page: Page) {
  await page.getByRole("button", { name: "Confirm appointment", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Your appointment is confirmed", exact: true }),
  ).toBeVisible();
}

// Completes the whole journey for the signed-in customer and returns what was booked.
async function bookAppointment(
  page: Page,
  options: { dayOffset: number; slotIndex: number },
): Promise<Booking> {
  const plate = freshPlate();
  const date = openDay(options.dayOffset);
  await reachAppointment(page, plate);
  await offerSlots(page, date);
  const startsAt = await pickSlot(page, options.slotIndex);
  const branchId = await page.getByLabel("Preferred branch", { exact: true }).inputValue();
  await confirmBooking(page);
  return { plate, branchId, date, startsAt };
}

async function openAppointments(page: Page, date: string) {
  await page.goto(`/account/appointments?from=${date}&to=${date}`);
  await expect(page.getByRole("heading", { name: "Your appointments", exact: true })).toBeVisible();
}

function appointmentCard(page: Page, plate: string) {
  return page.getByRole("article").filter({ hasText: plate });
}

async function offeredSlotsText(context: BrowserContext, page: Page, booking: Booking) {
  const headers = await privateHeaders(context);
  const response = await page.request.get(
    `/api/intake/slots?branchId=${encodeURIComponent(booking.branchId)}&date=${booking.date}`,
    { headers },
  );
  expect(response.status()).toBe(200);
  return response.text();
}

// Presses the button and waits for the rear photo upload to be answered, so the next choice is not ignored mid-upload.
async function clickAndAwaitRearUpload(page: Page, button: Locator) {
  const answered = page.waitForResponse(
    (response) =>
      response.url().includes("/api/intake/photos/") && response.url().includes("angle=Rear"),
  );
  await button.click();
  await answered;
  await expect(button).toBeEnabled();
}

async function expectNoRawCodes(page: Page) {
  const alerts = await page.getByRole("alert").allInnerTexts();
  for (const text of alerts) {
    expect(text.trim(), "raw code shown to the visitor").not.toMatch(rawCodePattern);
  }
}

async function newCustomerPage(browser: Browser, email: string) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await openSellerAs(page, email);
  return { context, page };
}

test.describe("selling a car", () => {
  test.setTimeout(180_000);

  test("SELL-01 a verified customer completes the journey and the booking is listed for the seller and staff", async ({
    page,
    browser,
  }) => {
    const guard = guardPage(page);
    await openSellerAs(page, seller);
    await expectNoSidewaysScroll(page);

    const booking = await bookAppointment(page, { dayOffset: 14, slotIndex: 0 });
    await expect(page.getByText(booking.plate, { exact: true })).toBeVisible();
    await expect(page.getByText(/Reference: /)).toBeVisible();

    await page.getByRole("link", { name: "View your appointments", exact: true }).click();
    await expect(page).toHaveURL(/\/account\/appointments/);
    await openAppointments(page, booking.date);
    const card = appointmentCard(page, booking.plate);
    await expect(card).toHaveCount(1);
    await expect(card.getByText("Booked", { exact: true }).first()).toBeVisible();

    const staff = await browser.newContext();
    const staffPage = await staff.newPage();
    const staffGuard = guardPage(staffPage);
    await signIn(staffPage, "siti.aminah@example.invalid");
    await expect(staffPage).toHaveURL(/\/dashboard\/home/);
    await staffPage.goto("/dashboard/intake");
    await setAppointmentDates(staffPage, booking.date, booking.date);
    await staffPage.getByRole("button", { name: "Apply", exact: true }).click();
    await expect(staffPage.getByText(booking.plate).first()).toBeVisible();
    expectClean(staffGuard, [photoThrottle]);
    await staff.close();
    expectClean(guard, [photoThrottle]);
  });

  test("SELL-02 plates and mileage that are wrong are refused and sloppy plates are normalised", async ({
    page,
  }) => {
    const guard = guardPage(page);
    await openSellerAs(page, seller);
    await fillVehicle(page, "");
    const plate = page.getByLabel("Registration plate");
    const heading = page.getByRole("heading", { name: "Vehicle details", exact: true });

    for (const wrong of ["ABC", "1234", "VCA 6242 X Y"]) {
      await plate.fill(wrong);
      await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
      await expect(page.getByRole("alert").filter({ hasText: /plate/i })).toBeVisible();
      await expect(heading).toBeVisible();
    }

    await plate.fill("   vca 6242");
    await plate.blur();
    await expect(plate).toHaveValue("VCA 6242");
    await plate.fill("vca6242");
    await plate.blur();
    await expect(plate).toHaveValue("VCA 6242");
    await plate.fill("w1234a");
    await plate.blur();
    await expect(plate).toHaveValue("W 1234 A");
    await expect(page.getByRole("alert").filter({ hasText: /plate/i })).toHaveCount(0);

    const mileage = page.getByLabel("Declared mileage (km)");
    for (const wrong of ["-5", "9999999"]) {
      await mileage.fill(wrong);
      await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
      await expect(heading).toBeVisible();
      const valid = await mileage.evaluate((input: HTMLInputElement) => input.validity.valid);
      expect(valid, `mileage ${wrong} accepted by the form`).toBe(false);
    }

    await mileage.fill("");
    await mileage.pressSequentially("abc");
    await expect(mileage).toHaveValue("");
    await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
    await expect(heading).toBeVisible();

    await mileage.fill("0");
    await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
    await expect(
      page
        .getByRole("heading", { name: "Valuation", exact: true })
        .or(page.getByRole("alert").filter({ hasText: /\S/ })),
    ).toBeVisible();
    await expectNoRawCodes(page);
    expectClean(guard, [photoThrottle]);
  });

  test("SELL-03 bad photo files are refused and the step needs four valid photos", async ({
    page,
  }) => {
    const guard = guardPage(page);
    await openSellerAs(page, seller);
    await reachPhotos(page, freshPlate());
    const next = page.getByRole("button", { name: "Choose appointment", exact: true });

    for (const angle of ["Driver", "Passenger", "Front"]) {
      await choosePhoto(page, angle, photo);
    }
    await expect(next).toBeDisabled();

    const huge = {
      name: "huge.jpg",
      mimeType: "image/jpeg",
      buffer: Buffer.alloc(11 * 1024 * 1024, 1),
    };
    await choosePhoto(page, "Rear", huge);
    await expect(page.getByRole("alert").filter({ hasText: /10 MiB/ })).toBeVisible();
    await expect(next).toBeDisabled();

    await choosePhoto(page, "Rear", {
      name: "notes.jpg",
      mimeType: "image/jpeg",
      buffer: Buffer.from("this is plain text, not a picture"),
    });
    await expect(next).toBeEnabled();
    await next.click();
    await expect(page.getByRole("alert").filter({ hasText: /\S/ })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Exterior photos", exact: true })).toBeVisible();
    await expect(page.getByLabel("Preferred branch", { exact: true })).toHaveCount(0);
    await expectNoRawCodes(page);

    await choosePhoto(page, "Rear", {
      name: "empty.jpg",
      mimeType: "image/jpeg",
      buffer: Buffer.alloc(0),
    });
    await clickAndAwaitRearUpload(page, next);
    await expect(page.getByRole("heading", { name: "Exterior photos", exact: true })).toBeVisible();
    await expect(page.getByLabel("Preferred branch", { exact: true })).toHaveCount(0);

    await choosePhoto(page, "Rear", photo);
    await next.click();
    await expect(page.getByLabel("Preferred branch", { exact: true })).toBeVisible();
    expectClean(guard, [/Failed to load resource/]);
  });

  test("SELL-04 reload, Back and Forward between steps leave a usable page", async ({ page }) => {
    const guard = guardPage(page);
    page.on("dialog", (dialog) => void dialog.accept());
    await openSellerAs(page, seller);
    const plate = freshPlate();
    await reachPhotos(page, plate);
    await expect(page).toHaveURL(/step=photos/);

    await page.goBack();
    await expect(page.getByRole("heading", { name: "Valuation", exact: true })).toBeVisible();
    await expect(page.getByText(/Indicative valuation/).first()).toBeVisible();
    await page.goForward();
    await expect(page.getByRole("heading", { name: "Exterior photos", exact: true })).toBeVisible();
    await page.goBack();
    await page.goBack();
    await expect(page.getByRole("heading", { name: "Vehicle details", exact: true })).toBeVisible();
    await expect(page.getByLabel("Registration plate")).toHaveValue(plate);

    await page.goForward();
    await page.goForward();
    await expect(page.getByRole("heading", { name: "Exterior photos", exact: true })).toBeVisible();

    await page.reload();
    const details = page.getByRole("heading", { name: "Vehicle details", exact: true });
    const photos = page.getByRole("heading", { name: "Exterior photos", exact: true });
    await expect(details.or(photos)).toBeVisible();
    if (await details.isVisible()) {
      // Entered work is lost on reload, so the page must be a clean, usable first step.
      await expect(page).not.toHaveURL(/step=photos/);
      await expect(page.getByRole("button", { name: "Get indicative valuation" })).toBeEnabled();
    } else {
      await expect(page.getByLabel("Driver photo", { exact: true })).toBeVisible();
    }
    await expectNoSidewaysScroll(page);
    expectClean(guard, [photoThrottle]);
  });

  test("SELL-05 double-clicking confirm books exactly one appointment", async ({ page }) => {
    const guard = guardPage(page);
    await openSellerAs(page, seller);
    const plate = freshPlate();
    await reachAppointment(page, plate);
    await offerSlots(page, openDay(16));
    await pickSlot(page, 1);

    await page.getByRole("button", { name: "Confirm appointment", exact: true }).dblclick();
    await expect(
      page.getByRole("heading", { name: "Your appointment is confirmed", exact: true }),
    ).toBeVisible();

    await openAppointments(page, openDay(16));
    await expect(appointmentCard(page, plate)).toHaveCount(1);
    expectClean(guard, [photoThrottle]);
  });

  test("SELL-06 a bay taken by another seller is refused with a clear message and fresh bays", async ({
    page,
    browser,
  }) => {
    const guard = guardPage(page);
    await openSellerAs(page, seller);
    const plate = freshPlate();
    const date = openDay(18);
    await reachAppointment(page, plate);
    await offerSlots(page, date);
    const takenStart = await pickSlot(page, 2);

    const rivalSession = await newCustomerPage(browser, rival);
    const rivalPage = rivalSession.page;
    await reachAppointment(rivalPage, freshPlate());
    await offerSlots(rivalPage, date);
    await rivalPage.locator(`input[type=radio][value="${takenStart}"]`).check();
    await confirmBooking(rivalPage);
    await rivalSession.context.close();

    await page.getByRole("button", { name: "Confirm appointment", exact: true }).click();
    await expect(page.getByRole("alert").filter({ hasText: /\S/ })).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Your appointment is confirmed", exact: true }),
    ).toHaveCount(0);
    await expectNoRawCodes(page);

    await page.getByRole("button", { name: "Check available slots", exact: true }).click();
    await expect(page.getByRole("radio").first()).toBeVisible();
    await expect(page.locator(`input[type=radio][value="${takenStart}"]`)).toHaveCount(0);

    await openAppointments(page, date);
    await expect(appointmentCard(page, plate)).toHaveCount(0);
    expectClean(guard, [/Failed to load resource/]);
  });

  test("SELL-07 cancelling from the account frees the bay again", async ({ page, context }) => {
    const guard = guardPage(page);
    await openSellerAs(page, seller);
    const booking = await bookAppointment(page, { dayOffset: 20, slotIndex: 0 });

    expect(await offeredSlotsText(context, page, booking)).not.toContain(booking.startsAt);

    await openAppointments(page, booking.date);
    const card = appointmentCard(page, booking.plate);
    await card.getByRole("button", { name: "Request cancellation", exact: true }).click();
    await page.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
    await expect(card.getByText("Cancelled", { exact: true }).first()).toBeVisible();
    await expect(
      card.getByRole("button", { name: "Request cancellation", exact: true }),
    ).toHaveCount(0);

    expect(await offeredSlotsText(context, page, booking)).toContain(booking.startsAt);
    expectClean(guard, [photoThrottle]);
  });

  test("SELL-08 a visitor who signs in when asked returns to the form with details kept", async ({
    page,
  }) => {
    test.fail(
      true,
      "NEW: the leave guard swallows the sign-in link click, so the vehicle handoff is never saved and the form returns empty",
    );
    const guard = guardPage(page);
    const plate = freshPlate();
    await page.goto("/sell");
    await fillVehicle(page, plate, "65000");
    await requestValuation(page);
    await page.getByRole("button", { name: "Continue to photos", exact: true }).click();
    await expect(
      page.getByRole("link", { name: "Sign in to continue", exact: true }),
    ).toBeVisible();
    await page.getByRole("link", { name: "Sign in to continue", exact: true }).click();
    // Leaving with entered work asks first; the visitor chooses to go and sign in.
    const leave = page.getByRole("button", { name: "Discard and leave", exact: true });
    const email = page.getByLabel("Email", { exact: true });
    await expect(email.or(leave)).toBeVisible();
    if (await leave.isVisible()) {
      await leave.click();
    }

    await page.getByLabel("Email", { exact: true }).fill(seller);
    await page.getByLabel("Password", { exact: true }).fill("Password123!");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/sell/);

    await expect(page.getByLabel("Make", { exact: true })).toHaveValue("Proton");
    await expect(page.getByLabel("Model", { exact: true })).toHaveValue("Saga");
    await expect(page.getByLabel("Trim", { exact: true })).toHaveValue("Standard");
    await expect(page.getByLabel("Manufacture year")).toHaveValue("2020");
    await expect(page.getByLabel("Declared mileage (km)")).toHaveValue("65000");
    await expect(page.getByLabel("Registration plate")).toHaveValue(plate);
    expectClean(guard, [photoThrottle]);
  });
});
