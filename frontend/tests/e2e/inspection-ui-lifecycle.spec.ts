import { expect, test, type Page } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { privateHeaders, signInAs } from "./staff-session";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
async function screenshot(page: Page, name: string) {
  await expect(page.locator('[aria-label="Notifications"] > *')).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: path.join(evidence, `${name}.png`), fullPage: false });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
}

async function selectPoint(page: Page, code: string) {
  await page.getByRole("searchbox", { name: "Find a checklist point", exact: true }).fill(code);
  await page
    .getByRole("navigation", { name: "Inspection points" })
    .getByRole("button", { name: new RegExp(`^${code} `) })
    .click();
}

async function loseFirstReceipt(page: Page, endpoint: string) {
  const bodies: string[] = [];
  await page.route(endpoint, async (route) => {
    bodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (bodies.length === 1) {
      await route.fulfill({
        status: 502,
        json: { status: 502, data: null, message: "Controlled lost receipt", uncertain: true },
      });
      return;
    }
    await route.fulfill({ response });
  });
  return bodies;
}

test("inspection claim, retained draft/photo, exact reconciliation, signed report and repair lifecycle", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(240_000);
  const mobile = info.project.name === "mobile";
  const technician = mobile ? "amirul.izzat@example.invalid" : "farid.hakim@example.invalid";
  const managerEmail = mobile ? "nur.syafiqah@example.invalid" : "siti.aminah@example.invalid";
  const counts = async () => {
    let snapshot: Record<string, number> | null = null;
    await expect
      .poll(() => {
        try {
          snapshot = JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
          return true;
        } catch {
          return false;
        }
      })
      .toBe(true);
    return snapshot!;
  };
  const before = (await counts())["/api/staff/inspection/workshop/jobs"] ?? 0;
  await signInAs(page, technician, "WorkshopTechnician");
  await expect(page.getByRole("button", { name: "Refresh workshop", exact: true })).toBeEnabled();
  expect((await counts())["/api/staff/inspection/workshop/jobs"] - before).toBe(3);
  await expect(page.getByRole("heading", { name: "Select a job", exact: true })).toBeVisible();
  await screenshot(page, "inspection-queue");
  await page
    .getByRole("button", { name: /^To claim\b/ })
    .last()
    .click();
  const claimButton = page.getByRole("button", { name: /^Claim inspection ·/ }).first();
  const registration = (await claimButton.getAttribute("aria-label"))!.split(" · ")[1];
  const claimableJobs = page
    .getByRole("region", { name: "Available to claim", exact: true })
    .getByRole("button", { name: /^Claim inspection ·/ });
  const confirmedJobs = await claimableJobs.allTextContents();
  await page.route("**/api/staff/inspection/workshop/jobs?*", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled failed queue", uncertain: true },
    }),
  );
  await page.getByRole("button", { name: "Refresh workshop", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "last confirmed worklists" }),
  ).toBeVisible();
  expect(await claimableJobs.allTextContents()).toEqual(confirmedJobs);
  await page.unroute("**/api/staff/inspection/workshop/jobs?*");
  await page
    .getByRole("button", { name: `Claim inspection · ${registration}`, exact: true })
    .click();
  const assignmentBodies = await loseFirstReceipt(
    page,
    "**/api/staff/inspection/vehicles/*/assignments",
  );
  // A technician's claim confirms in place; the first confirmation submits the captured request.
  await page.getByRole("button", { name: "Confirm assignment", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Reconcile assignment", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("dialog").getByRole("button", { name: "Close dialog", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Reconcile assignment", exact: true }).click();
  expect(assignmentBodies).toHaveLength(2);
  expect(assignmentBodies[1]).toBe(assignmentBodies[0]);
  await expect(page).toHaveURL(/\/dashboard\/workshop\/[a-f0-9-]+/);
  const id = new URL(page.url()).pathname.split("/")[3];
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  await page.unroute("**/api/staff/inspection/vehicles/*/assignments");
  const header = page.getByRole("heading", { level: 1 });
  await header.evaluate((element) => {
    (element as HTMLElement & { retained?: boolean }).retained = true;
  });
  await selectPoint(page, "EP-004");
  await page.getByLabel("EP-004 disposition", { exact: true }).selectOption("Finding");
  await page.getByLabel("EP-004 severity", { exact: true }).selectOption("MinorDefect");
  await page.getByLabel("EP-004 estimate (MYR)", { exact: true }).fill("250.10");
  await page
    .getByLabel("EP-004 finding notes", { exact: true })
    .fill("Bumper scuff requires paint repair.");
  await page
    .getByLabel("Add JPEG, PNG or WebP (up to 10 MiB)", { exact: true })
    .setInputFiles(path.join(evidence, "upload.jpg"));
  await page.getByRole("button", { name: "Upload photo", exact: true }).click();
  await expect(page.getByText("Private evidence · 1 of 4 photos", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("img", { name: "EP-004 inspection evidence", exact: true }),
  ).toBeVisible();
  await page.getByRole("checkbox", { name: /^I have examined all/ }).check();
  await screenshot(page, "inspection-editor");
  await page.route(`**/api/staff/inspection/drafts/${id}/workspace`, (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        message: "Controlled failed editor refresh",
        uncertain: true,
      },
    }),
  );
  await page.getByRole("button", { name: "Refresh workshop", exact: true }).click();
  await expect(page.getByLabel("EP-004 estimate (MYR)", { exact: true })).toHaveValue("250.10");
  await page.unroute(`**/api/staff/inspection/drafts/${id}/workspace`);
  await expect(page.getByRole("navigation", { name: "Workshop sections" })).toHaveCount(0);
  await expect(
    page.locator("footer").getByText("Unsaved inspection changes", { exact: true }),
  ).toBeVisible();
  await expect(
    page.locator("footer").getByRole("button", { name: "Save draft", exact: true }),
  ).toBeEnabled();
  const bodies = await loseFirstReceipt(page, `**/api/staff/inspection/drafts/${id}/saves`);
  await page.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Reconcile draft save", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Workshop sections" })).toHaveCount(0);
  expect(page.url()).toContain(id);
  await page.getByRole("button", { name: "Reconcile draft save", exact: true }).click();
  expect(bodies).toHaveLength(2);
  expect(bodies[1]).toBe(bodies[0]);
  await expect(page.getByRole("button", { name: "Sign inspection", exact: true })).toBeEnabled();
  expect(
    (await counts())[
      `/api/staff/inspection/drafts/${id}/photos/${JSON.parse(bodies[0]).photoIds[0]}/receipt`
    ] ?? 0,
  ).toBe(0);
  await page.unroute(`**/api/staff/inspection/drafts/${id}/saves`);
  expect(
    await header.evaluate((element) => (element as HTMLElement & { retained?: boolean }).retained),
  ).toBe(true);
  await page.getByRole("button", { name: "Sign inspection", exact: true }).click();
  await page.getByRole("button", { name: "Confirm sign-off", exact: true }).click();
  await expect(
    page.getByRole("link", { name: "Open signed report · revision 1", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("navigation", { name: "Workshop sections" })
    .getByRole("button", { name: "Repairs", exact: true })
    .click();
  await page.getByRole("button", { name: "Request repair work", exact: true }).click();
  await expect(
    page.locator("footer").getByText("Edit repair work and budget", { exact: true }),
  ).toBeVisible();
  await page.getByRole("checkbox", { name: "Include in request", exact: true }).check();
  await page
    .getByLabel("EP-004 repair work", { exact: true })
    .fill("  Repair bumper scuff and inspect finish.  ");
  await page.getByLabel("Requested budget (MYR)", { exact: true }).fill("250.10");
  await page
    .getByRole("navigation", { name: "Workshop sections" })
    .getByRole("button", { name: "Inspection", exact: true })
    .click();
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toHaveCount(0);
  await page
    .getByRole("navigation", { name: "Workshop sections" })
    .getByRole("button", { name: "Repairs", exact: true })
    .click();
  await expect(
    page.locator("footer").getByText("Edit repair work and budget", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Refresh workshop", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh workshop", exact: true })).toBeEnabled();
  await expect(page.getByLabel("EP-004 repair work", { exact: true })).toHaveValue(
    "  Repair bumper scuff and inspect finish.  ",
  );
  await expect(page.getByLabel("Requested budget (MYR)", { exact: true })).toHaveValue("250.10");
  const requestBodies = await loseFirstReceipt(
    page,
    `**/api/staff/inspection/drafts/${id}/repair-requests`,
  );
  await page.getByRole("button", { name: "Submit request", exact: true }).click();
  await page
    .getByRole("dialog", { name: "Submit this repair request?", exact: true })
    .getByRole("button", { name: "Submit request", exact: true })
    .click();
  await expect(page.getByRole("button", { name: "Reconcile request", exact: true })).toBeVisible();
  await expect(page.getByLabel("EP-004 repair work", { exact: true })).toBeDisabled();
  await expect(page.getByLabel("Requested budget (MYR)", { exact: true })).toBeDisabled();
  await expect(
    page
      .getByRole("navigation", { name: "Workshop sections" })
      .getByRole("button", { name: "History", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Reconcile request", exact: true }).click();
  await expect(
    page
      .getByRole("region", { name: "Selected workshop job", exact: true })
      .getByText("Needs approval", { exact: true }),
  ).toBeVisible();
  expect(requestBodies).toHaveLength(2);
  expect(requestBodies[1]).toBe(requestBodies[0]);
  expect(JSON.parse(requestBodies[0])).toMatchObject({
    budget: 250.1,
    items: [{ pointCode: "EP-004", work: "Repair bumper scuff and inspect finish." }],
  });
  await page.unroute(`**/api/staff/inspection/drafts/${id}/repair-requests`);

  const managerContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const manager = await managerContext.newPage();
  await signInAs(manager, managerEmail, "BranchManager");
  await manager.goto("/dashboard/intake");
  await expect(
    manager.getByRole("button", { name: "Refresh worklist", exact: true }),
  ).toBeEnabled();
  await screenshot(manager, "intake-parity-reference");
  await manager.goto("/dashboard/workshop?view=approval");
  await expect(
    manager.getByRole("button", { name: "Refresh workshop", exact: true }),
  ).toBeEnabled();
  await screenshot(manager, "repair-queue");
  await manager.getByRole("button", { name: "Filter Current work", exact: true }).click();
  await manager.getByRole("radio", { name: "Needs approval", exact: true }).check();
  await manager.getByRole("button", { name: "Apply", exact: true }).click();
  await expect(
    manager.getByRole("button", { name: "Remove workStatus filter: Needs approval", exact: true }),
  ).toBeVisible();
  await expect(
    manager.getByRole("button", { name: "Refresh workshop", exact: true }),
  ).toBeEnabled();
  await manager.getByRole("searchbox", { name: "Search workshop", exact: true }).fill(registration);
  await manager.getByRole("searchbox", { name: "Search workshop", exact: true }).press("Enter");
  await expect.poll(() => new URL(manager.url()).searchParams.get("search")).toBe(registration);
  await expect(manager.locator("tbody tr")).toHaveCount(1);
  await manager.getByRole("button", { name: "Filter Registration", exact: true }).click();
  await manager.getByLabel("Registration contains", { exact: true }).fill(registration);
  await manager.getByRole("button", { name: "Apply", exact: true }).click();
  await expect
    .poll(() => new URL(manager.url()).searchParams.get("registration"))
    .toBe(registration);
  await manager.getByRole("button", { name: "Filter Requested budget", exact: true }).click();
  await manager.getByLabel("Minimum (MYR)", { exact: true }).fill("250.10");
  await manager.getByLabel("Maximum (MYR)", { exact: true }).fill("250.10");
  await manager.getByRole("button", { name: "Apply", exact: true }).click();
  await expect.poll(() => new URL(manager.url()).searchParams.get("minimumBudget")).toBe("250.10");
  await expect(manager.locator("tbody tr")).toHaveCount(1);
  await manager.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  const repairDownload = manager.waitForEvent("download");
  await manager.getByRole("button", { name: "Export selected CSV", exact: true }).click();
  const repairFile = await (await repairDownload).path();
  const repairCsv = readFileSync(repairFile!, "utf8");
  expect(repairCsv.replace(/^\uFEFF/, "").split("\r\n")[0]).toBe(
    '"Registration","Vehicle","Current work","Assigned technician","Branch","Requested budget","Checked in","Repair requested"',
  );
  expect(repairCsv).toContain('"Needs approval"');
  expect(repairCsv).toContain('"250.10"');
  expect(repairCsv.split("\r\n")).toHaveLength(3);
  await manager.getByRole("button", { name: "Clear selection", exact: true }).click();
  await manager.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  await manager.locator("tbody tr").getByRole("cell").nth(2).click();
  await expect(manager.getByRole("dialog", { name: registration, exact: true })).toBeVisible();
  await expect(
    manager
      .getByRole("dialog", { name: registration, exact: true })
      .getByRole("button", { name: "Refresh inspection", exact: true }),
  ).toBeEnabled();
  await expect(
    manager.getByText("Bumper scuff requires paint repair.", { exact: true }),
  ).toBeVisible();
  const repairRequestId = JSON.parse(requestBodies[0]).operationId as string;
  await screenshot(manager, "repair-review");
  await expect(manager.getByRole("button", { name: /bulk.*approv/i })).toHaveCount(0);
  const approvalBodies = await loseFirstReceipt(
    manager,
    `**/api/staff/inspection/drafts/${id}/repair-approvals`,
  );
  await manager.getByRole("button", { name: "Approve budget", exact: true }).click();
  await manager
    .getByRole("dialog", { name: "Approve this repair budget?", exact: true })
    .getByRole("button", { name: "Approve budget", exact: true })
    .click();
  await expect(
    manager.getByRole("button", { name: "Reconcile action", exact: true }),
  ).toBeVisible();
  await expect(
    manager
      .getByRole("dialog", { name: registration, exact: true })
      .getByRole("button", { name: "Close drawer", exact: true }),
  ).toBeDisabled();
  await manager.getByRole("button", { name: "Reconcile action", exact: true }).click();
  await expect(
    manager
      .getByRole("dialog", { name: registration, exact: true })
      .getByText("Repairs pending", { exact: true }),
  ).toBeVisible();
  expect(approvalBodies).toHaveLength(2);
  expect(approvalBodies[1]).toBe(approvalBodies[0]);
  expect(JSON.parse(approvalBodies[0])).not.toHaveProperty("notes");
  await manager.unroute(`**/api/staff/inspection/drafts/${id}/repair-approvals`);
  await page.getByRole("button", { name: "Refresh workshop", exact: true }).click();
  await page
    .getByLabel("Completion & reinspection notes", { exact: true })
    .fill("  Bumper repair and finish checked. Reinspection complete.  ");
  await page.getByRole("button", { name: "Refresh workshop", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh workshop", exact: true })).toBeEnabled();
  await expect(page.getByLabel("Completion & reinspection notes", { exact: true })).toHaveValue(
    "  Bumper repair and finish checked. Reinspection complete.  ",
  );
  const completionBodies = await loseFirstReceipt(
    page,
    `**/api/staff/inspection/drafts/${id}/repair-completions`,
  );
  await page.getByRole("button", { name: "Complete repairs", exact: true }).click();
  await page
    .getByRole("dialog", { name: "Complete these repairs?", exact: true })
    .getByRole("button", { name: "Complete repairs", exact: true })
    .click();
  await expect(page.getByRole("button", { name: "Reconcile action", exact: true })).toBeVisible();
  await expect(page.getByLabel("Completion & reinspection notes", { exact: true })).toBeDisabled();
  await expect(
    page
      .getByRole("navigation", { name: "Workshop sections" })
      .getByRole("button", { name: "History", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Reconcile action", exact: true }).click();
  const completedSummary = page.getByRole("region", { name: "Vehicle and confirmed assessment" });
  await expect(completedSummary.getByText("Ready for sale", { exact: true })).toBeVisible();
  await expect(completedSummary.getByText("Revision 1 · Signed", { exact: true })).toBeVisible();
  expect(completionBodies).toHaveLength(2);
  expect(completionBodies[1]).toBe(completionBodies[0]);
  expect(JSON.parse(completionBodies[0]).notes).toBe(
    "Bumper repair and finish checked. Reinspection complete.",
  );
  await page.unroute(`**/api/staff/inspection/drafts/${id}/repair-completions`);
  await page
    .getByRole("navigation", { name: "Workshop sections" })
    .getByRole("button", { name: "History", exact: true })
    .click();
  const history = page.getByRole("region", { name: "Record history", exact: true });
  await expect(history.getByRole("link", { name: "Open repair request", exact: true })).toHaveCount(
    1,
  );
  await expect(history.getByRole("button", { name: "Show repair history" })).toHaveCount(0);
  await expect(
    history.getByText("Bumper repair and finish checked. Reinspection complete.", { exact: true }),
  ).toBeVisible();
  await screenshot(page, "inspection-current-history");
  const reportHref = await page
    .getByRole("link", { name: "Open signed report · revision 1", exact: true })
    .getAttribute("href");
  // The signed report is a retained PDF served inline, so the browser fetches it with the signed-in cookie.
  const reportDocument = await page.evaluate(async (url) => {
    const response = await fetch(url, { credentials: "include" });
    const bytes = Array.from(new Uint8Array(await response.arrayBuffer()));
    return {
      status: response.status,
      type: response.headers.get("content-type"),
      disposition: response.headers.get("content-disposition"),
      bytes,
    };
  }, reportHref!);
  expect(reportDocument.status).toBe(200);
  expect(reportDocument.type).toContain("application/pdf");
  expect(reportDocument.disposition).toContain("inline");
  const reportBytes = Buffer.from(reportDocument.bytes);
  expect(reportBytes.subarray(0, 5).toString("latin1")).toBe("%PDF-");
  writeFileSync(path.join(evidence, "signed-report.pdf"), reportBytes);

  await manager
    .getByRole("dialog", { name: registration, exact: true })
    .getByRole("button", { name: "Close drawer", exact: true })
    .click();
  await manager.goto(`/dashboard/workshop/${id}`);
  await manager.getByRole("button", { name: "Reopen inspection", exact: true }).click();
  await manager
    .getByLabel("Reopening reason", { exact: true })
    .fill("  Inspect newly reported suspension noise.  ");
  await manager
    .getByRole("dialog", { name: "Reopen signed inspection", exact: true })
    .getByRole("button", { name: "Close dialog", exact: true })
    .click();
  await manager
    .getByRole("dialog", { name: "Discard unsaved changes?", exact: true })
    .getByRole("button", { name: "Keep editing", exact: true })
    .click();
  await expect(manager.getByLabel("Reopening reason", { exact: true })).toHaveValue(
    "  Inspect newly reported suspension noise.  ",
  );
  const reopeningBodies = await loseFirstReceipt(
    manager,
    `**/api/staff/inspection/drafts/${id}/reopenings`,
  );
  await manager.getByRole("button", { name: "Confirm reopening", exact: true }).click();
  await manager
    .getByRole("dialog", { name: "Reopen this signed inspection?", exact: true })
    .getByRole("button", { name: "Reopen inspection", exact: true })
    .click();
  await expect(
    manager.getByRole("button", { name: "Reconcile reopening", exact: true }),
  ).toBeVisible();
  await expect(manager.getByLabel("Reopening reason", { exact: true })).toBeDisabled();
  await expect(
    manager
      .getByRole("dialog", { name: "Reopen signed inspection", exact: true })
      .getByRole("button", { name: "Close dialog", exact: true }),
  ).toBeDisabled();
  await manager.getByRole("button", { name: "Reconcile reopening", exact: true }).click();
  const reopenedSummary = manager.getByRole("region", { name: "Vehicle and confirmed assessment" });
  await expect(reopenedSummary.getByText("Inspecting", { exact: true }).first()).toBeVisible();
  await expect(reopenedSummary.getByText("Revision 2 · Draft", { exact: true })).toBeVisible();
  expect(reopeningBodies).toHaveLength(2);
  expect(reopeningBodies[1]).toBe(reopeningBodies[0]);
  expect(JSON.parse(reopeningBodies[0]).reason).toBe("Inspect newly reported suspension noise.");
  await manager.unroute(`**/api/staff/inspection/drafts/${id}/reopenings`);
  await manager
    .getByRole("dialog", { name: registration, exact: true })
    .getByRole("button", { name: "Close drawer", exact: true })
    .click();
  await manager.goto(`/dashboard/workshop/${id}/repairs/${repairRequestId}/revisions/1`);
  await expect(
    manager.getByRole("heading", { name: "Retained repair request", exact: true }),
  ).toBeVisible();
  await expect(
    manager.getByText("Bumper scuff requires paint repair.", { exact: true }),
  ).toBeVisible();
  await expect(manager.getByRole("button", { name: "Approve budget", exact: true })).toHaveCount(0);
  await manager
    .locator("footer")
    .getByRole("button", { name: "Back to current repairs", exact: true })
    .click();
  await expect(manager.getByRole("heading", { name: "Retained repair request" })).toHaveCount(0);
  await manager
    .getByRole("dialog", { name: registration, exact: true })
    .getByRole("button", { name: "Close drawer", exact: true })
    .click();
  await manager.goto(`/dashboard/workshop/${id}`);
  const selected = await context.request.get(`/api/staff/inspection/reports/${id}/revisions/1`, {
    headers: await privateHeaders(context),
  });
  expect(selected.status()).toBe(200);
  const report = (await selected.json()).data;
  expect(report.assessment.estimatedRepairCost).toBe(250.1);
  expect(report.revision).toBe(1);
  writeFileSync(
    path.join(evidence, "inspection.json"),
    JSON.stringify({
      inspectionId: id,
      reportOperationId: report.operationId,
      originalEstimate: report.assessment.estimatedRepairCost,
      assignmentReplay: assignmentBodies[0] === assignmentBodies[1],
      saveReplay: bodies[0] === bodies[1],
      repairRequestReplay: requestBodies[0] === requestBodies[1],
      approvalReplay: approvalBodies[0] === approvalBodies[1],
      completionReplay: completionBodies[0] === completionBodies[1],
      reopeningReplay: reopeningBodies[0] === reopeningBodies[1],
      checklistPoints: report.checklist.length,
    }),
  );
  await page.goto(`/dashboard/workshop/${id}`);
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  await selectPoint(page, "EP-004");
  await page.getByLabel("EP-004 finding notes", { exact: true }).fill("Retained local findings.");
  await page
    .getByRole("navigation", { name: "Workshop sections" })
    .getByRole("button", { name: "History", exact: true })
    .click();
  const previousHistory = `**/api/staff/inspection/reports/${id}/revisions/1/repair-history?*`;
  await page.route(previousHistory, (route) => route.fulfill({ status: 502, body: "Unavailable" }));
  await history.getByRole("button", { name: "Show repair history", exact: true }).click();
  await expect(history.getByRole("button", { name: "Retry repair requests" })).toBeVisible();
  await page.unroute(previousHistory);
  await history.getByRole("button", { name: "Retry repair requests" }).click();
  await expect(history.getByRole("link", { name: "Open repair request", exact: true })).toHaveCount(
    1,
  );
  await screenshot(page, "inspection-previous-history");
  await history.getByRole("link", { name: "Open repair request", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Retained repair request" })).toBeVisible();
  await expect(page.locator("footer").getByRole("button", { name: "Save draft" })).toBeEnabled();
  await screenshot(page, "inspection-retained-footer");
  await page.locator("footer").getByRole("button", { name: "Back to current repairs" }).click();
  await page
    .getByRole("navigation", { name: "Workshop sections" })
    .getByRole("button", { name: "Inspection", exact: true })
    .click();
  await expect(page.getByLabel("EP-004 finding notes", { exact: true })).toHaveValue(
    "Retained local findings.",
  );
  await page.getByLabel("EP-004 severity", { exact: true }).selectOption("CriticalDisqualifier");
  await page
    .getByLabel("EP-004 finding notes", { exact: true })
    .fill("Structural accident evidence requires rejection.");
  await page.getByRole("checkbox", { name: /^I have examined all/ }).check();
  await page.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(page.getByRole("button", { name: "Sign inspection", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Sign inspection", exact: true }).click();
  await page.getByRole("button", { name: "Confirm sign-off", exact: true }).click();
  await expect(completedSummary.getByText("Rejected", { exact: true }).first()).toBeVisible();
  await expect(completedSummary.getByText("Revision 2 · Signed", { exact: true })).toBeVisible();
  const rejectedHref = await page
    .getByRole("link", { name: "Open signed report · revision 2", exact: true })
    .getAttribute("href");
  const rejectedReport = await page.evaluate(async (url) => {
    const response = await fetch(url, { credentials: "include" });
    const bytes = new Uint8Array(await response.arrayBuffer());
    return {
      status: response.status,
      type: response.headers.get("content-type"),
      size: bytes.length,
    };
  }, rejectedHref!);
  expect(rejectedReport.status).toBe(200);
  expect(rejectedReport.type).toContain("application/pdf");
  expect(rejectedReport.size).toBeGreaterThan(1000);
  await manager.getByRole("button", { name: "Refresh inspection", exact: true }).click();
  await manager.getByRole("button", { name: "Reopen inspection", exact: true }).click();
  await manager
    .getByLabel("Reopening reason", { exact: true })
    .fill("Review accident evidence in a new retained revision.");
  await manager.getByRole("button", { name: "Confirm reopening", exact: true }).click();
  await manager
    .getByRole("dialog", { name: "Reopen this signed inspection?", exact: true })
    .getByRole("button", { name: "Reopen inspection", exact: true })
    .click();
  await expect(reopenedSummary.getByText("Inspecting", { exact: true }).first()).toBeVisible();
  await expect(reopenedSummary.getByText("Revision 3 · Draft", { exact: true })).toBeVisible();
  await managerContext.close();
  const salesContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const sales = await salesContext.newPage();
  await signInAs(sales, "aina.sofia@example.invalid", "SalesAgent");
  await sales.goto(`/dashboard/workshop/${id}`);
  await expect(
    sales.getByRole("heading", { name: "Workshop access unavailable", exact: true }),
  ).toBeVisible();
  expect(
    (
      await salesContext.request.get(`/api/staff/inspection/drafts/${id}`, {
        headers: await privateHeaders(salesContext),
      })
    ).status(),
  ).toBe(403);
  const changed = (await salesContext.cookies()).filter(
    (cookie) => cookie.name === "apexauto_token",
  );
  await context.addCookies(changed);
  await page.goto(`/dashboard/workshop/${id}`);
  await expect(
    page.getByRole("heading", { name: "Workshop access unavailable", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("Bumper scuff requires paint repair.", { exact: true })).toHaveCount(
    0,
  );
  await salesContext.close();
  const groupContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const group = await groupContext.newPage();
  await signInAs(group, "farah.aziz@example.invalid", "GeneralManager");
  await group.goto("/dashboard/workshop");
  await expect(group.getByRole("button", { name: /^Claim inspection ·/ })).toHaveCount(0);
  await group.getByLabel("Rows per page", { exact: true }).selectOption("10");
  await expect.poll(() => new URL(group.url()).searchParams.get("pageSize")).toBe("10");
  await group.getByRole("button", { name: "Next records", exact: true }).click();
  await expect.poll(() => new URL(group.url()).searchParams.get("page")).toBe("2");
  const fullDownload = group.waitForEvent("download");
  await group.getByRole("button", { name: "Export full CSV", exact: true }).click();
  const fullFile = await (await fullDownload).path();
  expect(readFileSync(fullFile!, "utf8").trim().split("\r\n").length).toBeGreaterThan(26);
  await group.goto(`/dashboard/workshop/${id}`);
  await expect(group.getByRole("button", { name: "Save draft", exact: true })).toHaveCount(0);
  await expect(group.getByRole("button", { name: "Reassign technician", exact: true })).toHaveCount(
    0,
  );
  await groupContext.close();
});
