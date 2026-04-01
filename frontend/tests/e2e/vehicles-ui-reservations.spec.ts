import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { ReservationItem } from "@/lib/vehicles/reservations";

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

async function read(context: BrowserContext, query: string) {
  const response = await context.request.get(`/api/staff/reservations?${query}`, {
    headers: await headers(context),
  });
  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toContain("no-store");
  return (await response.json()).data as { items: ReservationItem[]; total: number };
}

async function verifyDetails(page: Page, id: string) {
  const target = `/api/staff/reservations/${id}`;
  const before = counts()[target] ?? 0;
  const mountedTable = await page
    .getByRole("table", { name: "Reservations", exact: true })
    .elementHandle();
  await page.getByRole("button", { name: id, exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Reservation details", exact: true });
  await expect(dialog.getByRole("heading", { name: "Retained price", exact: true })).toBeVisible();
  expect(counts()[target] - before).toBe(1);
  await expect(page).toHaveURL(new RegExp(`reservation=${id}`));
  const html = await dialog.textContent();
  const confirmedFacts = await dialog.locator("dl").allTextContents();
  expect(html).toContain("MYR 500.00");
  expect(html).not.toMatch(/nric|@example\.invalid/);
  const refresh = dialog.getByRole("button", { name: "Refresh reservation details", exact: true });
  writeFileSync(path.join(evidence, "fault.txt"), "unavailable");
  try {
    await refresh.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
    ).toBeVisible();
    expect(await dialog.locator("dl").allTextContents()).toEqual(confirmedFacts);
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(`**${target}`, async (route) => {
    await gate;
    await route.continue();
  });
  try {
    await refresh.click();
    await expect(refresh).toBeDisabled();
    await expect(
      dialog.getByRole("heading", { name: "Retained price", exact: true }),
    ).toBeVisible();
  } finally {
    release();
  }
  await expect(refresh).toBeEnabled();
  await page.unroute(`**${target}`);
  expect(await mountedTable!.evaluate((element) => element.isConnected)).toBe(true);
  const beforeReload = counts()[target];
  const response = await page.reload();
  const initialHtml = await response!.text();
  expect(initialHtml).toContain("Retained price");
  expect(initialHtml).not.toContain("nric");
  await expect(refresh).toBeEnabled();
  expect(counts()[target] - beforeReload).toBe(1);
  await dialog.getByRole("button", { name: "Close details", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page).not.toHaveURL(/reservation=/);
}

test("manager reservations retain scoped active/history snapshots, server order and actor isolation", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(120_000);
  const mobile = info.project.name === "mobile";
  await login(page, mobile ? "lim.mei.ling@example.invalid" : "farah.aziz@example.invalid");
  const before = counts()["/api/staff/reservations"] ?? 0;
  const response = await page.goto("/dashboard/reservations?pageSize=10");
  const html = await response!.text();
  expect(html).toContain("Active hold");
  expect(html).toContain("Reserved total");
  expect(html).not.toContain("nric");
  const refresh = page.getByRole("button", { name: "Refresh reservations", exact: true });
  await expect(refresh).toBeEnabled();
  expect(counts()["/api/staff/reservations"] - before).toBe(1);
  const table = page.getByRole("table", { name: "Reservations", exact: true });
  const rows = () => table.locator("tbody tr");
  const active = await read(context, "isActive=true&pageSize=10");
  expect(active.total).toBe(mobile ? 1 : 3);
  await expect(rows()).toHaveCount(active.total);
  expect(active.items.every((row) => row.status === "Active")).toBe(true);
  if (mobile) {
    expect(new Set(active.items.map((row) => row.branchId)).size).toBe(1);
    const denied = await context.request.get(`/api/staff/reservations?branchId=${branch}`, {
      headers: await headers(context),
    });
    expect(denied.status()).toBe(403);
  } else {
    await page.getByRole("button", { name: "Filters", exact: true }).click();
    await page.getByRole("combobox", { name: "Branch", exact: true }).selectOption(branch);
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`branchId=${branch}`));
    await expect(rows()).toHaveCount(1);
  }
  const held = active.items.find((row) => mobile || row.branchId === branch)!;
  await verifyDetails(page, held.id);
  const activeRows = await rows().allTextContents();
  let releaseRead = () => {};
  const readGate = new Promise<void>((resolve) => {
    releaseRead = resolve;
  });
  await page.route("**/api/staff/reservations?*", async (route) => {
    await readGate;
    await route.continue();
  });
  try {
    await page.getByRole("button", { name: "History", exact: true }).click();
    await expect(
      page.getByRole("group", { name: "Reservation views", exact: true }),
    ).toHaveAttribute("aria-busy", "true");
    await expect(page.getByRole("button", { name: "History", exact: true })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await expect(refresh).toBeDisabled();
    expect(await rows().allTextContents()).toEqual(activeRows);
  } finally {
    releaseRead();
  }
  await expect(page).toHaveURL(/isActive=false/);
  await page.unroute("**/api/staff/reservations?*");
  await expect(rows()).toHaveCount(10);
  const scope = mobile ? "" : `&branchId=${branch}`;
  const history = await read(
    context,
    `isActive=false&pageSize=50${scope}&sort=reference&direction=desc`,
  );
  expect(history.total).toBe(51);
  expect(history.items.every((row) => row.status !== "Active")).toBe(true);
  expect(history.items.some((row) => active.items.some((held) => held.id === row.id))).toBe(false);
  const sort = table.locator('th[data-column-id="reference"]');
  const sortButton = page.getByRole("button", { name: "Sort by Reference", exact: true });
  await sortButton.click();
  await expect(sort).toHaveAttribute("aria-sort", "ascending");
  await sortButton.click();
  await expect(sort).toHaveAttribute("aria-sort", "descending");
  await expect(page).toHaveURL(/sort=reference&direction=desc/);
  // Read the full reference text through the shared single-line cells.
  const rowReferences = async () =>
    rows().evaluateAll((elements) =>
      elements.map((row) => row.querySelectorAll("td")[1].textContent),
    );
  expect(await rowReferences()).toEqual(history.items.slice(0, 10).map((row) => row.id));
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expect(page).toHaveURL(/page=2/);
  expect(await rowReferences()).toEqual(history.items.slice(10, 20).map((row) => row.id));
  await page.reload();
  await expect(refresh).toBeEnabled();
  await expect(sort).toHaveAttribute("aria-sort", "descending");
  expect(await rowReferences()).toEqual(history.items.slice(10, 20).map((row) => row.id));
  const confirmed = await rows().allTextContents();
  const confirmedUrl = page.url();
  const sameTable = await table.elementHandle();
  writeFileSync(path.join(evidence, "fault.txt"), "unavailable");
  try {
    await page.getByRole("button", { name: "Active holds", exact: true }).click();
    await expect(page.getByRole("alert").filter({ hasText: "last confirmed rows" })).toBeVisible();
    expect(page.url()).toBe(confirmedUrl);
    expect(await rows().allTextContents()).toEqual(confirmed);
    await expect(page.getByRole("button", { name: "History", exact: true })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await expect(sort).toHaveAttribute("aria-sort", "descending");
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
  await refresh.click();
  await expect(refresh).toBeEnabled();
  expect(await rows().allTextContents()).toEqual(confirmed);
  expect(await sameTable!.evaluate((element) => element.isConnected)).toBe(true);
  await page.getByLabel("Select all loaded rows", { exact: true }).check();
  const selectedDownload = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export selected CSV", exact: true }).click();
  const selected = readFileSync((await (await selectedDownload).path())!, "utf8");
  expect(selected.trim().split("\r\n")).toHaveLength(11);
  await page.getByRole("button", { name: "Clear selection", exact: true }).click();
  const fullDownload = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export full CSV", exact: true }).click();
  const full = readFileSync((await (await fullDownload).path())!, "utf8")
    .trim()
    .split("\r\n")
    .slice(1);
  const last = await read(
    context,
    `isActive=false&page=2&pageSize=50${scope}&sort=reference&direction=desc`,
  );
  expect(full.map((line) => line.split(",")[0])).toEqual(
    [...history.items, ...last.items].map((row) => `"${row.id}"`),
  );
  await sortButton.click();
  await expect(sort).toHaveAttribute("aria-sort", "none");
  await expect(page).not.toHaveURL(/sort=/);
  await page.goto(`/dashboard/reservations?isActive=false&page=99&pageSize=10${scope}`);
  await page.getByRole("button", { name: "Go to first page", exact: true }).click();
  await expect(page).toHaveURL(/page=1/);
  await expect(rows()).toHaveCount(10);
  const releasedId = await rows().first().getByRole("button").first().textContent();
  await verifyDetails(page, releasedId!.trim());
  await page.getByRole("button", { name: releasedId!.trim(), exact: true }).click();
  const details = page.getByRole("dialog", { name: "Reservation details", exact: true });
  await expect(
    details.getByRole("heading", { name: "Simulated refund", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: path.join(evidence, `reservation-details-${info.project.name}.png`),
    fullPage: true,
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  const sales = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const salesPage = await sales.newPage();
  await login(salesPage, "aina.sofia@example.invalid");
  const beforeDenied = counts()["/api/staff/reservations"] ?? 0;
  const denied = await sales.request.get("/api/staff/reservations?isActive=true", {
    headers: await headers(sales),
  });
  expect(denied.status()).toBe(403);
  expect(counts()["/api/staff/reservations"] ?? 0).toBe(beforeDenied);
  await context.addCookies(
    (await sales.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  await details.getByRole("button", { name: "Refresh reservation details", exact: true }).click();
  await expect(table).toHaveCount(0);
  await expect(details).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Reservation access unavailable", exact: true }),
  ).toBeVisible();
  await sales.close();
  writeFileSync(
    path.join(evidence, `reservations-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      active: active.total,
      history: history.total,
      export: full.length,
      role: mobile ? "BranchManager" : "GeneralManager",
      htmlBytes: Buffer.byteLength(html),
    }),
  );
});
