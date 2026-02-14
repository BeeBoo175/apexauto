import { setAppointmentDates, expectWorklistPage } from "./intake-ui-controls";
import { expect, test, type CDPSession } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.AUTH_EVIDENCE_DIR!;
const assignment = (command: string) =>
  writeFileSync(path.join(evidence, "assignment.txt"), command);
const day = (offset: number) =>
  new Date(Date.now() + (offset * 24 + 8) * 3600000).toISOString().slice(0, 10);

async function swipe(touch: CDPSession, x: number, y: number, deltaX: number, deltaY: number) {
  await touch.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x, y }] });
  for (let step = 1; step <= 8; step += 1) {
    await touch.send("Input.dispatchTouchEvent", {
      type: "touchMove",
      touchPoints: [{ x: x + (deltaX * step) / 8, y: y + (deltaY * step) / 8 }],
    });
    // Give Chromium a frame between movements in the simulated finger swipe.
    await new Promise((resolve) => setTimeout(resolve, 16));
  }
  // Finish the finger movement before lifting so the next tap does not stop a fling.
  await new Promise((resolve) => setTimeout(resolve, 100));
  await touch.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
}

test("intake content supports mobile touch scrolling and preserves desktop viewport fit", async ({
  page,
  context,
}, testInfo) => {
  const mobile = testInfo.project.name === "mobile-auth";
  if (mobile) {
    await page.setViewportSize({ width: 390, height: 640 });
  }
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  if (!mobile) {
    const bounds = await page
      .getByRole("region", { name: "Intake calendar", exact: true })
      .boundingBox();
    expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(page.viewportSize()!.height);
    return;
  }
  const events = page.getByRole("region", { name: "Selected date events", exact: true });
  const lastEvent = events.getByRole("button", { name: /^Open / }).last();
  const before = await lastEvent.evaluate((node) => node.getBoundingClientRect().top);
  const touch = await context.newCDPSession(page);
  for (let attempt = 0; attempt < 3; attempt += 1) {
    await swipe(touch, 190, 550, 0, -420);
    const visible = await lastEvent.evaluate((node) => {
      const bounds = node.getBoundingClientRect();
      return bounds.top >= 0 && bounds.bottom <= window.innerHeight;
    });
    if (visible) {
      break;
    }
  }
  await expect
    .poll(() => lastEvent.evaluate((node) => node.getBoundingClientRect().top))
    .toBeLessThan(before - 50);
  await expect(lastEvent).toBeInViewport({ ratio: 1 });
  await lastEvent.tap();
  await expect(page.getByRole("dialog", { name: "Intake appointment", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Close dialog", exact: true }).tap();
  await page.screenshot({
    path: path.join(evidence, "mobile-calendar-scrolled.png"),
    fullPage: true,
  });

  await page.getByRole("link", { name: "Table", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  const rows = page.getByRole("table", { name: "Scoped intake appointments" }).locator("..");
  const bounds = (await rows.boundingBox())!;
  const startY = bounds.y + bounds.height - 20;
  await swipe(touch, 190, startY, 0, -180);
  await expect.poll(() => rows.evaluate((node) => node.scrollTop)).toBeGreaterThan(0);
  await swipe(touch, 330, startY, -260, 0);
  await expect.poll(() => rows.evaluate((node) => node.scrollLeft)).toBeGreaterThan(0);
  await expect(page.getByRole("button", { name: "Next records", exact: true })).toBeInViewport({
    ratio: 1,
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: path.join(evidence, "mobile-table-scrolled.png"), fullPage: true });
  await touch.detach();
});

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

test("slow and failed view reads retain the staff shell and header, with fresh history reads", async ({
  page,
  context,
}) => {
  test.setTimeout(120000);
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
  const sidebar = page.locator('aside[aria-label="Dealership operations"]');
  const header = page
    .locator("header")
    .filter({ has: page.getByRole("heading", { name: "Intake appointments", exact: true }) });
  const originalSidebar = await sidebar.elementHandle();
  const originalHeader = await header.elementHandle();
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
    expect(await sidebar.evaluate((node, original) => node === original, originalSidebar)).toBe(
      true,
    );
    expect(await header.evaluate((node, original) => node === original, originalHeader)).toBe(true);
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
  expect(await sidebar.evaluate((node, original) => node === original, originalSidebar)).toBe(true);
  expect(await header.evaluate((node, original) => node === original, originalHeader)).toBe(true);
  await page.screenshot({
    path: path.join(evidence, "persistent-intake-shell.png"),
    fullPage: true,
  });
});

test("staff sidebar survives navigation from appointment detail back to intake", async ({
  page,
}) => {
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
  const sidebar = page.locator('aside[aria-label="Dealership operations"]');
  const originalSidebar = await sidebar.elementHandle();
  await page.getByRole("link", { name: "Back to intake appointments", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Intake appointments", exact: true }),
  ).toBeVisible();
  expect(await sidebar.evaluate((node, original) => node === original, originalSidebar)).toBe(true);
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
  await page.getByRole("link", { name: "Table", exact: true }).click();
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
