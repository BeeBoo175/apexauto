import { setAppointmentDates, expectWorklistPage } from "./intake-ui-controls";
import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const refs = () =>
  JSON.parse(readFileSync(path.join(evidence, "references.json"), "utf8")) as {
    id: string;
    branchId: string;
    registrationPlate: string;
    startsAt: string;
  }[];
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;
const day = (offset: number) =>
  new Date(Date.now() + (offset * 24 + 8) * 3600000).toISOString().slice(0, 10);

async function privateHeaders(context: BrowserContext) {
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  // APIRequestContext does not apply Chromium's loopback Secure-cookie exception.
  return { Cookie: `apexauto_token=${cookie.value}` };
}

async function login(page: Page, email: string, destination: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`${destination}$`));
}

test("sales intake preserves scoped lists, drawer drafts and an exact lost check-in receipt", async ({
  page,
  context,
}, testInfo) => {
  test.setTimeout(120000);
  await login(page, "aina.sofia@example.invalid", "/dashboard/intake");
  await page.goto("/dashboard/intake?view=list&status=Booked");
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  const cross = refs().find((item) => item.branchId !== "c77708bf-1a31-42f5-9a69-0c8766697a52")!;
  expect(
    (
      await context.request.get(`/api/staff/intake/appointments/${cross.id}`, {
        headers: await privateHeaders(context),
      })
    ).status(),
  ).toBe(404);
  expect(
    (
      await context.request.get(`/dashboard/intake/${cross.id}`, {
        headers: await privateHeaders(context),
      })
    ).status(),
  ).toBe(404);
  const arrivalPlate = testInfo.project.name === "mobile" ? "VCA 9702" : "VCA 9701";
  const link = page.getByRole("link", { name: `Open ${arrivalPlate}`, exact: true });
  const detailHref = await link.getAttribute("href");
  await link.click();
  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeEditable();
  const detailPath = `/api/staff/intake/appointments/${new URL(page.url()).searchParams.get("appointment")}`;
  const detailReads = counts()[detailPath];
  const reopened = await page.reload();
  expect(await reopened!.text()).toContain("Declared mileage");
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeEditable();
  expect(counts()[detailPath] - detailReads).toBe(1);
  const vin = `PL1BT65R84B${Date.now().toString().slice(-6)}`;
  await dialog.getByLabel("VIN", { exact: true }).fill(vin);
  await page.route("**/api/staff/intake/appointments/*", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled detail refresh failure",
      },
    }),
  );
  await dialog.getByRole("button", { name: "Refresh appointment", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Existing details and your draft remain shown." }),
  ).toBeVisible();
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue(vin);
  await page.unroute("**/api/staff/intake/appointments/*");
  await dialog.getByRole("button", { name: "Refresh appointment", exact: true }).click();
  await expect(
    dialog.getByRole("alert").filter({ hasText: "Existing details and your draft remain shown." }),
  ).toHaveCount(0);
  await dialog.getByLabel("Actual odometer (km)", { exact: true }).fill("92000");
  for (const checkbox of await dialog.getByRole("checkbox").all()) {
    await checkbox.check();
  }
  await dialog
    .getByLabel("Notes (optional)", { exact: true })
    .fill("Customer attended with vehicle and ownership documents.");
  let loseReceipt = true;
  const bodies: string[] = [];
  let receipt: unknown;
  await page.route("**/api/staff/intake/appointments/*/check-in", async (route) => {
    bodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    receipt = (await response.json()).data;
    if (loseReceipt) {
      loseReceipt = false;
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          uncertain: true,
          message: "Controlled lost check-in receipt",
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await dialog.getByRole("button", { name: "Confirm check-in", exact: true }).click();
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeDisabled();
  await expect(dialog.getByRole("button", { name: "Close dialog", exact: true })).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await expect(
    dialog.getByRole("button", { name: "Retry same check-in", exact: true }),
  ).toBeVisible();
  const uncertainHref = page.url();
  await page.evaluate(() => {
    const current = window.location.href;
    window.history.replaceState(null, "", "/dashboard/intake?view=list");
    window.history.pushState(null, "", current);
  });
  const attemptedBack = page.waitForEvent("framenavigated");
  await page.evaluate(() => window.history.back());
  await attemptedBack;
  await expect(page).toHaveURL(uncertainHref);
  await expect(
    dialog.getByRole("button", { name: "Retry same check-in", exact: true }),
  ).toBeVisible();
  await dialog.getByRole("button", { name: "Retry same check-in", exact: true }).click();
  await expect(
    dialog.getByRole("heading", { name: "Check-in confirmed", exact: true }),
  ).toBeVisible();
  expect(bodies).toHaveLength(2);
  expect(bodies[0]).toBe(bodies[1]);
  writeFileSync(path.join(evidence, "check-in.json"), JSON.stringify(receipt, null, 2));
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();

  await page.goto(detailHref!);
  await expect(page.getByText("Checked in", { exact: true })).toBeVisible();
  await expect(page.getByText("90,000 km", { exact: true })).toBeVisible();
  await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);

  await page.goto("/dashboard/intake?view=list");
  await setAppointmentDates(page, day(0), day(29));
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(page.getByRole("row")).toHaveCount(26);
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expectWorklistPage(page, 2);
  expect(await page.getByRole("row").count()).toBeGreaterThan(1);
  expect(await page.getByRole("row").count()).toBeLessThan(26);
  await page.route("**/api/staff/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled failed worklist refresh",
      },
    }),
  );
  const oldRows = await page.getByRole("row").count();
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "The last confirmed rows and filters remain shown." }),
  ).toBeVisible();
  await expect(page.getByRole("row")).toHaveCount(oldRows);
  await page.unroute("**/api/staff/intake/appointments?**");
  await page.getByRole("button", { name: "Previous records", exact: true }).click();
  await expectWorklistPage(page, 1);

  await expect(
    page.getByText(
      "The worklist could not be refreshed. The last confirmed rows and filters remain shown.",
      { exact: true },
    ),
  ).toHaveCount(0);
});

for (const [role, email, destination] of [
  ["Branch manager", "siti.aminah@example.invalid", "/dashboard/intake"],
  ["General manager", "farah.aziz@example.invalid", "/dashboard/intake"],
  ["Workshop technician", "azlan.rahim@example.invalid", "/dashboard/inspection"],
  ["Administrator", "muhammad.hafiz@example.invalid", "/dashboard"],
]) {
  test(`${role} receives its verified destination and current intake permission`, async ({
    page,
    context,
  }) => {
    await login(page, email, destination);
    for (const route of ["/login", "/register"]) {
      await page.goto(route);
      await expect(page).toHaveURL(new RegExp(`${destination}$`));
    }
    if (role === "Workshop technician" || role === "Administrator") {
      const heading = role === "Workshop technician" ? "Inspections" : "Your staff workspace";
      await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
      await page.goto("/dashboard/intake?view=list");
      await expect(
        page.getByRole("heading", { name: "Intake access unavailable", exact: true }),
      ).toBeVisible();
      expect(
        (
          await context.request.get("/api/staff/intake/appointments", {
            headers: await privateHeaders(context),
          })
        ).status(),
      ).toBe(403);
      return;
    }
    await page.getByRole("link", { name: "Table", exact: true }).click();
    await setAppointmentDates(page, day(-10), day(29));
    await page
      .getByRole("radiogroup", { name: "Status", exact: true })
      .getByRole("radio", { name: "All statuses", exact: true })
      .check();
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await expect(page.getByRole("row")).toHaveCount(26);
    const link = page.getByRole("link", { name: /^Open / }).first();
    await link.click();
    const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
    await expect(
      dialog.getByRole("button", { name: "Refresh appointment", exact: true }),
    ).toBeEnabled();
    if (role === "General manager") {
      await expect(dialog.getByText("Your role has read-only intake access.")).toBeVisible();
      await expect(
        dialog.getByRole("button", { name: "Confirm check-in", exact: true }),
      ).toHaveCount(0);
      const id = new URL(page.url()).searchParams.get("appointment");
      const denial = await context.request.post(`/api/staff/intake/appointments/${id}/check-in`, {
        headers: { ...(await privateHeaders(context)), Origin: new URL(page.url()).origin },
        data: {
          vin: "PL1BT65R84B123456",
          mileageKilometres: 90000,
          customerPresent: true,
          identityConfirmed: true,
          ownershipConfirmed: true,
          vehicleConfirmed: true,
          notes: null,
        },
      });
      expect(denial.status()).toBe(403);
    }
    await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
    if (role === "General manager") {
      await page.getByRole("button", { name: /^Filters/ }).click();
      await page
        .getByLabel("Branch", { exact: true })
        .selectOption("68f371ad-223a-43d6-b45b-b878eece79ae");
      await page.getByRole("button", { name: "Apply filters", exact: true }).click();
      await expect(
        page.getByRole("cell", { name: "ApexAuto Petaling Jaya", exact: true }),
      ).toHaveCount(25);
    }
  });
}
