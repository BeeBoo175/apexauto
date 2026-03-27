import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";

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

test("manager listing table preserves scoped server rows, drafts and complete filtered exports", async ({
  page,
  context,
}, info) => {
  test.setTimeout(120_000);
  await login(page, "farah.aziz@example.invalid");
  const initialReads = counts()["/api/staff/listings"] ?? 0;
  const initial = await page.goto(`/dashboard/management?pageSize=10&branchId=${branch}`);
  const html = await initial!.text();
  expect(html).toContain("ApexAuto Glenmarie");
  expect(html).toContain("Draft price");
  await expect(page.getByRole("button", { name: "Refresh listings", exact: true })).toBeEnabled();
  expect(counts()["/api/staff/listings"] - initialReads).toBe(1);
  const table = page.getByRole("table", { name: "Listings", exact: true });
  const rows = () => table.locator("tbody tr");
  await expect(rows()).toHaveCount(10);
  const first = await rows().allTextContents();
  const response = await context.request.get(`/api/staff/listings?pageSize=10&branchId=${branch}`, {
    headers: await headers(context),
  });
  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toContain("no-store");
  const data = (await response.json()).data;
  expect(data.total).toBeGreaterThan(20);
  for (const row of data.items) {
    expect(row.branchId).toBe(branch);
    expect(first.join(" ")).toContain(row.registrationPlate);
  }
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expect(page).toHaveURL(/page=2/);
  expect(await rows().allTextContents()).not.toEqual(first);
  await page.getByRole("button", { name: "Previous records", exact: true }).click();
  await expect(page).toHaveURL(/page=1/);
  expect(await rows().allTextContents()).toEqual(first);

  await page.getByLabel("Search listings", { exact: true }).fill("Unsubmitted search");
  const confirmed = page.url();
  writeFileSync(path.join(evidence, "fault.txt"), "unavailable");
  try {
    await page.getByRole("button", { name: "Refresh listings", exact: true }).click();
    await expect(page.getByRole("alert").filter({ hasText: "last confirmed rows" })).toBeVisible();
    expect(page.url()).toBe(confirmed);
    expect(await rows().allTextContents()).toEqual(first);
    await expect(page.getByLabel("Search listings", { exact: true })).toHaveValue(
      "Unsubmitted search",
    );
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
  await page.getByLabel("Search listings", { exact: true }).fill("Proton");
  let release = () => {};
  let entered = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const pending = new Promise<void>((resolve) => {
    entered = resolve;
  });
  await page.route("**/api/staff/listings?**", async (route) => {
    entered();
    await gate;
    await route.continue();
  });
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await pending;
  await page.getByLabel("Search listings", { exact: true }).fill("Edited during read");
  release();
  await expect(page).toHaveURL(/search=Proton/);
  await expect(page.getByLabel("Search listings", { exact: true })).toHaveValue(
    "Edited during read",
  );
  await page.unroute("**/api/staff/listings?**");
  await page.getByRole("button", { name: "Clear search", exact: true }).click();
  await expect(page).not.toHaveURL(/search=/);
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
  const file = await download;
  const exported = readFileSync((await file.path())!, "utf8");
  const registrations: string[] = [];
  for (let number = 1; registrations.length < data.total; number += 1) {
    const response = await context.request.get(
      `/api/staff/listings?pageSize=50&page=${number}&branchId=${branch}`,
      { headers: await headers(context) },
    );
    expect(response.status()).toBe(200);
    const result = (await response.json()).data;
    expect(result.total).toBe(data.total);
    registrations.push(
      ...result.items.map((row: { registrationPlate: string }) => row.registrationPlate),
    );
  }
  const exportedRows = exported.trim().split("\r\n").slice(1);
  expect(exportedRows.map((row) => row.split(",")[0])).toEqual(
    registrations.map((plate) => `"${plate}"`),
  );
  expect(new Set(registrations).size).toBe(data.total);
  await page.screenshot({ path: path.join(evidence, "listings.png"), fullPage: false });

  await page.getByLabel("Search listings", { exact: true }).fill("Unsubmitted reset");
  await page.getByRole("button", { name: /^Filters/ }).click();
  await page.getByRole("button", { name: "Clear all filters", exact: true }).click();
  await expect(page.getByLabel("Search listings", { exact: true })).toHaveValue("");
  await page.getByRole("button", { name: /^Filters/ }).click();

  let releaseOld = () => {};
  let oldEntered = () => {};
  let oldHandled = () => {};
  const oldGate = new Promise<void>((resolve) => {
    releaseOld = resolve;
  });
  const oldPending = new Promise<void>((resolve) => {
    oldEntered = resolve;
  });
  const oldComplete = new Promise<void>((resolve) => {
    oldHandled = resolve;
  });
  await page.route("**/api/staff/listings?**", async (route) => {
    const response = await route.fetch();
    oldEntered();
    await oldGate;
    await route.fulfill({ response }).catch(() => {});
    oldHandled();
  });
  await page.getByLabel("Search listings", { exact: true }).fill("Superseded search");
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await oldPending;
  const listingsLink = page.getByRole("link", { name: "Listings", exact: true });
  if (!(await listingsLink.isVisible())) {
    await page.getByRole("button", { name: "Workspaces", exact: true }).click();
  }
  await listingsLink.click();
  await expect(page.getByLabel("Search listings", { exact: true })).toHaveValue("");
  releaseOld();
  await oldComplete;
  await page.evaluate(
    () =>
      new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
      ),
  );
  await expect(page).not.toHaveURL(/search=/);
  await expect(page.getByRole("table", { name: "Listings", exact: true })).toBeVisible();
  await page.unroute("**/api/staff/listings?**");

  await page.getByRole("button", { name: /^Filters/ }).click();
  await page
    .getByRole("combobox", { name: "Branch", exact: true })
    .selectOption("68f371ad-223a-43d6-b45b-b878eece79ae");
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(page).toHaveURL(/branchId=68f371ad/);
  await expect(table.getByText("ApexAuto Petaling Jaya", { exact: true }).first()).toBeVisible();

  await signOut(page);
  await login(page, "siti.aminah@example.invalid");
  await page.goto("/dashboard/management?pageSize=10");
  await expect(rows()).toHaveCount(10);
  expect(await table.innerText()).not.toContain("ApexAuto Petaling Jaya");
  const denied = await context.request.get(
    "/api/staff/listings?branchId=68f371ad-223a-43d6-b45b-b878eece79ae",
    { headers: await headers(context) },
  );
  expect((await denied.json()).data?.items ?? []).toEqual([]);
  await signOut(page);
  await login(page, "aina.sofia@example.invalid");
  const beforeDenied = counts()["/api/staff/listings"];
  await page.goto("/dashboard/management");
  await expect(
    page.getByRole("heading", { name: "Listing access unavailable", exact: true }),
  ).toBeVisible();
  expect(counts()["/api/staff/listings"]).toBe(beforeDenied);
  expect(
    (
      await context.request.get("/api/staff/listings", { headers: await headers(context) })
    ).status(),
  ).toBe(403);
  writeFileSync(
    path.join(evidence, `listings-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      scopedTotal: data.total,
      failedRefreshRetained: true,
      draftRetained: true,
      exportVerified: true,
    }),
  );
});
