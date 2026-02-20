import { setAppointmentDates, expectWorklistPage } from "./intake-ui-controls";
import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const assignment = (command: string) =>
  writeFileSync(path.join(evidence, "assignment.txt"), command);
const day = (offset: number) =>
  new Date(Date.now() + (offset * 24 + 8) * 3600000).toISOString().slice(0, 10);

test("staff reassignment clears old branch records and applies current permissions", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await setAppointmentDates(page, day(0), day(29));
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expectWorklistPage(page, 2);
  const selectedName = await page
    .getByRole("link", { name: /^Open / })
    .first()
    .getAttribute("aria-label");
  await page.getByRole("link", { name: selectedName!, exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  await dialog.getByLabel("VIN", { exact: true }).fill("PL1BT65R84B123456");
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  const navigation = page.getByRole("link", { name: "Intake appointments", exact: true });
  async function navigateWorkspace() {
    if (!(await navigation.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    const refreshed = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/dashboard/intake" &&
        response.request().headers()["rsc"] === "1",
    );
    await navigation.click();
    await refreshed;
    await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  }
  try {
    assignment("second-branch");
    await navigateWorkspace();
    await expect(page.getByRole("link", { name: /^Open VCA / })).toHaveCount(0);
    await expect(page.getByLabel("Branch", { exact: true })).toHaveCount(0);
    await setAppointmentDates(page, day(0), day(29));
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await expect(page.getByRole("link", { name: /^Open BPL / }).first()).toBeVisible();
    assignment("group");
    await navigateWorkspace();
    await expect(page.getByText("General manager", { exact: true })).toBeAttached();
    if (!(await navigation.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    await expect(page.getByText("General manager", { exact: true })).toBeVisible();
    await setAppointmentDates(page, day(0), day(29));
    await expect(page.getByLabel("Branch", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await page
      .getByRole("link", { name: /^Open / })
      .first()
      .click();
    await expect(dialog.getByText("Your role has read-only intake access.")).toBeVisible();
    await expect(dialog.getByLabel("VIN", { exact: true })).toHaveCount(0);
  } finally {
    assignment("restore");
    await page.reload();
    await expect(page.getByText("Sales agent", { exact: true })).toBeAttached();
    if (await dialog.isVisible()) {
      await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
    }
    if (!(await navigation.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    await expect(page.getByText("Sales agent", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Branch", { exact: true })).toHaveCount(0);
  }
});

test("failed and cancelled view reads retain confirmed content, with fresh history reads", async ({
  page,
  context,
}) => {
  test.setTimeout(120000);
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  const errors: string[] = [];
  let documentRequests = 0;
  let successfulTableReads = 0;
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => {
    if (request.isNavigationRequest()) {
      documentRequests += 1;
    }
  });
  page.on("response", (response) => {
    if (response.url().includes("/api/staff/intake/appointments?") && response.status() === 200) {
      successfulTableReads += 1;
    }
  });
  const tableRoute = "**/api/staff/intake/appointments?**";
  await page.route(tableRoute, (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled read failure", uncertain: true },
    }),
  );
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByText(/The worklist could not be refreshed/)).toBeVisible();
  await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toBeVisible();
  expect(new URL(page.url()).searchParams.get("view")).not.toBe("list");
  await page.unroute(tableRoute);

  const network = await context.newCDPSession(page);
  await network.send("Network.enable");
  await network.send("Network.emulateNetworkConditions", {
    offline: false,
    latency: 350,
    downloadThroughput: 50_000,
    uploadThroughput: 25_000,
  });
  let release = () => {};
  let observed = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const requested = new Promise<void>((resolve) => {
    observed = resolve;
  });
  await page.route(tableRoute, async (route) => {
    const response = await route.fetch();
    observed();
    await gate;
    if (!route.request().failure()) {
      await route.fulfill({ response });
    }
  });
  try {
    await page.getByRole("link", { name: "Table", exact: true }).click();
    await requested;
    await expect(page.getByRole("navigation", { name: "Appointment views" })).toHaveAttribute(
      "aria-busy",
      "true",
    );
    await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toBeVisible();
    const aborted = page.waitForEvent("requestfailed", (request) =>
      request.url().includes("/api/staff/intake/appointments?"),
    );
    await page.keyboard.press("Escape");
    await aborted;
    await expect(page.getByRole("navigation", { name: "Appointment views" })).toHaveAttribute(
      "aria-busy",
      "false",
    );
    await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toBeVisible();
  } finally {
    release();
  }
  await page.unroute(tableRoute);
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toBeVisible();
  await page.getByRole("link", { name: "Calendar", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  await page.goBack();
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  expect(successfulTableReads).toBe(2);
  await page.goForward();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  expect(documentRequests).toBe(0);
  expect(errors).toEqual([]);
});

test("restoring an appointment detail applies changed staff permissions", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.getByRole("link", { name: "Table", exact: true }).click();
  const href = await page
    .getByRole("link", { name: /^Open / })
    .first()
    .getAttribute("href");
  await page.goto(href!);
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
    await page.reload();
  }
});

test("direct appointment back link retains an uncertain check-in operation", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.goto("/dashboard/intake?view=list&status=Booked");
  const href = await page.getByRole("link", { name: /^Open VCA 9701/ }).getAttribute("href");
  await page.goto(href!);
  await page.getByLabel("VIN", { exact: true }).fill("PL1BT65R84B123456");
  await page.getByLabel("Actual odometer (km)", { exact: true }).fill("92000");
  for (const checkbox of await page.getByRole("checkbox").all()) {
    await checkbox.check();
  }
  await page.route("**/api/staff/intake/appointments/*/check-in", (route) =>
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
  await expect(
    page.getByRole("button", { name: "Retry same check-in", exact: true }),
  ).toBeVisible();
  const current = page.url();
  const unexpectedNavigation = page
    .waitForRequest(
      (request) => request.isNavigationRequest() || request.headers()["rsc"] === "1",
      { timeout: 750 },
    )
    .then(
      () => true,
      () => false,
    );
  await page.getByRole("link", { name: "Back to intake appointments", exact: true }).click();
  expect(await unexpectedNavigation).toBe(false);
  await expect(page).toHaveURL(current);
  await expect(
    page.getByRole("button", { name: "Retry same check-in", exact: true }),
  ).toBeVisible();
});
