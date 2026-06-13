import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { signIn } from "./staff-sign-in";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function login(page: Page, email: string) {
  await signIn(page, email);
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
}

async function headers(context: BrowserContext) {
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}` };
}

async function preparedListing(context: BrowserContext, branchId: string) {
  for (let page = 1; ; page += 1) {
    const response = await context.request.get(
      `/api/staff/listings?branchId=${branchId}&pageSize=50&page=${page}`,
      { headers: await headers(context) },
    );
    expect(response.status()).toBe(200);
    const data = (await response.json()).data;
    const item = data.items.find((row: { revisionId: string | null }) => row.revisionId);
    if (item) {
      return item;
    }
    expect(page * data.pageSize).toBeLessThan(data.total);
  }
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
  const item = await preparedListing(context, branch);
  const outside = await preparedListing(context, "68f371ad-223a-43d6-b45b-b878eece79ae");
  const outsideWorkspace = await context.request.get(`/api/staff/listings/${outside.vehicleId}`, {
    headers: await headers(context),
  });
  const outsidePhotoId = (await outsideWorkspace.json()).data.draft.photoIds[0];
  const endpoint = `/api/staff/listings/${item.vehicleId}`;
  const workspace = await context.request.get(endpoint, { headers: await headers(context) });
  expect(workspace.status()).toBe(200);
  const saved = (await workspace.json()).data;
  const signedReport = saved.currentSignedReport;
  expect(signedReport).not.toBeNull();
  const reportPath = `/dashboard/inspection/${signedReport.inspectionId}/reports/${signedReport.revision}`;
  const photoIds: string[] = saved.draft.photoIds;
  expect(photoIds.length).toBeGreaterThan(1);
  const photoPath = `${endpoint}/photos/${photoIds[0]}`;
  await page.route(`**${photoPath}`, (route) => route.fulfill({ status: 503, body: "" }));
  const before = counts()[endpoint] ?? 0;
  const initial = await page.goto(
    `/dashboard/management?branchId=${branch}&pageSize=10&listing=${item.vehicleId}`,
  );
  const initialHtml = await initial!.text();
  expect(initialHtml).not.toContain(item.registrationPlate);
  expect(initialHtml).not.toContain(`href="${reportPath}"`);
  const dialog = page.getByRole("dialog", { name: item.registrationPlate, exact: true });
  const listingTab = dialog
    .getByRole("navigation", { name: "Listing workspace", exact: true })
    .getByRole("button", { name: "Listing", exact: true });
  const previewMode = dialog
    .getByRole("group", { name: "Editor view", exact: true })
    .getByRole("button", { name: "Preview", exact: true });
  const fieldsMode = dialog
    .getByRole("group", { name: "Editor view", exact: true })
    .getByRole("button", { name: "Fields", exact: true });
  if (info.project.name === "mobile") {
    await previewMode.click();
  }
  const reportLink = dialog.getByRole("link", {
    name: "Open signed inspection report (new tab)",
    exact: true,
  });
  await expect(reportLink).toHaveAttribute("href", reportPath);
  await expect(reportLink).toHaveAttribute("target", "_blank");
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(dialog.getByRole("heading", { name: "Saved listing", exact: true })).toBeVisible();
  expect(counts()[endpoint] - before).toBe(1);
  const table = page.getByRole("table", { name: "Listings", exact: true });
  const rows = await table.locator("tbody").innerText();
  const publicationDetails = dialog
    .getByRole("region", { name: "Saved draft for publication", exact: true })
    .locator("dl");
  const details = await publicationDetails.innerText();
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
  expect(await publicationDetails.innerText()).toBe(details);
  expect(page.url()).toBe(confirmedUrl);
  await page.unroute(`**${endpoint}`);
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toHaveCount(0);
  await listingTab.click();
  await expect(
    dialog.getByRole("heading", { name: "About this vehicle", exact: true }),
  ).toBeVisible();
  const popupOpened = page.waitForEvent("popup");
  await reportLink.click();
  const reportPage = await popupOpened;
  await expect(reportPage).toHaveURL(new RegExp(`${reportPath}$`));
  await expect(
    reportPage.getByRole("heading", { name: "Inspection report", exact: true }),
  ).toBeVisible();
  await expect(reportPage.locator("[data-inspection-report]")).toContainText(
    saved.currentSignedRevisionId,
  );
  await reportPage.close();
  await expect(dialog).toBeVisible();
  if (info.project.name === "mobile") {
    await fieldsMode.click();
  }
  const gallery = dialog.getByRole("group", { name: "Selected listing photos", exact: true });
  await expect(
    gallery.getByRole("status").filter({ hasText: "Photo 1 is unavailable." }),
  ).toBeVisible();
  const remaining = gallery.locator("img");
  for (const image of await remaining.all()) {
    await image.scrollIntoViewIfNeeded();
    await expect
      .poll(() => image.evaluate((element) => (element as HTMLImageElement).naturalWidth))
      .toBeGreaterThan(0);
  }
  const healthyPath = `${endpoint}/photos/${photoIds[1]}`;
  const healthyReads = counts()[healthyPath];
  await page.unroute(`**${photoPath}`);
  const recovered = page.waitForResponse((response) => response.url().endsWith(photoPath));
  await gallery.getByRole("button", { name: "Retry photo 1", exact: true }).click();
  const delivered = await recovered;
  expect(delivered.status()).toBe(200);
  expect(delivered.headers()["cache-control"]).toContain("no-store");
  expect(delivered.headers()["x-content-type-options"]).toBe("nosniff");
  expect(delivered.headers()["x-robots-tag"]).toBe("noindex, nofollow");
  await expect
    .poll(() =>
      gallery
        .locator("img")
        .first()
        .evaluate((image: HTMLImageElement) => image.naturalWidth),
    )
    .toBeGreaterThan(0);
  expect(
    await gallery
      .locator("img")
      .evaluateAll((images) =>
        images.map((image) => new URL(image.getAttribute("src")!, location.href).pathname),
      ),
  ).toEqual(photoIds.map((id) => `${endpoint}/photos/${id}`));
  expect(counts()[healthyPath]).toBe(healthyReads);
  await expect(
    gallery.getByRole("combobox", { name: "Photo 1 position", exact: true }),
  ).toHaveValue("0");
  const mediaReads = photoIds.map((id) => counts()[`${endpoint}/photos/${id}`]);
  const refreshed = page.waitForResponse((response) => response.url().endsWith(endpoint));
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  expect((await refreshed).status()).toBe(200);
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  await expect(listingTab).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toHaveCount(0);
  expect(photoIds.map((id) => counts()[`${endpoint}/photos/${id}`])).toEqual(mediaReads);
  if (info.project.name === "mobile") {
    await previewMode.click();
  }
  await expect(reportLink).toHaveAttribute("href", reportPath);
  const confirmedListingUrl = page.url();
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 200,
      json: {
        status: 200,
        data: { ...saved, currentSignedReport: { inspectionId: "invalid", revision: 0 } },
        uncertain: false,
      },
    }),
  );
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toBeVisible();
  await expect(reportLink).toHaveAttribute("href", reportPath);
  expect(page.url()).toBe(confirmedListingUrl);
  await page.unroute(`**${endpoint}`);
  await reportLink.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-report.png") });
  if (info.project.name === "mobile") {
    await fieldsMode.click();
  }
  await gallery.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-drawer.png") });
  let releasePhoto = () => {};
  let photoEntered = () => {};
  let photoCompleted = () => {};
  const photoGate = new Promise<void>((resolve) => {
    releasePhoto = resolve;
  });
  const photoPending = new Promise<void>((resolve) => {
    photoEntered = resolve;
  });
  const photoHandled = new Promise<void>((resolve) => {
    photoCompleted = resolve;
  });
  await page.route(`**${photoPath}?closingProbe=1`, async (route) => {
    const response = await route.fetch();
    photoEntered();
    await photoGate;
    await route.fulfill({ response }).catch(() => {});
    photoCompleted();
  });
  await gallery
    .locator("img")
    .first()
    .evaluate((image: HTMLImageElement) => {
      image.src = `${image.src}?closingProbe=1`;
    });
  await photoPending;
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  releasePhoto();
  await photoHandled;
  await expect(
    page.getByRole("group", { name: "Selected listing photos", exact: true }),
  ).toHaveCount(0);
  await page.unroute(`**${photoPath}?closingProbe=1`);
  expect(await table.locator("tbody").innerText()).toBe(rows);
  await expect(page).not.toHaveURL(/listing=/);

  const tableData = await context.request.get(
    `/api/staff/listings?branchId=${branch}&pageSize=10`,
    { headers: await headers(context) },
  );
  const opening = (await tableData.json()).data.items[0];
  const openingResponse = await context.request.get(`/api/staff/listings/${opening.vehicleId}`, {
    headers: await headers(context),
  });
  const openingWorkspace = (await openingResponse.json()).data;
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
  if (!opening.revisionId) {
    await expect(page.getByText("No photos yet.", { exact: true })).toBeVisible();
  }
  if (!openingWorkspace.currentSignedReport) {
    if (info.project.name === "mobile") {
      await page
        .getByRole("group", { name: "Editor view", exact: true })
        .getByRole("button", { name: "Preview", exact: true })
        .click();
    }
    await expect(
      page.getByText("No signed inspection report is linked.", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Open signed inspection report (new tab)", exact: true }),
    ).toHaveCount(0);
  }
  await page.getByRole("dialog").getByRole("button", { name: "Close dialog", exact: true }).click();
  await signOut(page);
  await login(page, "siti.aminah@example.invalid");
  const denied = await context.request.get(`/api/staff/listings/${outside.vehicleId}`, {
    headers: await headers(context),
  });
  const own = await context.request.get(endpoint, { headers: await headers(context) });
  expect(own.status()).toBe(200);
  expect(denied.status()).toBe(404);
  const ownPhoto = await context.request.get(photoPath, { headers: await headers(context) });
  expect(ownPhoto.status()).toBe(200);
  const deniedPhoto = await context.request.get(
    `/api/staff/listings/${outside.vehicleId}/photos/${outsidePhotoId}`,
    { headers: await headers(context) },
  );
  expect(deniedPhoto.status()).toBe(404);
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
  const beforePhotoDenied = counts()[photoPath];
  const forbiddenPhoto = await context.request.get(photoPath, { headers: await headers(context) });
  expect(forbiddenPhoto.status()).toBe(403);
  expect(counts()[photoPath]).toBe(beforePhotoDenied);
  const invalidPhoto = await context.request.get(`${endpoint}/photos/not-a-uuid`, {
    headers: await headers(context),
  });
  expect(invalidPhoto.status()).toBe(400);
  writeFileSync(
    path.join(evidence, `listing-drawer-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      retainedRefresh: true,
      supersededReadCancelled: true,
      salesDenied: true,
      savedPhotoOrder: photoIds,
      optionalPhotoRecovery: true,
      healthyPhotoRetained: true,
      scopedPrivateMedia: true,
      latePhotoDiscardedOnClose: true,
      exactSignedReportLink: reportPath,
      browserRenderedReportLink: true,
      malformedRefreshRetainsReportLink: true,
      reportReceipt: saved.currentSignedRevisionId,
    }),
  );
});
