import { expect, test, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { ReservationItem } from "@/lib/vehicles/reservations";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function login(context: BrowserContext, email: string) {
  const response = await context.request.post("/api/auth/login", {
    headers: { Origin: process.env.APP_BASE_URL! },
    data: { loginKey: crypto.randomUUID(), email, password: "Password123!" },
  });
  expect(response.status()).toBe(200);
}

async function headers(context: BrowserContext) {
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}`, Origin: process.env.APP_BASE_URL! };
}

test("staff cancellation binds its full refund receipt and deliberately replays an uncertain request", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(120_000);
  const mobile = info.project.name === "mobile";
  await login(context, mobile ? "lim.mei.ling@example.invalid" : "farah.aziz@example.invalid");
  const managerCookies = await context.cookies();
  const listing = await context.request.get("/api/staff/reservations?isActive=true", {
    headers: await headers(context),
  });
  const active = (await listing.json()).data.items as ReservationItem[];
  const held = active.find(
    (item) => mobile || item.branchId === "c77708bf-1a31-42f5-9a69-0c8766697a52",
  )!;
  const endpoint = `/api/staff/reservations/${held.id}`;
  const postPath = `${endpoint}/cancellations`;
  const upstream = `/api/reservations/${held.id}/cancellations`;
  const selectedUrl = `/dashboard/reservations?reservation=${held.id}&branchId=${held.branchId}`;
  const denied = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await login(denied, "aina.sofia@example.invalid");
  const beforeDenied = counts()[upstream] ?? 0;
  const request = { operationId: crypto.randomUUID(), reason: "Customer requested release" };
  expect(
    (
      await denied.request.post(postPath, { headers: await headers(denied), data: request })
    ).status(),
  ).toBe(403);
  expect(
    (
      await context.request.post(postPath, {
        headers: { ...(await headers(context)), Origin: "https://outside.invalid" },
        data: request,
      })
    ).status(),
  ).toBe(403);
  expect(counts()[upstream] ?? 0).toBe(beforeDenied);
  const otherBranch = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await login(otherBranch, mobile ? "siti.aminah@example.invalid" : "lim.mei.ling@example.invalid");
  expect(
    (
      await otherBranch.request.post(postPath, {
        headers: await headers(otherBranch),
        data: request,
      })
    ).status(),
  ).toBe(404);
  await otherBranch.close();

  await page.goto(selectedUrl);
  const dialog = page.getByRole("dialog", { name: "Reservation details", exact: true });
  const button = (name: string) => dialog.getByRole("button", { name, exact: true });
  const reason = dialog.getByLabel("Cancellation reason", { exact: true });
  const cancel = button("Cancel reservation");
  const reconcile = button("Reconcile reservation cancellation");
  const table = page.getByRole("table", { name: "Reservations", exact: true });
  await expect(cancel).toBeEnabled();
  const beforeValidation = counts()[upstream] ?? 0;
  for (const invalid of ["", "   ", "Customer\nrequested release"]) {
    await reason.fill(invalid);
    await cancel.click();
    await expect(reason).toHaveAttribute("aria-invalid", "true");
  }
  expect(counts()[upstream] ?? 0).toBe(beforeValidation);
  await reason.fill("Private cancellation draft");
  await expect(button("Close details")).toBeDisabled();
  await context.addCookies(
    (await denied.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  await button("Refresh reservation details").click();
  await expect(dialog).toHaveCount(0);
  await expect(table).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Reservation access unavailable", exact: true }),
  ).toBeVisible();
  await context.addCookies(managerCookies);
  await page.goto(selectedUrl);
  await expect(reason).toHaveValue("");
  await reason.fill("  Customer requested release  ");
  const confirmed = await dialog.locator("dl").allTextContents();
  const mounted = await table.elementHandle();
  const rows = await table.locator("tbody tr").allTextContents();
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, uncertain: true, message: "Unavailable" },
    }),
  );
  await button("Refresh reservation details").click();
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toBeVisible();
  expect(await dialog.locator("dl").allTextContents()).toEqual(confirmed);
  await expect(reason).toHaveValue("  Customer requested release  ");
  await page.unroute(`**${endpoint}`);

  const attempts: string[] = [];
  const mismatches = ["id", "reservationId", "actorId", "reason", "kind", "refund"];
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let receipt: Record<string, unknown> = {};
  await page.route(`**${postPath}`, async (route) => {
    attempts.push(route.request().postData()!);
    if (attempts.length === 2) {
      await route.fulfill({
        status: 429,
        headers: { "Retry-After": "1" },
        json: { status: 429, data: null, uncertain: false, message: "Too many requests." },
      });
      return;
    }
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    expect(response.headers()["cache-control"]).toContain("no-store");
    const body = await response.json();
    receipt = body.data;
    if (attempts.length === 1) {
      await gate;
      await route.fulfill({
        status: 503,
        json: { status: 503, data: null, uncertain: true, message: "Lost cancellation response" },
      });
      return;
    }
    const key = mismatches[attempts.length - 3];
    await route.fulfill({
      response,
      json: key
        ? { ...body, data: { ...body.data, [key]: key === "refund" ? 499 : crypto.randomUUID() } }
        : body,
    });
  });
  try {
    await cancel.click();
    await expect.poll(() => attempts.length).toBe(1);
    await expect(reason).toBeDisabled();
    await expect(button("Close details")).toBeDisabled();
    await expect(button("Discard cancellation reason")).toBeDisabled();
    await expect(cancel).toBeDisabled();
  } finally {
    release();
  }
  await expect(reconcile).toBeEnabled();
  await button("Refresh reservation details").click();
  await expect(
    dialog.getByRole("heading", { name: "Simulated refund", exact: true }),
  ).toBeVisible();
  await expect(reconcile).toBeEnabled();
  await expect(reason).toHaveValue("  Customer requested release  ");
  await reconcile.click();
  await expect.poll(() => attempts.length).toBe(2);
  await expect(reconcile).toBeEnabled();
  await expect(reason).toBeDisabled();
  await expect(button("Close details")).toBeDisabled();
  for (let index = 0; index < mismatches.length; index += 1) {
    await reconcile.click();
    await expect.poll(() => attempts.length).toBe(index + 3);
    await expect(reconcile).toBeEnabled();
    await expect(button("Close details")).toBeDisabled();
  }
  await expect(dialog.getByRole("status").filter({ hasText: "Request confirmed" })).toHaveCount(0);
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, uncertain: true, message: "Unavailable" },
    }),
  );
  await page.route("**/api/staff/reservations?*", (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, uncertain: true, message: "Unavailable" },
    }),
  );
  await reconcile.click();
  await expect(reconcile).toHaveCount(0);
  await expect(button("Close details")).toBeEnabled();
  await expect(dialog.getByText("Refund reference", { exact: true }).locator("..")).toContainText(
    String(receipt.id),
  );
  await expect(dialog.getByText("Reason", { exact: true }).locator("..")).toContainText(
    "Customer requested release",
  );
  expect(await table.locator("tbody tr").allTextContents()).toEqual(rows);
  expect(await mounted!.evaluate((element) => element.isConnected)).toBe(true);
  await expect(page).toHaveURL(new RegExp(`reservation=${held.id}`));
  expect(new Set(attempts).size).toBe(1);
  const captured = JSON.parse(attempts[0]);
  expect(receipt).toMatchObject({
    id: captured.operationId,
    reservationId: held.id,
    kind: "StaffCancelled",
    reason: captured.reason,
    refund: 500,
  });
  expect(captured.reason).toBe("Customer requested release");
  await page.unroute(`**${postPath}`);
  await page.unroute(`**${endpoint}`);
  await page.unroute("**/api/staff/reservations?*");
  const persisted = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  expect(persisted.reservation.status).toBe("StaffCancelled");
  expect(persisted.release).toMatchObject({
    id: captured.operationId,
    refund: 500,
    reason: captured.reason,
  });
  const replay = await context.request.post(postPath, {
    headers: await headers(context),
    data: captured,
  });
  expect((await replay.json()).data).toEqual(receipt);
  const refreshedTable = page.waitForResponse(
    (response) => response.url().includes("/api/staff/reservations?") && response.status() === 200,
  );
  // Closing after the retained receipt allows deliberate recovery of the failed table refresh.
  await button("Close details").click();
  await page.getByRole("button", { name: "Refresh reservations", exact: true }).click();
  await refreshedTable;
  await expect(page.getByRole("button", { name: held.id, exact: true })).toHaveCount(0);
  await page.goto(selectedUrl);
  await expect(
    dialog.getByRole("heading", { name: "Simulated refund", exact: true }),
  ).toBeVisible();
  await expect(cancel).toHaveCount(0);
  await page.screenshot({
    path: path.join(evidence, "reservation-cancellation.png"),
    fullPage: true,
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await denied.close();
  writeFileSync(
    path.join(evidence, "reservation-cancellation.json"),
    JSON.stringify({
      receipt,
      captured,
      identicalAttempts: attempts.length,
      role: mobile ? "BranchManager" : "GeneralManager",
    }),
  );
});
