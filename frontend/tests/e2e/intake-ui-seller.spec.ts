import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { expect, test, type Page } from "@playwright/test";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function vehicle(page: Page) {
  await page.getByLabel("Make", { exact: true }).selectOption("Proton");
  await page.getByLabel("Model", { exact: true }).selectOption("Saga");
  await page.getByLabel("Trim", { exact: true }).selectOption("Standard");
  await page.getByLabel("Manufacture year").fill("2020");
  await page.getByLabel("Declared mileage (km)").fill("90000");
  await page.getByLabel("Registration plate").fill(`ui ${Date.now().toString().slice(-6)}`);
}

test("seller journey retains vehicle handoff, confirms independent uploads and replays a lost booking receipt", async ({
  page,
}) => {
  test.setTimeout(180_000);
  const before = counts();

  const initial = await page.goto("/sell");
  expect(initial!.status()).toBe(200);
  expect(await initial!.text()).toContain("Proton");
  await expect(
    page.getByRole("button", { name: "Get indicative valuation", exact: true }),
  ).toBeEnabled();
  const after = counts();

  expect((after["/api/taxonomy"] ?? 0) - (before["/api/taxonomy"] ?? 0)).toBe(1);
  expect((after["/api/branches"] ?? 0) - (before["/api/branches"] ?? 0)).toBe(1);
  await vehicle(page);

  const plate = await page.getByLabel("Registration plate").inputValue();
  await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Valuation", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Continue to photos", exact: true }).click();
  await page.getByRole("link", { name: "Sign in to continue", exact: true }).click();
  await page.getByLabel("Email", { exact: true }).fill("ahmad.faizal@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/sell$/);
  await expect(page.getByLabel("Registration plate")).toHaveValue(plate);
  await page.getByRole("button", { name: "Get indicative valuation", exact: true }).click();
  await page.getByRole("button", { name: "Continue to photos", exact: true }).click();

  let lostUpload = true;
  const uploadedIds: string[] = [];
  await page.route("**/api/intake/photos/*?angle=Driver", async (route) => {
    uploadedIds.push(route.request().url());
    const response = await route.fetch();
    if (lostUpload) {
      lostUpload = false;
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          uncertain: true,
          message: "Controlled lost upload receipt",
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  for (const angle of ["Driver", "Passenger", "Front", "Rear"]) {
    const section = page.getByRole("region", { name: `${angle} photo upload`, exact: true });
    await section
      .getByLabel(`${angle} photo`, { exact: true })
      .setInputFiles(path.join(evidence, "upload.jpg"));
  }
  await page.getByRole("button", { name: "Choose appointment", exact: true }).click();
  const driverSection = page.getByRole("region", { name: "Driver photo upload", exact: true });
  await expect(driverSection.getByLabel("Driver photo", { exact: true })).toBeDisabled();
  await driverSection.getByRole("button", { name: "Retry same upload", exact: true }).click();
  for (const angle of ["Driver", "Passenger", "Front", "Rear"]) {
    const section = page.getByRole("region", { name: `${angle} photo upload`, exact: true });
    await expect(
      page.getByRole("status").filter({ hasText: `${angle} photo confirmed.` }),
    ).toBeVisible();
    await expect(section.locator("p[role=status]")).toHaveCount(0);
  }
  expect(uploadedIds).toHaveLength(2);
  expect(uploadedIds[0]).toBe(uploadedIds[1]);
  await expect(page.getByText("Rear photo confirmed.", { exact: true })).toHaveCount(0);

  await page.getByRole("button", { name: "Choose appointment", exact: true }).click();
  await page
    .getByLabel("Preferred branch", { exact: true })
    .selectOption({ label: "ApexAuto Glenmarie — Shah Alam" });
  const day = new Date(Date.now() + (14 * 24 + 8) * 60 * 60 * 1000);
  if (day.getUTCDay() === 0) day.setUTCDate(day.getUTCDate() + 1);
  await page.getByLabel("Appointment date (Malaysia)").fill(day.toISOString().slice(0, 10));
  await page.getByRole("button", { name: "Check available slots", exact: true }).click();
  await page.getByRole("radio").last().check();

  let driftQuote = true;
  await page.route("**/api/intake/appointments", async (route) => {
    const body = route.request().postDataJSON();
    if (driftQuote) {
      driftQuote = false;
      body.acceptedQuote.maxValuation += 1;
    }
    const response = await route.fetch({ postData: JSON.stringify(body) });
    expect(response.status()).toBe(409);
    await route.fulfill({ response });
  });
  await page.getByRole("button", { name: "Confirm appointment", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Accept updated valuation", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Confirm appointment", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Accept updated valuation", exact: true }).click();
  await page.unroute("**/api/intake/appointments");
  let loseReceipt = true;
  const bodies: string[] = [];
  await page.route("**/api/intake/appointments", async (route) => {
    bodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (loseReceipt) {
      loseReceipt = false;
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
  await page.getByRole("button", { name: "Confirm appointment", exact: true }).click();
  await expect(page.getByLabel("Appointment date (Malaysia)")).toBeDisabled();
  await page.getByRole("button", { name: "Retry same booking", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Your appointment is confirmed", exact: true }),
  ).toBeVisible();
  expect(bodies).toHaveLength(2);
  expect(bodies[0]).toBe(bodies[1]);
  await expect(page.getByText("Request confirmed.", { exact: true })).toHaveCount(0);

  writeFileSync(
    path.join(evidence, "seller-booking.json"),
    JSON.stringify({ ...JSON.parse(bodies[0]), password: undefined }, null, 2),
  );
});
