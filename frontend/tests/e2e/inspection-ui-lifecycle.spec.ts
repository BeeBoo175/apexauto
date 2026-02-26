import { expect, test, type Page, type BrowserContext } from "@playwright/test";
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
async function privateHeaders(context: BrowserContext) {
  const cookie = (await context.cookies()).find((c) => c.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}` };
}
async function screenshot(page: Page, name: string) {
  await expect(page.locator('[aria-label="Notifications"] > *')).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: path.join(evidence, `${name}.png`), fullPage: false });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
}

test("inspection claim, retained draft/photo, exact reconciliation, signed report and repair lifecycle", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(240_000);
  const mobile = info.project.name === "mobile";
  const technician = mobile ? "mohd.firdaus@example.invalid" : "azlan.rahim@example.invalid";
  const managerEmail = mobile ? "nur.syafiqah@example.invalid" : "siti.aminah@example.invalid";
  const counts = () =>
    JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;
  const before = counts()["/api/staff/inspection/vehicles"] ?? 0;
  await login(page, technician, "/dashboard/inspection");
  await expect(
    page.getByRole("button", { name: "Refresh inspections", exact: true }),
  ).toBeEnabled();
  expect(counts()["/api/staff/inspection/vehicles"] - before).toBe(1);
  await screenshot(page, "inspection-queue");
  if (mobile) {
    const session = await context.newCDPSession(page);
    await session.send("Input.dispatchTouchEvent", {
      type: "touchStart",
      touchPoints: [{ x: 210, y: 700 }],
    });
    await session.send("Input.dispatchTouchEvent", {
      type: "touchMove",
      touchPoints: [{ x: 210, y: 280 }],
    });
    await session.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
    await expect
      .poll(() => page.getByRole("table").evaluate((table) => table.parentElement!.scrollTop))
      .toBeGreaterThan(0);
    await expect(page.getByRole("navigation", { name: "Table pages", exact: true })).toBeVisible();
    await session.detach();
  }
  await page.getByRole("button", { name: /^Filters/ }).click();
  await page.getByRole("radio", { name: "Unassigned", exact: true }).check();
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Remove status filter: Unassigned", exact: true }),
  ).toBeVisible();
  await page.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  const selectedDownload = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export selected CSV", exact: true }).click();
  const selectedFile = await (await selectedDownload).path();
  expect(readFileSync(selectedFile!, "utf8")).toContain('"Registration","Vehicle","Branch"');
  await page.getByRole("button", { name: "Clear selection", exact: true }).click();
  await expect(page.getByRole("button", { name: "Export full CSV", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Claim inspection", exact: true })).toHaveCount(1);
  const confirmedRow = await page.getByRole("table").innerText();
  await page.route("**/api/staff/inspection/vehicles?*", (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled failed queue", uncertain: true },
    }),
  );
  await page.getByRole("button", { name: "Refresh inspections", exact: true }).click();
  await expect(page.getByRole("alert").filter({ hasText: "last confirmed rows" })).toBeVisible();
  expect(await page.getByRole("table").innerText()).toBe(confirmedRow);
  await page.unroute("**/api/staff/inspection/vehicles?*");
  await page.getByRole("button", { name: "Claim inspection", exact: true }).click();
  const assignmentBodies: string[] = [];
  await page.route("**/api/staff/inspection/vehicles/*/assignment", async (route) => {
    assignmentBodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (assignmentBodies.length === 1) {
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          message: "Controlled lost assignment receipt",
          uncertain: true,
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
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
  await expect(page).toHaveURL(/\/dashboard\/inspection\?.*inspection=[a-f0-9-]+/);
  const id = new URL(page.url()).searchParams.get("inspection")!;
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  await page.unroute("**/api/staff/inspection/vehicles/*/assignment");
  const header = page.getByRole("heading", { level: 1 });
  await header.evaluate((element) => {
    (element as HTMLElement & { retained?: boolean }).retained = true;
  });
  await page.getByRole("searchbox", { name: "Find a checklist point", exact: true }).fill("EP-004");
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
  await page.getByRole("button", { name: "Refresh inspection", exact: true }).click();
  await expect(page.getByLabel("EP-004 estimate (MYR)", { exact: true })).toHaveValue("250.10");
  await page.unroute(`**/api/staff/inspection/drafts/${id}/workspace`);
  const bodies: string[] = [];
  let lose = true;
  await page.route(`**/api/staff/inspection/drafts/${id}/saves`, async (route) => {
    bodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (lose) {
      lose = false;
      await route.fulfill({
        status: 502,
        json: { status: 502, data: null, message: "Controlled lost save receipt", uncertain: true },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await page.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Reconcile draft save", exact: true }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("dialog", { name: "Inspection", exact: true })
      .getByRole("button", { name: "Close dialog", exact: true }),
  ).toBeDisabled();
  expect(page.url()).toContain(id);
  await page.getByRole("button", { name: "Reconcile draft save", exact: true }).click();
  expect(bodies).toHaveLength(2);
  expect(bodies[1]).toBe(bodies[0]);
  await expect(page.getByRole("button", { name: "Sign inspection", exact: true })).toBeEnabled();
  expect(
    counts()[
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
  await page.getByText("Request repair work", { exact: true }).click();
  await page.getByRole("checkbox", { name: /^EP-004 ·/ }).check();
  await page
    .getByLabel("EP-004 repair work", { exact: true })
    .fill("  Repair bumper scuff and inspect finish.  ");
  await page.getByLabel("Requested budget (MYR)", { exact: true }).fill("250.10");
  await page.getByRole("button", { name: "Refresh inspection", exact: true }).click();
  await expect(page.getByRole("button", { name: "Refresh inspection", exact: true })).toBeEnabled();
  await expect(page.getByLabel("EP-004 repair work", { exact: true })).toHaveValue(
    "  Repair bumper scuff and inspect finish.  ",
  );
  await expect(page.getByLabel("Requested budget (MYR)", { exact: true })).toHaveValue("250.10");
  const requestBodies: string[] = [];
  await page.route(`**/api/staff/inspection/drafts/${id}/repair-requests`, async (route) => {
    requestBodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (requestBodies.length === 1) {
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          message: "Controlled lost repair request receipt",
          uncertain: true,
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await page.getByRole("button", { name: "Submit repair request", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Reconcile repair request", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("EP-004 repair work", { exact: true })).toBeDisabled();
  await expect(page.getByLabel("Requested budget (MYR)", { exact: true })).toBeDisabled();
  await expect(
    page
      .getByRole("dialog", { name: "Inspection", exact: true })
      .getByRole("button", { name: "Close dialog", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Reconcile repair request", exact: true }).click();
  await expect(page.getByText("Pending Approval", { exact: true })).toBeVisible();
  expect(requestBodies).toHaveLength(2);
  expect(requestBodies[1]).toBe(requestBodies[0]);
  expect(JSON.parse(requestBodies[0])).toMatchObject({
    budget: 250.1,
    items: [{ pointCode: "EP-004", work: "Repair bumper scuff and inspect finish." }],
  });
  await page.unroute(`**/api/staff/inspection/drafts/${id}/repair-requests`);
  // Pace the accelerated actors within the real shared-loopback rate limit.
  await new Promise((resolve) => setTimeout(resolve, 61_000));

  const managerContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const manager = await managerContext.newPage();
  await login(manager, managerEmail, "/dashboard/intake");
  await manager.goto("/dashboard/intake?view=list");
  await expect(
    manager.getByRole("button", { name: "Refresh worklist", exact: true }),
  ).toBeEnabled();
  await screenshot(manager, "intake-parity-reference");
  await manager.goto("/dashboard/inspection/repairs");
  await expect(
    manager.getByRole("button", { name: "Refresh repair requests", exact: true }),
  ).toBeEnabled();
  await screenshot(manager, "repair-queue");
  await manager.getByRole("button", { name: /^Filters/ }).click();
  await manager.getByRole("radio", { name: "Pending Approval", exact: true }).check();
  await manager.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(
    manager.getByRole("button", { name: "Remove status filter: Pending Approval", exact: true }),
  ).toBeVisible();
  await expect(
    manager.getByRole("button", { name: "Refresh repair requests", exact: true }),
  ).toBeEnabled();
  await manager.getByRole("searchbox", { name: "Search repair requests", exact: true }).fill(id);
  await manager
    .getByRole("searchbox", { name: "Search repair requests", exact: true })
    .press("Enter");
  await expect.poll(() => new URL(manager.url()).searchParams.get("search")).toBe(id);
  await expect(manager.locator("tbody tr")).toHaveCount(1);
  await manager.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  await manager.locator("tbody tr").getByRole("cell").nth(2).click();
  await expect(manager.getByRole("dialog", { name: "Repair request", exact: true })).toBeVisible();
  await expect(
    manager
      .getByRole("dialog", { name: "Repair request", exact: true })
      .getByRole("button", { name: "Refresh inspection", exact: true }),
  ).toBeEnabled();
  await expect(
    manager.getByText("Bumper scuff requires paint repair.", { exact: true }),
  ).toBeVisible();
  const repairRequestId = new URL(manager.url()).searchParams.get("request")!;
  await screenshot(manager, "repair-review");
  await expect(manager.getByRole("button", { name: /bulk.*approv/i })).toHaveCount(0);
  await manager.getByRole("button", { name: "Approve repair budget", exact: true }).click();
  await expect(
    manager
      .getByRole("dialog", { name: "Repair request", exact: true })
      .getByText("Approved", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Refresh inspection", exact: true }).click();
  await page
    .getByLabel("Completion & reinspection notes", { exact: true })
    .fill("Bumper repair and finish checked. Reinspection complete.");
  await page.getByRole("button", { name: "Confirm repairs completed", exact: true }).click();
  const completedSummary = page.getByRole("region", { name: "Vehicle and confirmed assessment" });
  await expect(completedSummary.getByText("Available", { exact: true })).toBeVisible();
  await expect(completedSummary.getByText("1 · Signed", { exact: true })).toBeVisible();
  const reportHref = await page
    .getByRole("link", { name: "Open signed report · revision 1", exact: true })
    .getAttribute("href");
  await page.goto(reportHref!);
  await expect(page.getByRole("heading", { name: "Inspection report", exact: true })).toBeVisible();
  await screenshot(page, "signed-report");
  expect(await page.locator("tbody tr").count()).toBe(210);
  await page.evaluate(() => {
    const target = window as Window & { printCalls?: number };
    target.printCalls = 0;
    window.print = () => {
      target.printCalls = (target.printCalls ?? 0) + 1;
    };
  });
  let failEvidence = true;
  await page.route(`**/api/staff/inspection/drafts/${id}/photos/*`, async (route) => {
    if (failEvidence) {
      failEvidence = false;
      await route.fulfill({ status: 429, body: "Controlled evidence request limit" });
      return;
    }
    await route.continue();
  });
  await page
    .getByRole("img", { name: "EP-004 signed evidence 1", exact: true })
    .evaluate((img: HTMLImageElement) => {
      img.src = `${img.src}?printProbe=1`;
    });
  await page.getByRole("button", { name: "Print / save PDF", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Some evidence is unavailable" }),
  ).toBeVisible();
  expect(await page.evaluate(() => (window as Window & { printCalls?: number }).printCalls)).toBe(
    0,
  );
  await expect.poll(() => failEvidence).toBe(false);
  await expect
    .poll(() =>
      page
        .getByRole("img", { name: "EP-004 signed evidence 1", exact: true })
        .evaluate((img: HTMLImageElement) => img.complete),
    )
    .toBe(true);
  await page.getByRole("button", { name: "Print / save PDF", exact: true }).click();
  await expect
    .poll(() => page.evaluate(() => (window as Window & { printCalls?: number }).printCalls))
    .toBe(1);
  await page.unroute(`**/api/staff/inspection/drafts/${id}/photos/*`);
  await page.emulateMedia({ media: "print" });
  await expect(page.getByRole("button", { name: "Print / save PDF", exact: true })).toBeHidden();
  await page.pdf({
    path: path.join(evidence, "signed-report.pdf"),
    preferCSSPageSize: true,
    printBackground: true,
  });
  await page.emulateMedia({ media: "screen" });

  await manager
    .getByRole("dialog", { name: "Repair request", exact: true })
    .getByRole("button", { name: "Close dialog", exact: true })
    .click();
  await manager.goto(`/dashboard/inspection?inspection=${id}`);
  await manager.getByRole("button", { name: "Reopen inspection", exact: true }).click();
  await manager
    .getByLabel("Reopening reason", { exact: true })
    .fill("Inspect newly reported suspension noise.");
  await manager.getByRole("button", { name: "Confirm reopening", exact: true }).click();
  const reopenedSummary = manager.getByRole("region", { name: "Vehicle and confirmed assessment" });
  await expect(reopenedSummary.getByText("Pending Inspection", { exact: true })).toBeVisible();
  await expect(reopenedSummary.getByText("2 · Draft", { exact: true })).toBeVisible();
  await manager
    .getByRole("dialog", { name: "Inspection", exact: true })
    .getByRole("button", { name: "Close dialog", exact: true })
    .click();
  await manager.goto(
    `/dashboard/inspection/repairs?inspection=${id}&request=${repairRequestId}&revision=1`,
  );
  await expect(
    manager.getByRole("heading", { name: "Retained repair request", exact: true }),
  ).toBeVisible();
  await expect(
    manager.getByText("Bumper scuff requires paint repair.", { exact: true }),
  ).toBeVisible();
  await expect(
    manager.getByRole("button", { name: "Approve repair budget", exact: true }),
  ).toHaveCount(0);
  await manager
    .getByRole("dialog", { name: "Repair request", exact: true })
    .getByRole("button", { name: "Close dialog", exact: true })
    .click();
  await manager.goto(`/dashboard/inspection?inspection=${id}`);
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
      checklistPoints: report.checklist.length,
    }),
  );
  await new Promise((resolve) => setTimeout(resolve, 61_000));
  await page.getByRole("link", { name: "Back to inspection", exact: true }).click();
  await expect(page.getByRole("button", { name: "Save draft", exact: true })).toBeEnabled();
  await page.getByRole("searchbox", { name: "Find a checklist point", exact: true }).fill("EP-004");
  await page.getByLabel("EP-004 severity", { exact: true }).selectOption("CriticalDisqualifier");
  await page
    .getByLabel("EP-004 finding notes", { exact: true })
    .fill("Structural accident evidence requires rejection.");
  await page.getByRole("checkbox", { name: /^I have examined all/ }).check();
  await page.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(page.getByRole("button", { name: "Sign inspection", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Sign inspection", exact: true }).click();
  await page.getByRole("button", { name: "Confirm sign-off", exact: true }).click();
  await expect(completedSummary.getByText("Rejected", { exact: true })).toBeVisible();
  await expect(completedSummary.getByText("2 · Signed", { exact: true })).toBeVisible();
  const rejectedHref = await page
    .getByRole("link", { name: "Open signed report · revision 2", exact: true })
    .getAttribute("href");
  await page.goto(rejectedHref!);
  await expect(page.getByText("Rejected", { exact: true }).first()).toBeVisible();
  await manager.getByRole("button", { name: "Refresh inspection", exact: true }).click();
  await manager.getByRole("button", { name: "Reopen inspection", exact: true }).click();
  await manager
    .getByLabel("Reopening reason", { exact: true })
    .fill("Review accident evidence in a new retained revision.");
  await manager.getByRole("button", { name: "Confirm reopening", exact: true }).click();
  await expect(reopenedSummary.getByText("Pending Inspection", { exact: true })).toBeVisible();
  await expect(reopenedSummary.getByText("3 · Draft", { exact: true })).toBeVisible();
  await managerContext.close();
  const salesContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const sales = await salesContext.newPage();
  await login(sales, "aina.sofia@example.invalid", "/dashboard/intake");
  await sales.goto(`/dashboard/inspection/${id}`);
  await expect(
    sales.getByRole("heading", { name: "Inspection access unavailable", exact: true }),
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
  await page.getByRole("link", { name: "Back to inspection", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Inspection access unavailable", exact: true }),
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
  await login(group, "farah.aziz@example.invalid", "/dashboard/intake");
  await group.goto("/dashboard/inspection");
  await expect(group.getByRole("button", { name: "Claim inspection", exact: true })).toHaveCount(0);
  await group.getByLabel("Rows per page", { exact: true }).selectOption("10");
  await expect.poll(() => new URL(group.url()).searchParams.get("pageSize")).toBe("10");
  await group.getByRole("button", { name: "Next records", exact: true }).click();
  await expect.poll(() => new URL(group.url()).searchParams.get("page")).toBe("2");
  const fullDownload = group.waitForEvent("download");
  await group.getByRole("button", { name: "Export full CSV", exact: true }).click();
  const fullFile = await (await fullDownload).path();
  expect(readFileSync(fullFile!, "utf8").trim().split("\r\n").length).toBeGreaterThan(26);
  await group.goto(`/dashboard/inspection/${id}`);
  await expect(group.getByRole("button", { name: "Save draft", exact: true })).toHaveCount(0);
  await expect(group.getByRole("button", { name: "Reassign technician", exact: true })).toHaveCount(
    0,
  );
  await groupContext.close();
  const adminContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const admin = await adminContext.newPage();
  await login(admin, "muhammad.hafiz@example.invalid", "/dashboard");
  await admin.goto("/dashboard/inspection/repairs");
  await expect(
    admin.getByRole("heading", { name: "Inspection access unavailable", exact: true }),
  ).toBeVisible();
  await adminContext.close();
});
