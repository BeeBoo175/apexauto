import { expect, test, type Page } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;

async function login(page: Page, email: string, destination: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`${destination}$`));
}

test("no-show is a visible state and a manager can reconcile original-date attendance", async ({
  page,
  context,
}, testInfo) => {
  test.setTimeout(120000);
  const mobile = testInfo.project.name === "mobile";
  const plate = mobile ? "BPL 6243" : "VCA 6243";
  const records = JSON.parse(readFileSync(path.join(evidence, "references.json"), "utf8")) as {
    id: string;
    registrationPlate: string;
    startsAt: string;
  }[];
  const appointment = records.find((item) => item.registrationPlate === plate)!;
  const date = new Date(Date.parse(appointment.startsAt) + 8 * 3600000).toISOString().slice(0, 10);

  await login(
    page,
    mobile ? "kavitha.rajan@example.invalid" : "tan.hui.min@example.invalid",
    "/account",
  );
  await page.getByLabel("From (Malaysia)", { exact: true }).fill(date);
  await page.getByLabel("To (Malaysia)", { exact: true }).fill(date);
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(page.getByText("No show", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Request cancellation", exact: true })).toHaveCount(
    0,
  );

  await context.clearCookies();

  await login(
    page,
    mobile ? "daniel.wong@example.invalid" : "aina.sofia@example.invalid",
    "/dashboard/intake",
  );
  await page.goto(`/dashboard/intake/${appointment.id}`);
  await expect(page.getByText("No show", { exact: true })).toBeVisible();
  await expect(page.getByText(/Only the same-branch BranchManager/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Confirm attendance correction", exact: true }),
  ).toHaveCount(0);
  await context.clearCookies();

  await login(
    page,
    mobile ? "lim.mei.ling@example.invalid" : "siti.aminah@example.invalid",
    "/dashboard/intake",
  );
  const response = await page.goto(`/dashboard/intake?view=list&status=NoShow`);
  expect(await response!.text()).not.toContain(plate);
  expect(response!.headers()["cache-control"]).toContain("no-store");
  await expect(page.getByRole("link", { name: `Open ${plate}`, exact: true })).toBeVisible();
  await page.goto(`/dashboard/intake?view=calendar&status=NoShow&date=${date}`);
  if (mobile) {
    await page
      .getByRole("table", { name: "Month dates", exact: true })
      .getByRole("button", { name: /, 1 events?$/ })
      .click();
  }
  await expect(page.getByRole("button", { name: new RegExp(plate) }).first()).toBeVisible();
  await page.goto(`/dashboard/intake?view=list&status=NoShow&appointment=${appointment.id}`);
  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  await expect(dialog.getByLabel("Correction reason", { exact: true })).toBeEditable();
  await dialog.getByLabel("Actual attendance time (Malaysia)", { exact: true }).fill("09:00");
  await dialog
    .getByLabel("Correction reason", { exact: true })
    .fill("Arrival confirmed from the branch attendance log.");
  await dialog
    .getByLabel("VIN", { exact: true })
    .fill(mobile ? "PL1BT65R84B123461" : "PL1BT65R84B123460");
  await dialog.getByLabel("Actual odometer (km)", { exact: true }).fill("92500");
  for (const checkbox of await dialog.getByRole("checkbox").all()) {
    await checkbox.check();
  }
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  await page.getByRole("link", { name: `Open ${plate}`, exact: true }).click();
  await expect(dialog.getByLabel("Correction reason", { exact: true })).toHaveValue(
    "Arrival confirmed from the branch attendance log.",
  );
  await page.route(`**/api/staff/intake/appointments/${appointment.id}`, (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled read failure",
      },
    }),
  );
  await dialog.getByRole("button", { name: "Refresh appointment", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Existing details and your draft remain shown" }),
  ).toBeVisible();
  await expect(dialog.getByLabel("Actual attendance time (Malaysia)", { exact: true })).toHaveValue(
    "09:00",
  );
  await page.unroute(`**/api/staff/intake/appointments/${appointment.id}`);

  const bodies: string[] = [];
  let receipt: unknown;
  await page.route(
    `**/api/staff/intake/appointments/${appointment.id}/no-show-corrections`,
    async (route) => {
      bodies.push(route.request().postData()!);
      const result = await route.fetch();
      expect(result.status()).toBe(200);
      receipt = (await result.json()).data;
      if (bodies.length === 1) {
        await route.fulfill({
          status: 502,
          json: { status: 502, data: null, uncertain: true, message: "Controlled lost receipt" },
        });
        return;
      }
      await route.fulfill({ response: result });
    },
  );
  await dialog.getByRole("button", { name: "Confirm attendance correction", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Close dialog", exact: true })).toBeDisabled();
  await expect(dialog.getByLabel("Correction reason", { exact: true })).toBeDisabled();
  await dialog.getByRole("button", { name: "Retry same correction", exact: true }).click();
  await expect(
    dialog.getByRole("heading", { name: "Check-in confirmed", exact: true }),
  ).toBeVisible();
  expect(bodies).toHaveLength(2);
  expect(bodies[0]).toBe(bodies[1]);
  writeFileSync(path.join(evidence, "no-show-correction.json"), JSON.stringify(receipt, null, 2));
  await page.goto(`/dashboard/intake/${appointment.id}`);
  await expect(page.getByText("Checked in", { exact: true })).toBeVisible();
  await expect(page.getByText(/Attendance corrected/)).toBeVisible();
  await expect(
    page.getByText("Arrival confirmed from the branch attendance log.", { exact: true }),
  ).toBeVisible();
  await expect(page.getByText(/Recorded no-show effective/)).toBeVisible();
  await page.reload();
  await expect(page.getByText("Checked in", { exact: true })).toBeVisible();
});
