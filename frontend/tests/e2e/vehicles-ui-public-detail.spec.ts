import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { randomUUID } from "node:crypto";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;
const fault = (value: string) => writeFileSync(path.join(evidence, "fault.txt"), value);

test("public detail shares its server snapshot and recovers deliberate reads without exposing private records", async ({
  page,
  request,
}, info) => {
  test.setTimeout(120_000);
  const branchId =
    info.project.name === "mobile"
      ? "68f371ad-223a-43d6-b45b-b878eece79ae"
      : "c77708bf-1a31-42f5-9a69-0c8766697a52";
  await page.goto(`/vehicles?branchId=${branchId}`);
  const link = page.getByRole("link", { name: /^View details for / }).first();
  const destination = (await link.getAttribute("href"))!;
  const id = destination.split("/").at(-1)!;
  const target = `/api/vehicles/${id}`;
  const beforeNavigation = counts()[target] ?? 0;
  await link.click();
  await expect(
    page.getByRole("heading", { name: "Itemized price estimate", exact: true }),
  ).toBeVisible();
  await page.waitForLoadState("networkidle");
  expect((counts()[target] ?? 0) - beforeNavigation).toBe(1);
  const beforeEntry = counts()[target];
  const started = Date.now();
  const response = await page.goto(destination);
  const html = await response!.text();
  const contentMs = Date.now() - started;
  expect(html).toContain("Itemized price estimate");
  expect(html).toContain("Signed inspection checklist");
  expect(html).toContain('property="og:title"');
  expect(response!.headers()["cache-control"]).toContain("no-store");
  await page.waitForLoadState("networkidle");
  expect(counts()[target] - beforeEntry).toBe(1);
  const projection = await request.get(target);
  expect(projection.status()).toBe(200);
  const data = (await projection.json()).data;
  expect(data.checklist.points).toHaveLength(210);
  expect(JSON.stringify(data)).not.toMatch(
    /"(?:notes|photos|vin|registrationPlate|technicianId|buyer|intakeOwnerId)":/,
  );
  const confirmedPrice = await page.locator("dl").textContent();
  const photos = page.getByRole("region", { name: "Vehicle photos", exact: true }).locator("img");
  for (const image of await photos.all()) {
    await image.scrollIntoViewIfNeeded();
    await expect
      .poll(() => image.evaluate((node) => (node as HTMLImageElement).naturalWidth))
      .toBeGreaterThan(0);
  }
  const firstPhoto = (await photos.first().getAttribute("src"))!;
  const healthyPhoto = (await photos.nth(1).getAttribute("src"))!;
  await page.route(`**${firstPhoto}`, (route) => route.fulfill({ status: 503, body: "" }));
  await page.reload();
  const retry = page.getByRole("button", { name: "Retry photo 1", exact: true });
  await expect(retry).toBeVisible();
  await page.unroute(`**${firstPhoto}`);
  const healthy = page.locator(`img[src="${healthyPhoto}"]`);
  await healthy.scrollIntoViewIfNeeded();
  await expect
    .poll(() => healthy.evaluate((node) => (node as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  const healthyBefore = counts()[healthyPhoto] ?? 0;
  await retry.click();
  await expect
    .poll(() => photos.first().evaluate((node) => (node as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  expect(counts()[healthyPhoto] ?? 0).toBe(healthyBefore);
  await page.locator("summary").first().click();
  const refresh = page.getByRole("button", { name: "Refresh vehicle", exact: true });
  fault("unavailable");
  try {
    await refresh.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "Your last confirmed details are still shown" }),
    ).toBeVisible();
    expect(await page.locator("dl").textContent()).toBe(confirmedPrice);
    await expect(page.locator("details").first()).toHaveAttribute("open", "");
  } finally {
    fault("");
  }
  await refresh.click();
  await expect(refresh).toBeEnabled();
  await page.getByRole("heading", { level: 1 }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "public-detail.png"), fullPage: false });

  fault("unavailable");
  try {
    await page.goto(destination);
    await expect(page.getByRole("heading", { name: "Vehicle details", exact: true })).toBeVisible();
    await expect(page.locator("dl")).toHaveCount(0);
  } finally {
    fault("");
  }
  await refresh.click();
  await expect(
    page.getByRole("heading", { name: "Itemized price estimate", exact: true }),
  ).toBeVisible();
  const login = await request.post(`${process.env.API_BASE_URL}/api/auth/login`, {
    data: { loginKey: randomUUID(), email: "farah.aziz@example.invalid", password: "Password123!" },
  });
  expect(login.status()).toBe(200);
  const headers = { Authorization: `Bearer ${(await login.json()).accessToken}` };
  const workspace = await request.get(`${process.env.API_BASE_URL}/api/staff/listings/${id}`, {
    headers,
  });
  expect(workspace.status()).toBe(200);
  const publicationId = (await workspace.json()).publication.id;
  const withdrawal = await request.post(
    `${process.env.API_BASE_URL}/api/staff/listings/${id}/withdrawals`,
    {
      headers,
      data: {
        operationId: randomUUID(),
        publicationId,
        reason: "Public detail availability review",
      },
    },
  );
  expect(withdrawal.status()).toBe(200);
  await expect(page.locator("dl")).toHaveCount(1);
  await refresh.click();
  await expect(
    page.getByText("This vehicle is currently unavailable.", { exact: false }),
  ).toBeVisible();
  await expect(page.locator("dl")).toHaveCount(0);
  expect((await request.get(firstPhoto)).status()).toBe(404);
  const holds = await request.get(
    `${process.env.API_BASE_URL}/api/staff/reservations?isActive=true&page=1&pageSize=10`,
    { headers },
  );
  expect(holds.status()).toBe(200);
  const heldId = (await holds.json()).items[0].receipt.vehicleId;
  const heldResponse = await page.goto(`/vehicles/${heldId}`);
  const heldHtml = await heldResponse!.text();
  expect(heldHtml).toContain('content="noindex, nofollow"');
  expect(heldHtml).not.toMatch(/@example\.invalid|"legalName"|"nric"|"buyer"/);
  await expect(
    page.getByText("This vehicle is currently unavailable.", { exact: false }),
  ).toBeVisible();
  writeFileSync(
    path.join(evidence, "public-detail.json"),
    JSON.stringify({
      id,
      heldId,
      contentMs,
      points: data.checklist.points.length,
      initialReads: 1,
      photoCount: data.photoIds.length,
    }),
  );
});
