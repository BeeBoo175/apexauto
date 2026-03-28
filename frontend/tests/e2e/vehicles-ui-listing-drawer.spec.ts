import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

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
  return { Cookie: `apexauto_token=${cookie.value}` };
}

async function signOut(page: Page) {
  const button = page.getByRole("button", { name: "Sign out", exact: true });
  if (!(await button.isVisible())) {
    await page.getByRole("button", { name: "Workspaces", exact: true }).click();
  }
  await button.click();
  await expect(page).toHaveURL(/\/login$/);
}

test("manager listing drawer retains confirmed details and cancels superseded selections", async ({
  page,
  context,
}, info) => {
  test.setTimeout(120_000);
  await login(page, "farah.aziz@example.invalid");
  const list = await context.request.get(`/api/staff/listings?branchId=${branch}&pageSize=50`, {
    headers: await headers(context),
  });
  const data = (await list.json()).data;
  let item = data.items.find((row: { revisionId: string | null }) => row.revisionId);
  for (let number = 2; !item && (number - 1) * data.pageSize < data.total; number += 1) {
    const response = await context.request.get(
      `/api/staff/listings?branchId=${branch}&pageSize=50&page=${number}`,
      { headers: await headers(context) },
    );
    item = (await response.json()).data.items.find(
      (row: { revisionId: string | null }) => row.revisionId,
    );
  }
  expect(item).toBeDefined();
  const otherBranch = await context.request.get(
    "/api/staff/listings?branchId=68f371ad-223a-43d6-b45b-b878eece79ae&pageSize=10",
    { headers: await headers(context) },
  );
  const outside = (await otherBranch.json()).data.items[0];
  const endpoint = `/api/staff/listings/${item.vehicleId}`;
  const before = counts()[endpoint] ?? 0;
  const initial = await page.goto(
    `/dashboard/management?branchId=${branch}&pageSize=10&listing=${item.vehicleId}&section=publication`,
  );
  expect(await initial!.text()).toContain(item.registrationPlate);
  const dialog = page.getByRole("dialog", { name: item.registrationPlate, exact: true });
  await expect(
    dialog.getByRole("heading", { name: "Publication record", exact: true }),
  ).toBeVisible();
  expect(counts()[endpoint] - before).toBe(1);
  const table = page.getByRole("table", { name: "Listings", exact: true });
  const rows = await table.locator("tbody").innerText();
  const details = await dialog.locator("dl").innerText();
  const confirmedUrl = page.url();
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Unavailable", uncertain: false },
    }),
  );
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  expect(await dialog.locator("dl").innerText()).toBe(details);
  expect(page.url()).toBe(confirmedUrl);
  await page.unroute(`**${endpoint}`);
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toHaveCount(0);
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await expect(dialog.getByRole("heading", { name: "Description", exact: true })).toBeVisible();
  const refreshed = page.waitForResponse((response) => response.url().endsWith(endpoint));
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  expect((await refreshed).status()).toBe(200);
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  await expect(dialog.getByRole("button", { name: "Listing page", exact: true })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toHaveCount(0);
  await page.screenshot({ path: path.join(evidence, "listing-drawer.png") });
  await dialog.getByRole("button", { name: "Back to listings", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(await table.locator("tbody").innerText()).toBe(rows);
  await expect(page).not.toHaveURL(/listing=/);

  const tableData = await context.request.get(
    `/api/staff/listings?branchId=${branch}&pageSize=10`,
    { headers: await headers(context) },
  );
  const opening = (await tableData.json()).data.items[0];
  let release = () => {};
  let entered = () => {};
  let completed = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const pending = new Promise<void>((resolve) => {
    entered = resolve;
  });
  const handled = new Promise<void>((resolve) => {
    completed = resolve;
  });
  await page.route(`**/api/staff/listings/${opening.vehicleId}`, async (route) => {
    const response = await route.fetch();
    entered();
    await gate;
    await route.fulfill({ response }).catch(() => {});
    completed();
  });
  await table.getByRole("button", { name: opening.registrationPlate, exact: true }).click();
  await pending;
  // Inject a server entry while the old response is held, without closing/cancelling it first.
  await page
    .locator('a[href="/dashboard/management"]')
    .evaluate((element: HTMLAnchorElement) => element.click());
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page).not.toHaveURL(/listing=/);
  release();
  await handled;
  await page.evaluate(
    () =>
      new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
      ),
  );
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page).not.toHaveURL(/listing=/);
  await page.unroute(`**/api/staff/listings/${opening.vehicleId}`);

  await page.goto(`/dashboard/management?listing=${opening.vehicleId}`);
  await expect(
    page.getByRole("dialog", { name: opening.registrationPlate, exact: true }),
  ).toBeVisible();
  await page.getByRole("dialog").getByRole("button", { name: "Close dialog", exact: true }).click();
  await signOut(page);
  await login(page, "siti.aminah@example.invalid");
  const denied = await context.request.get(`/api/staff/listings/${outside.vehicleId}`, {
    headers: await headers(context),
  });
  const own = await context.request.get(endpoint, { headers: await headers(context) });
  expect(own.status()).toBe(200);
  expect(denied.status()).toBe(404);
  await page.goto(`/dashboard/management?listing=${outside.vehicleId}`);
  await expect(
    page
      .getByRole("dialog")
      .getByText("The listing is unavailable. Your table remains unchanged.", { exact: true }),
  ).toBeVisible();
  await page.getByRole("dialog").getByRole("button", { name: "Close dialog", exact: true }).click();
  await signOut(page);
  await login(page, "aina.sofia@example.invalid");
  const beforeDenied = counts()[endpoint];
  const forbidden = await context.request.get(endpoint, { headers: await headers(context) });
  expect(forbidden.status()).toBe(403);
  expect(counts()[endpoint]).toBe(beforeDenied);
  writeFileSync(
    path.join(evidence, `listing-drawer-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      retainedRefresh: true,
      supersededReadCancelled: true,
      salesDenied: true,
    }),
  );
});
