import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { randomUUID } from "node:crypto";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

test("catalogue retains failed filter reads and uses browser reload for fresh availability", async ({
  page,
  request,
}, info) => {
  test.setTimeout(120_000);
  const mobile = info.project.name === "mobile";
  const branchId = mobile
    ? "68f371ad-223a-43d6-b45b-b878eece79ae"
    : "c77708bf-1a31-42f5-9a69-0c8766697a52";
  const query = `branchId=${branchId}&sort=price&direction=asc`;
  const before = counts()["/api/vehicles"] ?? 0;
  const initial = await page.goto(`/vehicles?${query}`);
  const html = await initial!.text();
  expect(html).toContain("<h2");
  expect(html).toContain("MYR");
  expect(html).toContain('property="og:title"');
  expect(initial!.headers()["cache-control"]).toContain("no-store");
  await page.waitForLoadState("networkidle");
  expect((counts()["/api/vehicles"] ?? 0) - before).toBe(1);
  await expect(page.getByRole("button", { name: "Refresh vehicles", exact: true })).toHaveCount(0);
  await expect(
    page.getByText("15 available vehicles · Page 1 of 2", { exact: true }),
  ).toBeVisible();
  const cards = page.getByRole("article");
  const visibleIds = () =>
    cards
      .locator("img")
      .evaluateAll((images) =>
        images.map(
          (image) => new URL(image.getAttribute("src")!, location.href).pathname.split("/")[3],
        ),
      );
  await expect(cards).toHaveCount(8);
  const firstIds = await visibleIds();
  const cover = cards.first().locator("img");
  await expect
    .poll(() => cover.evaluate((image) => (image as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  const photoResponse = await request.get((await cover.getAttribute("src")) as string);
  expect(photoResponse.status()).toBe(200);
  expect(photoResponse.headers()["cache-control"]).toContain("no-store");
  expect(photoResponse.headers()["x-content-type-options"]).toBe("nosniff");
  await page.screenshot({ path: path.join(evidence, "catalogue.png"), fullPage: false });
  for (const image of await cards.locator("img").all()) {
    await image.scrollIntoViewIfNeeded();
    await expect
      .poll(() => image.evaluate((element) => (element as HTMLImageElement).naturalWidth))
      .toBeGreaterThan(0);
  }
  const coverPath = new URL((await cover.getAttribute("src"))!, page.url()).pathname;
  await page.route(`**${coverPath}`, (route) => route.fulfill({ status: 503, body: "" }));
  await page.reload();
  await expect
    .poll(() =>
      cover.evaluate(
        (image) =>
          (image as HTMLImageElement).complete && (image as HTMLImageElement).naturalWidth === 0,
      ),
    )
    .toBe(true);
  await page.unroute(`**${coverPath}`);
  await page.reload();
  await expect
    .poll(() => cover.evaluate((image) => (image as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);

  await page.getByRole("button", { name: "Next vehicles", exact: true }).click();
  await expect(
    page.getByText("15 available vehicles · Page 2 of 2", { exact: true }),
  ).toBeVisible();
  await expect(cards).toHaveCount(7);
  const secondIds = await visibleIds();
  expect(new Set([...firstIds, ...secondIds]).size).toBe(15);
  await expect(page.getByRole("button", { name: "Next vehicles", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Previous vehicles", exact: true }).click();
  await expect(
    page.getByText("15 available vehicles · Page 1 of 2", { exact: true }),
  ).toBeVisible();
  expect(await visibleIds()).toEqual(firstIds);

  await page.getByText("Filter and sort vehicles", { exact: true }).click();
  await page.getByLabel("Model", { exact: true }).fill("Unsubmitted model");
  const confirmedUrl = page.url();
  writeFileSync(path.join(evidence, "fault.txt"), "unavailable");
  try {
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await expect(
      page
        .getByRole("alert")
        .filter({ hasText: "Your last confirmed vehicles and filters are still shown" }),
    ).toBeVisible();
    expect(page.url()).toBe(confirmedUrl);
    expect(await visibleIds()).toEqual(firstIds);
    await expect(page.getByLabel("Model", { exact: true })).toHaveValue("Unsubmitted model");
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
  let release: () => void = () => {};
  let entered: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const pending = new Promise<void>((resolve) => {
    entered = resolve;
  });
  await page.route("**/api/vehicles?**", async (route) => {
    entered();
    await gate;
    await route.continue();
  });
  await page.getByLabel("Model", { exact: true }).fill("");
  await page.getByLabel("Make", { exact: true }).fill("Proton");
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await pending;
  await page.getByLabel("Model", { exact: true }).fill("Edited during refresh");
  release();
  await expect(page).toHaveURL(/make=Proton/);
  await expect(page.getByLabel("Model", { exact: true })).toHaveValue("Edited during refresh");
  await page.unroute("**/api/vehicles?**");
  await page.getByLabel("Make", { exact: true }).fill("");
  await page.getByLabel("Model", { exact: true }).fill("");
  await page.getByLabel("Minimum price (MYR)", { exact: true }).fill("9999999");
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(page.getByText(/^No vehicles match these filters/)).toBeVisible();
  await page.getByLabel("Minimum price (MYR)", { exact: true }).fill("");
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await expect(cards).toHaveCount(8);

  const vehicleId = (await visibleIds())[0];
  const login = await request.post(`${process.env.API_BASE_URL}/api/auth/login`, {
    data: {
      loginKey: randomUUID(),
      email: "farah.aziz@example.invalid",
      password: "Password123!",
    },
  });
  expect(login.status()).toBe(200);
  const session = await login.json();
  const headers = { Authorization: `Bearer ${session.accessToken}` };
  const workspaceResponse = await request.get(
    `${process.env.API_BASE_URL}/api/staff/listings/${vehicleId}`,
    { headers },
  );
  expect(workspaceResponse.status()).toBe(200);
  const workspace = await workspaceResponse.json();
  const withdrawal = await request.post(
    `${process.env.API_BASE_URL}/api/staff/listings/${vehicleId}/withdrawals`,
    {
      headers,
      data: {
        operationId: randomUUID(),
        publicationId: workspace.publication.id,
        reason: "Showroom withdrawal during catalogue review",
      },
    },
  );
  expect(withdrawal.status()).toBe(200);
  expect(await visibleIds()).toContain(vehicleId);
  await page.reload();
  await expect(
    page.getByText("14 available vehicles · Page 1 of 2", { exact: true }),
  ).toBeVisible();
  expect(await visibleIds()).not.toContain(vehicleId);
  const removedPhoto = await request.get(
    `/api/vehicles/${vehicleId}/photos/${workspace.approvedRevision.photoIds[0]}`,
  );
  expect(removedPhoto.status()).toBe(404);
  const persisted = await request.get(
    `${process.env.API_BASE_URL}/api/staff/listings/${vehicleId}`,
    { headers },
  );
  expect((await persisted.json()).withdrawal.reason).toBe(
    "Showroom withdrawal during catalogue review",
  );
  const response = await request.get(`/api/vehicles?${query}`);
  const data = (await response.json()).data;
  expect(data.total).toBe(14);
  const metrics = await page.evaluate(() => ({
    navigation: performance.getEntriesByType("navigation").map((entry) => ({
      duration: entry.duration,
      encodedBodySize: (entry as PerformanceNavigationTiming).encodedBodySize,
    })),
    photos: performance
      .getEntriesByType("resource")
      .filter((entry) => entry.name.includes("/photos/"))
      .map((entry) => ({
        bytes: (entry as PerformanceResourceTiming).encodedBodySize,
        duration: entry.duration,
      })),
    horizontalOverflow: document.documentElement.scrollWidth > innerWidth,
  }));
  expect(metrics.horizontalOverflow).toBe(false);
  writeFileSync(
    path.join(evidence, "catalogue.json"),
    JSON.stringify(
      {
        htmlBytes: Buffer.byteLength(html),
        catalogueBytes: Buffer.byteLength(JSON.stringify(data)),
        metrics,
        initialCatalogueReads: 1,
        browserReloadRefresh: true,
        failedFilterDraftRetained: true,
        fullPartialPageIds: [...firstIds, ...secondIds],
        withdrawnVehicleId: vehicleId,
      },
      null,
      2,
    ),
  );
});
