import {
  setAppointmentDates,
  showIntakeQueue,
  exerciseOwnership,
  exerciseManagerHandler,
  captureIntake,
  hideIntakeQueue,
} from "./intake-ui-controls";
import {
  reviewAgentQueue,
  openStaffWorkspace,
  closeStaffWorkspaces,
} from "./staff-workspace-controls";
import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { calendarDate } from "@/lib/calendar";
import { privateHeaders, signInAs } from "./staff-session";
import { actorDestination, type Role } from "@/lib/auth/actor";

test.use({ actionTimeout: 15000 });
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
function appointmentRange() {
  const dates = refs()
    .map((item) => calendarDate(new Date(item.startsAt)))
    .sort();
  return { from: dates[0], to: dates[dates.length - 1] };
}

test("sales intake preserves scoped queues, workbench drafts and an exact lost check-in receipt", async ({
  page,
  context,
}, testInfo) => {
  test.setTimeout(120000);
  await signInAs(page, "aina.sofia@example.invalid", "SalesAgent");
  await page.goto("/dashboard/intake");
  await expect(
    page.getByRole("heading", { name: "Select an appointment", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  await expect(page.getByRole("button", { name: /^My work\b/ })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  const cross = refs().find((item) => item.branchId !== "c77708bf-1a31-42f5-9a69-0c8766697a52")!;
  expect(
    (
      await context.request.get(`/api/staff/intake/appointments/${cross.id}`, {
        headers: await privateHeaders(context),
      })
    ).status(),
  ).toBe(404);
  await page.goto(`/dashboard/intake/${cross.id}`);
  await expect(
    page.getByRole("heading", { name: "Intake appointments", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Retry appointment", exact: true })).toBeVisible();
  await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
  await expect(page.getByText(cross.registrationPlate, { exact: true })).toHaveCount(0);
  await page.route("**/api/staff/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled initial queue failure",
      },
    }),
  );
  await page.goto("/dashboard/intake?view=queue&queue=MyWork");
  await expect(page.getByRole("button", { name: "Retry queue", exact: true })).toBeVisible();
  await page.unroute("**/api/staff/intake/appointments?**");
  await page.getByRole("button", { name: "Retry queue", exact: true }).click();
  await expect(page.getByRole("button", { name: "Retry queue", exact: true })).toHaveCount(0);
  const requests: URL[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname === "/api/staff/intake/appointments") {
      requests.push(url);
    }
  });
  await page.goto(
    "/dashboard/intake?view=queue&queue=MyWork&status=Cancelled&handler=hidden&sort=handlerEmail",
  );
  const arrivalPlate = testInfo.project.name === "mobile" ? "VCA 9702" : "VCA 9701";
  const queue = page.getByRole("complementary", { name: "Intake queues", exact: true });
  const link = queue.getByRole("button", { name: new RegExp(`^${arrivalPlate} `) });
  const arrival = refs().find((item) => item.registrationPlate === arrivalPlate)!;
  const detailHref = `/dashboard/intake/${arrival.id}`;
  await link.click();
  const dialog = page.getByRole("region", { name: "Selected appointment", exact: true });
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeEditable();
  await expect(
    dialog.getByRole("region", { name: "Appointment time and status", exact: true }),
  ).toBeVisible();
  await dialog
    .locator("footer")
    .getByRole("button", { name: "Confirm check-in", exact: true })
    .click();
  await expect(
    page.getByRole("dialog", { name: "Check in this vehicle?", exact: true }),
  ).toHaveCount(0);
  await context.grantPermissions(["clipboard-read", "clipboard-write"], {
    origin: new URL(page.url()).origin,
  });
  await expect(dialog.locator('a[href^="tel:"], a[href^="mailto:"]')).toHaveCount(0);
  await expect(dialog.getByRole("button", { name: "Close appointment", exact: true })).toHaveCount(
    0,
  );
  await dialog.getByRole("button", { name: "Copy email", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "Email address copied." })).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`/dashboard/intake/${arrival.id}$`));
  const detailPath = `/api/staff/intake/appointments/${arrival.id}`;
  const detailReads = counts()[detailPath];
  await page.evaluate(() => window.history.back());
  await expect(
    page.getByRole("heading", { name: "Select an appointment", exact: true }),
  ).toBeVisible();
  await page.evaluate(() => window.history.forward());
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeEditable();
  expect(counts()[detailPath]).toBe(detailReads);
  const reopened = await page.reload();
  expect(await reopened!.text()).not.toContain("Declared mileage");
  await expect(
    page.getByRole("heading", { name: "Intake appointments", exact: true }),
  ).toBeVisible();
  await expect(queue).toBeAttached();
  await expect(page.getByLabel("VIN", { exact: true })).toBeEditable();
  expect(counts()[detailPath] - detailReads).toBe(1);
  await captureIntake(page, "agent-attendance");
  expect(requests.length).toBeGreaterThan(0);
  for (const url of requests) {
    expect(url.searchParams.get("status")).toBe("all");
    expect(url.searchParams.has("handler")).toBe(false);
    expect(url.searchParams.get("sort")).toBe("startsAt");
  }
  await page.goto("/dashboard/intake?view=queue&queue=MyWork");
  await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
  await showIntakeQueue(page);
  await link.click();
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeEditable();
  const beforeDirtyBack = page.url();
  await dialog.getByLabel("VIN", { exact: true }).fill("PL1BT65R84B654321");
  await page.evaluate(() => window.history.back());
  const dirtyBack = page.getByRole("dialog", { name: "Discard unsaved changes?", exact: true });
  await dirtyBack.getByRole("button", { name: "Keep editing", exact: true }).click();
  await expect(page).toHaveURL(beforeDirtyBack);
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue("PL1BT65R84B654321");
  await dialog.getByLabel("VIN", { exact: true }).fill("");
  await exerciseOwnership(page);
  await showIntakeQueue(page);
  const selectedHref = page.url();
  await page.route("**/api/staff/intake/appointments/*", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, uncertain: true, message: "Controlled selection failure" },
    }),
  );
  await queue.locator("li button").filter({ hasNotText: arrivalPlate }).first().click();
  await expect(
    page.getByRole("alert").filter({ hasText: "The record could not be refreshed." }),
  ).toBeVisible();
  await expect(page).toHaveURL(selectedHref);
  await expect(dialog.getByText(arrivalPlate, { exact: true }).first()).toBeVisible();
  await page.unroute("**/api/staff/intake/appointments/*");
  const vin = `PL1BT65R84B${Date.now().toString().slice(-6)}`;
  await dialog.getByLabel("VIN", { exact: true }).fill(vin);
  await showIntakeQueue(page);
  await queue.getByRole("button", { name: /^History\b/ }).click();
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue(vin);
  await openStaffWorkspace(page, "Work calendar");
  const guard = page.getByRole("dialog", { name: "Discard unsaved changes?", exact: true });
  await guard.getByRole("button", { name: "Keep editing", exact: true }).click();
  await closeStaffWorkspaces(page);
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue(vin);
  await queue.getByRole("button", { name: /^My work\b/ }).click();
  await page.route("**/api/staff/intake/appointments/*", (route) =>
    route.fulfill({
      status: 403,
      json: { status: 403, data: null, uncertain: false, message: "Access unavailable" },
    }),
  );
  await page.getByRole("button", { name: "Refresh appointment", exact: true }).click();
  await expect(
    page
      .getByRole("alert")
      .filter({
        hasText:
          "The appointment could not be refreshed. Existing details and your draft remain shown.",
      })
      .last(),
  ).toBeVisible();
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue(vin);
  await page.unroute("**/api/staff/intake/appointments/*");
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
  await page.getByRole("button", { name: "Refresh appointment", exact: true }).click();
  await expect(
    page
      .getByRole("alert")
      .filter({
        hasText:
          "The appointment could not be refreshed. Existing details and your draft remain shown.",
      })
      .last(),
  ).toBeVisible();
  await expect(dialog.getByLabel("VIN", { exact: true })).toHaveValue(vin);
  await page.unroute("**/api/staff/intake/appointments/*");
  await page.getByRole("button", { name: "Refresh appointment", exact: true }).click();
  await expect(
    dialog.getByRole("alert").filter({
      hasText:
        "The appointment could not be refreshed. Existing details and your draft remain shown.",
    }),
  ).toHaveCount(0);
  await hideIntakeQueue(page);
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
  await page.route("**/api/staff/intake/appointments/*/check-ins", async (route) => {
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
  await dialog
    .locator("footer")
    .getByRole("button", { name: "Confirm check-in", exact: true })
    .click();
  const confirmation = page.getByRole("dialog", { name: "Check in this vehicle?", exact: true });
  await confirmation.getByRole("button", { name: "Confirm check-in", exact: true }).click();
  await expect(confirmation).toBeHidden();
  await expect(dialog.getByLabel("VIN", { exact: true })).toBeDisabled();
  await openStaffWorkspace(page, "Work calendar", true);
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await expect(
    dialog.getByRole("button", { name: "Retry same check-in", exact: true }),
  ).toBeVisible();
  const uncertainHref = page.url();
  const attemptedBack = page.waitForEvent("framenavigated");
  await page.evaluate(() => window.history.back());
  await attemptedBack;
  await expect(page).toHaveURL(uncertainHref);
  await expect(
    dialog.getByRole("button", { name: "Retry same check-in", exact: true }),
  ).toBeVisible();
  await dialog.getByRole("button", { name: "Retry same check-in", exact: true }).click();
  await expect(
    dialog.locator("footer").getByText("Check-in confirmed", { exact: true }),
  ).toBeVisible();
  expect(bodies).toHaveLength(2);
  expect(bodies[0]).toBe(bodies[1]);
  writeFileSync(path.join(evidence, "check-in.json"), JSON.stringify(receipt, null, 2));
  await showIntakeQueue(page);
  await expect(queue.getByRole("button", { name: /^History\b/ })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await expect(dialog.getByRole("heading", { name: arrivalPlate, exact: true })).toHaveCount(1);
  await expect(dialog.getByText("Checked in", { exact: true })).toBeVisible();
  await expect(
    dialog.locator("footer").getByText("Check-in confirmed", { exact: true }),
  ).toBeVisible();
  await captureIntake(page, "agent-check-in");
  const completedReads = counts()[detailPath];
  await page.goto(`/dashboard/intake?appointment=${arrival.id}`);
  await expect(dialog.getByText("Checked in", { exact: true })).toBeVisible();
  await expect(
    queue.getByRole("button", { name: /^History\b/, includeHidden: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(page).toHaveURL(new RegExp(`/dashboard/intake/${arrival.id}\\?queue=history$`));
  expect(counts()[detailPath] - completedReads).toBe(1);
  await page.goto(detailHref!);
  await expect(dialog.getByText("Checked in", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Appointment details", exact: true }).click();
  await expect(
    page
      .getByRole("dialog", { name: "Appointment details", exact: true })
      .getByText("90,000 km", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("dialog", { name: "Appointment details", exact: true })
    .getByRole("button", { name: "Close dialog", exact: true })
    .click();
  await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
  await showIntakeQueue(page);
  await queue.locator("li button").filter({ hasNotText: arrivalPlate }).first().click();
  await expect(page).not.toHaveURL(new RegExp(`/dashboard/intake/${arrival.id}`));
  await page.getByRole("button", { name: "Appointment details", exact: true }).click();
  await expect(
    page.getByRole("dialog", { name: "Appointment details", exact: true }),
  ).toBeVisible();
  await page.evaluate(() => window.history.back());
  await expect(page).toHaveURL(new RegExp(`/dashboard/intake/${arrival.id}\\?queue=history$`));
  await expect(dialog.getByRole("heading", { name: arrivalPlate, exact: true })).toBeVisible();

  await page.goto("/dashboard/intake?view=queue&queue=History&pageSize=10");
  await expect(page).toHaveURL(/\/dashboard\/intake\?pageSize=10&queue=history$/);
  await expect(queue.locator("li")).toHaveCount(10);
  const oldJobs = await queue.locator("li").allTextContents();
  expect(oldJobs).toHaveLength(10);
  await page.route("**/api/staff/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled continuation failure",
      },
    }),
  );
  await queue.getByRole("button", { name: "Next records", exact: true }).click();
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "The last confirmed rows and filters remain shown." }),
  ).toBeVisible();
  expect(await queue.locator("li").allTextContents()).toEqual(oldJobs);
  await expect(queue.getByLabel("Page number", { exact: true })).toHaveValue("1");
  await page.unroute("**/api/staff/intake/appointments?**");
  await queue.getByRole("button", { name: "Next records", exact: true }).click();
  await expect(queue.getByLabel("Page number", { exact: true })).toHaveValue("2");
  await expect(page).toHaveURL(/\/dashboard\/intake\?page=2&pageSize=10&queue=history$/);
  await expect(queue.locator("li")).toHaveCount(10);
  const pageTwoJobs = await queue.locator("li").allTextContents();
  expect(pageTwoJobs).not.toEqual(oldJobs);
  await page.evaluate(() => window.history.back());
  await expect(page).toHaveURL(/\/dashboard\/intake\?pageSize=10&queue=history$/);
  await expect(queue.getByLabel("Page number", { exact: true })).toHaveValue("1");
  expect(await queue.locator("li").allTextContents()).toEqual(oldJobs);
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "The last confirmed rows and filters remain shown." }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(queue.getByLabel("Page number", { exact: true })).toHaveValue("1");
  await page.route("**/api/staff/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled failed forward traversal",
      },
    }),
  );
  await page.evaluate(() => window.history.forward());
  await expect(page).toHaveURL(/\/dashboard\/intake\?page=2&pageSize=10&queue=history$/);
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "The last confirmed rows and filters remain shown." }),
  ).toBeVisible();
  await expect(queue.getByLabel("Page number", { exact: true })).toHaveValue("1");
  expect(await queue.locator("li").allTextContents()).toEqual(oldJobs);
  await page.unroute("**/api/staff/intake/appointments?**");
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(queue.getByLabel("Page number", { exact: true })).toHaveValue("2");
  expect(await queue.locator("li").allTextContents()).toEqual(pageTwoJobs);
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
  const oldRows = await queue.locator("li").allTextContents();
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: "The last confirmed rows and filters remain shown." }),
  ).toBeVisible();
  expect(await queue.locator("li").allTextContents()).toEqual(oldRows);
  await page.unroute("**/api/staff/intake/appointments?**");
  await page.getByRole("button", { name: "Refresh worklist", exact: true }).click();
  await expect(queue.locator("li")).toHaveCount(10);
  expect(await queue.locator("li").allTextContents()).toEqual(oldRows);
  await captureIntake(page, "agent-history");
  await reviewAgentQueue(page, "sales", captureIntake);

  await expect(
    page.getByText(
      "The worklist could not be refreshed. The last confirmed rows and filters remain shown.",
      { exact: true },
    ),
  ).toHaveCount(0);
});

const staffRoles: [string, string, Role][] = [
  ["Branch manager", "siti.aminah@example.invalid", "BranchManager"],
  ["General manager", "farah.aziz@example.invalid", "GeneralManager"],
  ["Workshop technician", "azlan.rahim@example.invalid", "WorkshopTechnician"],
  ["Administrator", "muhammad.hafiz@example.invalid", "Administrator"],
];

for (const [role, email, actorRole] of staffRoles) {
  const destination = actorDestination({ userId: "", role: actorRole, branchId: null });

  test(`${role} receives its verified destination and current intake permission`, async ({
    page,
    context,
  }) => {
    await signInAs(page, email, actorRole);
    for (const route of ["/login", "/register"]) {
      await page.goto(route);
      await expect(page).toHaveURL(new RegExp(`${destination}$`));
    }
    if (role === "Workshop technician" || role === "Administrator") {
      const heading = role === "Workshop technician" ? "Workshop" : "Accounts";
      await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
      if (role === "Workshop technician") {
        await reviewAgentQueue(page, "inspection", captureIntake);
      }
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
    await page.goto("/dashboard/intake?view=list");
    await expect(
      page.getByRole("table", { name: "Scoped intake appointments", exact: true }),
    ).toBeVisible();
    const range = appointmentRange();
    await setAppointmentDates(page, range.from, range.to);
    await page.getByRole("button", { name: "Apply", exact: true }).click();
    await expect(page.getByRole("row")).toHaveCount(26);
    if (role === "Branch manager") {
      const rows = await page.locator("tbody tr").allTextContents();
      const refresh = page.getByRole("button", { name: "Refresh worklist", exact: true });
      let reads = 0;
      await page.route("**/api/staff/intake/appointments?**", async (route) => {
        reads++;
        if (reads === 1) {
          await route.fulfill({
            status: 429,
            headers: { "Retry-After": "1" },
            json: { status: 429, data: null, message: "Busy", uncertain: false },
          });
          return;
        }
        await route.continue();
      });
      await refresh.click();
      await expect.poll(() => reads).toBe(1);
      await expect(refresh).toBeDisabled();
      expect(await page.locator("tbody tr").allTextContents()).toEqual(rows);
      await expect(refresh).toBeEnabled();
      expect(reads).toBe(2);
      expect(await page.locator("tbody tr").allTextContents()).toEqual(rows);
      await page.unroute("**/api/staff/intake/appointments?**");
      await page.getByRole("button", { name: "Next records", exact: true }).click();
      await expect(page).toHaveURL(/page=2/);
      expect(await page.locator("tbody tr").allTextContents()).not.toEqual(rows);
      await page.evaluate(() => window.history.back());
      await expect(page).not.toHaveURL(/page=2/);
      expect(await page.locator("tbody tr").allTextContents()).toEqual(rows);
    }
    await exerciseManagerHandler(page);
    const link = page.locator("tbody tr").first();
    const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
    let releaseDetail = () => {};
    const detailGate = new Promise<void>((resolve) => {
      releaseDetail = resolve;
    });
    await page.route("**/api/staff/intake/appointments/*", async (route) => {
      await detailGate;
      await route.continue();
    });
    try {
      await link.click();
      await expect(dialog).toBeVisible();
      await expect(dialog.locator('[data-layout="single"]')).toBeVisible();
    } finally {
      releaseDetail();
    }
    await expect(
      dialog.getByRole("button", { name: "Refresh appointment", exact: true }),
    ).toBeEnabled();
    await page.unroute("**/api/staff/intake/appointments/*");
    if (role === "General manager") {
      await expect(
        dialog.getByRole("button", { name: "Confirm check-in", exact: true }),
      ).toHaveCount(0);
      const id = new URL(page.url()).pathname.split("/").at(-1);
      const denial = await context.request.post(`/api/staff/intake/appointments/${id}/check-ins`, {
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
    await captureIntake(
      page,
      role === "General manager" ? "manager-read-only-drawer" : "manager-appointment-drawer",
    );
    const recordHref = page.url();
    const recordId = new URL(recordHref).pathname.split("/").at(-1)!;
    const readsBeforeHistory = counts()[`/api/staff/intake/appointments/${recordId}`];
    await page.evaluate(() => window.history.back());
    await expect(dialog).toHaveCount(0);
    await expect(
      page.getByRole("table", { name: "Scoped intake appointments", exact: true }),
    ).toBeVisible();
    await page.evaluate(() => window.history.forward());
    await expect(dialog).toBeVisible();
    await expect(page).toHaveURL(recordHref);
    expect(counts()[`/api/staff/intake/appointments/${recordId}`]).toBe(readsBeforeHistory);
    await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page).toHaveURL(/\/dashboard\/intake\?/);
    await page.evaluate(() => window.history.back());
    await expect(dialog).toBeVisible();
    await expect(page).toHaveURL(recordHref);
    expect(counts()[`/api/staff/intake/appointments/${recordId}`]).toBe(readsBeforeHistory);
    await page.evaluate(() => window.history.forward());
    await expect(dialog).toHaveCount(0);
    await expect(page).toHaveURL(/\/dashboard\/intake\?/);
    if (role === "General manager") {
      const shared = await context.newPage();
      await shared.goto(recordHref);
      await expect(
        shared.getByRole("heading", { name: "Intake appointments", exact: true }),
      ).toBeVisible();
      const sharedDrawer = shared.getByRole("dialog", { name: "Intake appointment", exact: true });
      await expect(sharedDrawer).toBeVisible();
      await sharedDrawer.getByRole("button", { name: "Close drawer", exact: true }).click();
      await expect(sharedDrawer).toHaveCount(0);
      await expect(new URL(shared.url()).pathname).toBe("/dashboard/intake");
      await expect(
        shared.getByRole("table", { name: "Scoped intake appointments", exact: true }),
      ).toBeVisible();
      await shared.evaluate(() => window.history.back());
      await expect(sharedDrawer).toBeVisible();
      await expect(shared).toHaveURL(recordHref);
      await shared.close();
      await page.setViewportSize({ width: 390, height: 844 });
      await page.locator("tbody tr").first().click();
      await expect(dialog).toBeVisible();
      await page.evaluate(() => window.history.back());
      await expect(dialog).toHaveCount(0);
      await page.evaluate(() => window.history.forward());
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();
      await expect(dialog).toHaveCount(0);
      await page.setViewportSize({ width: 1440, height: 1024 });
      await page.getByRole("button", { name: "Filter Branch", exact: true }).click();
      await page
        .getByRole("combobox", { name: "Branch", exact: true })
        .selectOption("68f371ad-223a-43d6-b45b-b878eece79ae");
      const branchRead = page.waitForResponse(
        (response) =>
          new URL(response.url()).pathname === "/api/staff/intake/appointments" &&
          new URL(response.url()).searchParams.get("branchId") ===
            "68f371ad-223a-43d6-b45b-b878eece79ae" &&
          response.status() === 200,
      );
      await page.getByRole("button", { name: "Apply", exact: true }).click();
      const branchRows = (await (await branchRead).json()).data.items;
      expect(branchRows.length).toBeGreaterThan(0);
      expect(
        branchRows.every(
          (row: { branchId: string }) => row.branchId === "68f371ad-223a-43d6-b45b-b878eece79ae",
        ),
      ).toBe(true);
      await expect(
        page.getByRole("cell", { name: "ApexAuto Petaling Jaya", exact: true }),
      ).toHaveCount(branchRows.length);
      await page.evaluate(() => window.history.back());
      await expect(page).not.toHaveURL(/branchId=/);
      await page.evaluate(() => window.history.back());
      await expect(dialog).toBeVisible();
    }
  });
}
