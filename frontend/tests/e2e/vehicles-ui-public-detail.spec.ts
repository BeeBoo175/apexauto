import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { randomUUID } from "node:crypto";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;
const fault = (value: string) => writeFileSync(path.join(evidence, "fault.txt"), value);

test("public detail serves its primary snapshot and recovers browser reloads without exposing private records", async ({
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
  await expect(page.getByRole("heading", { name: "Inspection report", exact: true })).toBeVisible();
  await page.waitForLoadState("networkidle");
  expect((counts()[target] ?? 0) - beforeNavigation).toBe(1);

  const beforeEntry = counts()[target];
  const started = Date.now();
  const response = await page.goto(destination);
  const html = await response!.text();
  const contentMs = Date.now() - started;
  const markup = html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "");
  expect(markup).toMatch(/<h1\b/);
  expect(markup).toContain("Estimated total");
  expect(markup).toContain("Inspection report");
  expect(markup).toContain("Financing estimate");
  expect(markup).toContain('property="og:title"');
  expect(markup.match(/data-kind=/g)).toHaveLength(210);
  expect(response!.headers()["cache-control"]).toContain("no-store");
  await page.waitForLoadState("networkidle");
  expect(counts()[target] - beforeEntry).toBe(1);
  await expect(page.getByRole("button", { name: "Refresh vehicle", exact: true })).toHaveCount(0);

  const projection = await request.get(target);
  expect(projection.status()).toBe(200);
  const data = (await projection.json()).data;
  expect(data.checklist.points).toHaveLength(210);
  expect(JSON.stringify(data)).not.toMatch(
    /"(?:notes|photos|vin|registrationPlate|technicianId|buyer|intakeOwnerId)":/,
  );
  const gallery = page.getByRole("region", { name: "Vehicle photos", exact: true });
  const selectedPhoto = gallery.locator("figure img");
  const firstPhoto = `/api/vehicles/${id}/photos/${data.photoIds[0]}`;
  const healthyPhoto = `/api/vehicles/${id}/photos/${data.photoIds[1]}`;
  expect(data.photoIds.length).toBeGreaterThan(1);
  await page.route(`**${firstPhoto}`, (route) => route.fulfill({ status: 503, body: "" }));
  await page.reload();
  const retry = gallery.getByRole("button", { name: "Retry photo 1", exact: true });
  await expect(retry).toBeVisible();
  const coverThumbnail = gallery
    .getByRole("button", { name: "Show photo 1", exact: true })
    .locator("img");
  await expect
    .poll(() =>
      coverThumbnail.evaluate((node) => {
        const image = node as HTMLImageElement;
        return image.complete && image.naturalWidth === 0;
      }),
    )
    .toBe(true);
  await page.unroute(`**${firstPhoto}`);

  const healthyThumbnail = gallery
    .getByRole("button", { name: "Show photo 2", exact: true })
    .locator("img");
  await healthyThumbnail.scrollIntoViewIfNeeded();
  await expect
    .poll(() => healthyThumbnail.evaluate((node) => (node as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  const healthyBefore = counts()[healthyPhoto] ?? 0;
  await retry.click();
  await expect
    .poll(() => selectedPhoto.evaluate((node) => (node as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  await expect
    .poll(() => coverThumbnail.evaluate((node) => (node as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  expect(counts()[healthyPhoto] ?? 0).toBe(healthyBefore);

  for (let index = 0; index < data.photoIds.length; index += 1) {
    await gallery.getByRole("button", { name: `Show photo ${index + 1}`, exact: true }).click();
    await expect
      .poll(async () => new URL((await selectedPhoto.getAttribute("src"))!, page.url()).href)
      .toBe(new URL(`/api/vehicles/${id}/photos/${data.photoIds[index]}`, page.url()).href);
    await expect
      .poll(() => selectedPhoto.evaluate((node) => (node as HTMLImageElement).naturalWidth))
      .toBeGreaterThan(0);
  }
  await page.getByRole("heading", { level: 1 }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "public-detail.png"), fullPage: false });

  fault("unavailable");
  try {
    await page.reload();
    await expect(page.getByRole("heading", { name: "Vehicle details", exact: true })).toBeVisible();
    await expect(page.getByText("Reload the page to try again.", { exact: false })).toBeVisible();
    await expect(page.locator("dl")).toHaveCount(0);
  } finally {
    fault("");
  }
  await page.reload();
  await expect(page.getByRole("heading", { name: "Inspection report", exact: true })).toBeVisible();

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
  await expect(page.getByRole("heading", { name: "Inspection report", exact: true })).toBeVisible();
  await page.reload();
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
      primaryContentOutsideScripts: true,
      browserReloadRecovery: true,
      thumbnailRecovery: true,
      photoCount: data.photoIds.length,
    }),
  );
});
