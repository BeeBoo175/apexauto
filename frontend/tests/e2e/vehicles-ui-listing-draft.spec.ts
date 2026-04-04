import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { formatCurrency } from "../../src/lib/currency-format";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function login(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
}

async function headers(context: BrowserContext) {
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}`, Origin: process.env.APP_BASE_URL! };
}

test("manager saves inline drafts with exact replay, retained publication and stale-edit protection", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(120_000);
  await login(page, "farah.aziz@example.invalid");
  const cataloguePath = `/api/vehicles?branchId=${branch}&page=1`;
  const catalogue = (await (await context.request.get(cataloguePath)).json()).data;
  const item = catalogue.items[info.project.name === "mobile" ? 1 : 0];
  const endpoint = `/api/staff/listings/${item.id}`;
  const postPath = `${endpoint}/drafts`;
  const saved = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  const before = counts()[endpoint] ?? 0;
  const document = await page.goto(`/dashboard/management?branchId=${branch}&listing=${item.id}`);
  expect(await document!.text()).not.toContain(saved.vehicle.registrationPlate);
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  await dialog.getByRole("button", { name: "Edit price", exact: true }).click();
  expect(counts()[endpoint] - before).toBe(1);
  await dialog.getByRole("button", { name: "Back to listings", exact: true }).click();
  const search = page.getByRole("searchbox", { name: "Search listings", exact: true });
  await search.fill(saved.vehicle.registrationPlate);
  await search.press("Enter");
  const table = page.getByRole("table", { name: "Listings", exact: true });
  const row = table.locator("tbody tr").filter({ hasText: saved.vehicle.registrationPlate });
  await expect(row).toHaveCount(1);
  await expect(page.getByRole("button", { name: "Refresh listings", exact: true })).toBeEnabled();
  let releaseTable = () => {};
  let tableEntered = () => {};
  let tableCompleted = () => {};
  const tableGate = new Promise<void>((resolve) => {
    releaseTable = resolve;
  });
  const tablePending = new Promise<void>((resolve) => {
    tableEntered = resolve;
  });
  const tableHandled = new Promise<void>((resolve) => {
    tableCompleted = resolve;
  });
  let firstTableRead = true;
  page.on("close", () => releaseTable());
  await page.route("**/api/staff/listings?*", async (route) => {
    const response = await route.fetch();
    if (firstTableRead) {
      firstTableRead = false;
      tableEntered();
      await tableGate;
      await route.fulfill({ response }).catch(() => {});
      tableCompleted();
      return;
    }
    await route.fulfill({ response });
  });
  await page.getByRole("button", { name: "Refresh listings", exact: true }).click();
  await tablePending;
  await row.getByRole("button", { name: saved.vehicle.registrationPlate, exact: true }).click();
  await dialog.getByRole("button", { name: "Edit price", exact: true }).click();
  const writesBefore = counts()[postPath] ?? 0;
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("123.456");
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Enter exact MYR" })).toBeVisible();
  await expect(dialog.getByLabel("Vehicle price (MYR)", { exact: true })).toHaveAttribute(
    "aria-invalid",
    "true",
  );
  expect(counts()[postPath] ?? 0).toBe(writesBefore);
  const price = (saved.draft.vehiclePrice + 111.01).toFixed(2);
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill(price);
  await dialog.getByRole("button", { name: "Edit specifications", exact: true }).click();
  await dialog.getByLabel("Transmission", { exact: true }).selectOption("Manual");
  await dialog.getByLabel("Fuel", { exact: true }).selectOption("Hybrid");
  await dialog.getByLabel("Road tax (MYR)", { exact: true }).fill("99.01");
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  const description = `${saved.draft.description}\nManager reviewed ${info.project.name}.`;
  await dialog.getByLabel("Public description", { exact: true }).fill(description);
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(
    dialog.getByRole("heading", { name: "Publication record", exact: true }),
  ).toBeVisible();
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  await expect(dialog.getByLabel("Public description", { exact: true })).toHaveValue(description);
  await expect(
    dialog.getByRole("button", { name: "Back to listings", exact: true }),
  ).toBeDisabled();
  const editUrl = page.url();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await page
    .locator('a[href="/dashboard/intake"]')
    .first()
    .evaluate((link: HTMLAnchorElement) => link.click());
  expect(page.url()).toBe(editUrl);
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Unavailable", uncertain: true },
    }),
  );
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toBeVisible();
  await expect(dialog.getByLabel("Public description", { exact: true })).toHaveValue(description);
  expect(page.url()).toBe(editUrl);
  await page.unroute(`**${endpoint}`);

  const attempts: string[] = [];
  await page.route(`**${postPath}`, async (route) => {
    attempts.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (attempts.length === 1) {
      const body = await response.json();
      await route.fulfill({
        response,
        json: { ...body, data: { ...body.data, vehicleId: branch } },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    dialog.getByRole("button", { name: "Reconcile draft save", exact: true }),
  ).toBeEnabled();
  await expect(dialog.getByLabel("Public description", { exact: true })).toBeDisabled();
  await expect(dialog.getByRole("button", { name: "Discard changes", exact: true })).toBeDisabled();
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  await expect(dialog.getByLabel("Public description", { exact: true })).toHaveValue(description);
  await dialog.getByRole("button", { name: "Reconcile draft save", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Back to listings", exact: true })).toBeEnabled();
  expect(attempts).toHaveLength(2);
  expect(attempts[1]).toBe(attempts[0]);
  await page.unroute(`**${postPath}`);
  const captured = JSON.parse(attempts[0]);
  const current = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  expect(current.draft).toMatchObject({
    id: captured.operationId,
    version: saved.draft.version + 1,
    vehiclePrice: Number(price),
    roadTax: 99.01,
    transmission: "Manual",
    fuel: "Hybrid",
    description,
    photoIds: saved.draft.photoIds,
  });
  expect(current.approvedRevision).toEqual(saved.approvedRevision);
  expect(current.publication).toEqual(saved.publication);
  await expect(row).toContainText(formatCurrency(Number(price)));
  releaseTable();
  await tableHandled;
  await expect(row).toContainText(formatCurrency(Number(price)));
  await page.unroute("**/api/staff/listings?*");
  const publicAfter = (await (await context.request.get(cataloguePath)).json()).data;
  expect(publicAfter.items.find((row: { id: string }) => row.id === item.id)).toEqual(item);
  await expect(page.getByRole("table", { name: "Listings", exact: true })).toBeVisible();
  await dialog.getByRole("button", { name: "Edit price", exact: true }).click();
  await expect(dialog.getByLabel("Vehicle price (MYR)", { exact: true })).toHaveValue(price);
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  await dialog.getByLabel("Public description", { exact: true }).fill("Local unsaved description");
  const outside = await context.request.post(postPath, {
    headers: await headers(context),
    data: {
      ...captured,
      operationId: crypto.randomUUID(),
      expectedVersion: current.draft.version,
      description: "Saved by another manager",
    },
  });
  expect(outside.status()).toBe(200);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByText("Newer saved draft available", { exact: true })).toBeVisible();
  await expect(dialog.getByLabel("Public description", { exact: true })).toHaveValue(
    "Local unsaved description",
  );
  await expect(dialog.getByRole("button", { name: "Save draft", exact: true })).toBeDisabled();
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  await expect(dialog.getByLabel("Public description", { exact: true })).toHaveValue(
    "Saved by another manager",
  );
  await dialog.getByRole("button", { name: "Edit price", exact: true }).click();
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("45123.01");
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-draft.png") });
  const securityBefore = counts()[postPath];
  const crossOrigin = await context.request.post(postPath, {
    headers: { ...(await headers(context)), Origin: "https://outside.invalid" },
    data: captured,
  });
  expect(crossOrigin.status()).toBe(403);
  expect(counts()[postPath]).toBe(securityBefore);
  const managerContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const managerPage = await managerContext.newPage();
  await login(managerPage, "lim.mei.ling@example.invalid");
  const outsideBranch = await managerContext.request.post(postPath, {
    headers: await headers(managerContext),
    data: { ...captured, operationId: crypto.randomUUID() },
  });
  expect(outsideBranch.status()).toBe(404);
  await managerContext.close();
  const salesContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const salesPage = await salesContext.newPage();
  await login(salesPage, "aina.sofia@example.invalid");
  const beforeDenied = counts()[postPath];
  const forbidden = await salesContext.request.post(postPath, {
    headers: await headers(salesContext),
    data: captured,
  });
  expect(forbidden.status()).toBe(403);
  expect(counts()[postPath]).toBe(beforeDenied);
  await context.addCookies(
    (await salesContext.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Listing access unavailable", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("Vehicle price (MYR)", { exact: true })).toHaveCount(0);
  await salesContext.close();
  writeFileSync(
    path.join(evidence, `listing-draft-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      exactAttempts: attempts,
      savedRevisionId: current.draft.id,
      savedVersion: current.draft.version,
      photoOrder: current.draft.photoIds,
      approvedRevisionRetained: true,
      publicProjectionRetained: true,
      staleDraftRetained: true,
      actorDraftDiscarded: true,
      dirtyNavigationLocked: true,
    }),
  );
});
