import { openStaffWorkspace, showIntakeQueue } from "./intake-ui-controls";
import { expect, test, type Page } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

test.use({ actionTimeout: 15_000 });

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const assignment = (command: string) =>
  writeFileSync(path.join(evidence, "assignment.txt"), command);

async function login(page: Page, email = "aina.sofia@example.invalid") {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
}

function arrivalId() {
  const records = JSON.parse(readFileSync(path.join(evidence, "references.json"), "utf8")) as {
    id: string;
    registrationPlate: string;
  }[];
  return records.find((item) => item.registrationPlate === "VCA 9701")!.id;
}

test("staff reassignment clears old branch records and applies current permissions", async ({
  page,
}) => {
  await login(page);
  await page.goto(`/dashboard/intake?appointment=${arrivalId()}`);
  await page.getByLabel("VIN", { exact: true }).fill("PL1BT65R84B123456");
  const queue = page.getByRole("complementary", { name: "Intake queues", exact: true });
  page.on("dialog", (dialog) => dialog.accept());
  try {
    assignment("second-branch");
    await page.reload();
    await expect(page.getByText("VCA 9701", { exact: true })).toHaveCount(0);
    await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
    await expect(page.getByLabel("Branch", { exact: true })).toHaveCount(0);
    await expect(queue).toBeAttached();
    await showIntakeQueue(page);
    await queue.getByRole("button", { name: /^To claim\b/ }).click();
    await expect(queue.getByRole("button", { name: /^BPL / }).first()).toBeVisible();
    assignment("group");
    await page.reload();
    await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toBeVisible();
    await expect(queue).toHaveCount(0);
    const drawer = page.getByRole("dialog", { name: "Intake appointment", exact: true });
    if (await drawer.isVisible()) {
      await drawer.getByRole("button", { name: "Close drawer", exact: true }).click();
    }
    await page.locator("tbody tr").first().click();
    await expect(drawer.getByText("Your role has read-only intake access.")).toBeVisible();
    await expect(drawer.getByLabel("VIN", { exact: true })).toHaveCount(0);
  } finally {
    await page.keyboard.press("Escape");
    assignment("restore");
    await page.goto("/dashboard/intake");
    await expect(queue).toBeVisible();
    await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toHaveCount(0);
    await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
  }
});

test("failed queue replacement retains confirmed content and workspace history reads fresh data", async ({
  page,
}) => {
  test.setTimeout(120_000);
  await login(page);
  const queue = page.getByRole("complementary", { name: "Intake queues", exact: true });
  const confirmed = await queue.locator("li").allTextContents();
  const errors: string[] = [];
  let documentRequests = 0;
  let successfulReads = 0;
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => {
    if (request.isNavigationRequest()) {
      documentRequests += 1;
    }
  });
  page.on("response", (response) => {
    if (
      new URL(response.url()).pathname === "/api/staff/intake/appointments" &&
      response.status() === 200
    ) {
      successfulReads += 1;
    }
  });
  const routePattern = "**/api/staff/intake/appointments?**";
  await page.route(routePattern, (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled read failure", uncertain: true },
    }),
  );
  await queue.getByRole("button", { name: /^History\b/ }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "The worklist could not be refreshed" }),
  ).toBeVisible();
  expect(await queue.locator("li").allTextContents()).toEqual(confirmed);
  await expect(queue.getByRole("button", { name: /^My work\b/ })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await page.unroute(routePattern);
  await openStaffWorkspace(page, "Work calendar");
  await expect(
    page.getByRole("button", { name: "Refresh work calendar", exact: true }),
  ).toBeEnabled();
  await page.goBack();
  await expect(queue).toBeVisible();
  await expect.poll(() => successfulReads).toBe(1);
  await page.goForward();
  await expect(
    page.getByRole("button", { name: "Refresh work calendar", exact: true }),
  ).toBeEnabled();
  expect(documentRequests).toBe(0);
  expect(errors).toEqual([]);
});

test("restoring an appointment detail applies changed staff permissions", async ({ page }) => {
  await login(page);
  await page.goto(`/dashboard/intake/${arrivalId()}`);
  await expect(page.getByLabel("VIN", { exact: true })).toBeEditable();
  await page.getByRole("link", { name: "Back to intake appointments", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Intake appointments", exact: true }),
  ).toBeVisible();
  try {
    assignment("group");
    await page.goBack();
    await expect(page.getByText("Your role has read-only intake access.")).toBeVisible();
    await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
    await expect(page.getByText("General manager", { exact: true })).toBeAttached();
  } finally {
    assignment("restore");
    await page.goto("/dashboard/intake");
  }
});

test("direct appointment back link retains an uncertain check-in operation", async ({ page }) => {
  await login(page);
  await page.goto(`/dashboard/intake/${arrivalId()}`);
  await page.getByLabel("VIN", { exact: true }).fill("PL1BT65R84B123456");
  await page.getByLabel("Actual odometer (km)", { exact: true }).fill("92000");
  for (const checkbox of await page.getByRole("checkbox").all()) {
    await checkbox.check();
  }
  await page.route("**/api/staff/intake/appointments/*/check-ins", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        message: "Controlled unconfirmed check-in",
        uncertain: true,
      },
    }),
  );
  await page.getByRole("button", { name: "Confirm check-in", exact: true }).click();
  await page
    .getByRole("dialog", { name: "Check in this vehicle?", exact: true })
    .getByRole("button", { name: "Confirm check-in", exact: true })
    .click();
  const retry = page.getByRole("button", { name: "Retry same check-in", exact: true });
  await expect(retry).toBeVisible();
  const current = page.url();
  const back = page.getByRole("link", { name: "Back to intake appointments", exact: true });
  await expect(back).toHaveAttribute("aria-disabled", "true");
  await back.click({ force: true });
  await expect(page).toHaveURL(current);
  await expect(retry).toBeVisible();
});
