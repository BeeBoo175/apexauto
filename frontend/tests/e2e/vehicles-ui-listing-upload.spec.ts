import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { listingDraft, listingDraftContents } from "../../src/lib/vehicles/listing-draft";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function headers(context: BrowserContext) {
  const token = (await context.cookies()).find((cookie) => cookie.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${token.value}`, Origin: process.env.APP_BASE_URL! };
}

async function login(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  const signIn = page.getByRole("button", { name: "Sign in", exact: true });
  const response = page.waitForResponse((entry) => entry.url().endsWith("/api/auth/login"));
  await signIn.click();
  const result = await response;
  if (result.status() === 429) {
    const seconds = Number(result.headers()["retry-after"]);
    expect(seconds).toBeGreaterThan(0);
    expect(seconds).toBeLessThanOrEqual(60);
    await new Promise((resolve) => setTimeout(resolve, seconds * 1000));
    await signIn.click();
  }
  await page.waitForURL(/\/(?:dashboard\/intake|auth\/continue)$/);
  if (new URL(page.url()).pathname === "/auth/continue") {
    const cookie = (await page.context().cookies()).find((item) => item.name === "apexauto_token")!;
    const probe = await page.context().request.get(`${process.env.API_BASE_URL}/api/auth/session`, {
      headers: { Authorization: `Bearer ${cookie.value}` },
    });
    if (probe.status() === 429) {
      const seconds = Number(probe.headers()["retry-after"]);
      expect(seconds).toBeGreaterThan(0);
      expect(seconds).toBeLessThanOrEqual(60);
      await new Promise((resolve) => setTimeout(resolve, seconds * 1000));
    } else {
      expect(probe.status()).toBe(200);
    }
    await page.getByRole("button", { name: "Try checking session again", exact: true }).click();
  }
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
}

test("listing uploads reconcile exact files and join the same draft without changing publication", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(180_000);
  await login(page, "farah.aziz@example.invalid");
  const cataloguePath = `/api/vehicles?branchId=${branch}&page=1`;
  const catalogue = (await (await context.request.get(cataloguePath)).json()).data;
  const item = catalogue.items[info.project.name === "mobile" ? 1 : 0];
  const endpoint = `/api/staff/listings/${item.id}`;
  const photoPath = `${endpoint}/photos`;
  const saved = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  await page.goto(`/dashboard/management?branchId=${branch}&listing=${item.id}`);
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  await dialog.getByRole("button", { name: "Edit photos", exact: true }).click();
  const upload = dialog.getByRole("region", { name: "Upload listing photo", exact: true });
  const selected = dialog.getByRole("region", { name: "Selected listing photos", exact: true });
  const sources = () =>
    selected
      .locator("img")
      .evaluateAll((images) => images.map((image) => image.getAttribute("src")!.split("/").pop()));
  const file = readFileSync(path.join(evidence, "upload.jpg"));
  const choose = () =>
    upload.getByLabel("New listing photo", { exact: true }).setInputFiles({
      name: "listing-exterior.jpg",
      mimeType: "image/jpeg",
      buffer: file,
    });
  await upload.getByLabel("New listing photo", { exact: true }).setInputFiles({
    name: "unsupported.svg",
    mimeType: "image/svg+xml",
    buffer: Buffer.from("<svg />"),
  });
  await expect(upload.getByRole("button", { name: "Add new photo", exact: true })).toBeEnabled();
  const attempts: { url: string; hash: string }[] = [];
  let cooldown = 0;
  let mismatchDelivered = false;

  async function uploadSelected() {
    cooldown = 0;
    await upload.getByRole("button", { name: "Upload photo", exact: true }).click();
    await expect(
      upload.getByRole("button", { name: /^(Upload photo|Retry same photo)$/ }),
    ).toBeEnabled();
    if (cooldown > 0) {
      expect(cooldown).toBeLessThanOrEqual(60);
      await new Promise((resolve) => setTimeout(resolve, cooldown * 1000));
      await upload.getByRole("button", { name: "Upload photo", exact: true }).click();
    }
  }

  await page.route(`**${photoPath}/*`, async (route) => {
    if (route.request().method() !== "PUT") {
      await route.continue();
      return;
    }
    attempts.push({
      url: route.request().url(),
      hash: createHash("sha256").update(route.request().postDataBuffer()!).digest("hex"),
    });
    const response = await route.fetch();
    if (response.status() === 429) {
      cooldown = Number(response.headers()["retry-after"]);
      expect(cooldown).toBeGreaterThan(0);
      await route.fulfill({ response });
      return;
    }
    expect(response.status()).toBe(200);
    const body = await response.json();
    const corrupt = !mismatchDelivered;
    mismatchDelivered = true;
    await route.fulfill(
      corrupt
        ? { response, json: { ...body, data: { ...body.data, vehicleId: crypto.randomUUID() } } }
        : { response },
    );
  });
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  const description = `New photography reviewed for ${info.project.name}.`;
  await dialog.getByLabel("Public description", { exact: true }).fill(description);
  await choose();
  await expect(dialog.getByRole("button", { name: "Save draft", exact: true })).toBeDisabled();
  await expect(
    dialog.getByRole("button", { name: "Back to listings", exact: true }),
  ).toBeDisabled();
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await expect(upload.getByText("listing-exterior.jpg", { exact: true })).toBeVisible();
  await uploadSelected();
  await expect(upload.getByRole("button", { name: "Retry same photo", exact: true })).toBeEnabled();
  expect(await sources()).toEqual(saved.draft.photoIds);
  await expect(
    upload.getByRole("button", { name: "Discard selected photo", exact: true }),
  ).toHaveCount(0);
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(
    upload.getByRole("button", { name: "Check photo receipt", exact: true }),
  ).toBeEnabled();
  const firstId = attempts[0].url.split("/").pop()!;
  await page.route(`**${photoPath}/${firstId}/receipt`, (route) =>
    route.fulfill({
      status: 404,
      json: { status: 404, data: null, message: "Not found", uncertain: false },
    }),
  );
  await upload.getByRole("button", { name: "Check photo receipt", exact: true }).click();
  await expect(upload.getByRole("button", { name: "Retry same photo", exact: true })).toBeEnabled();
  await expect(dialog.getByRole("button", { name: "Discard changes", exact: true })).toBeDisabled();
  await page.unroute(`**${photoPath}/${firstId}/receipt`);
  await upload.getByRole("button", { name: "Retry same photo", exact: true }).click();
  await expect(upload.getByRole("button", { name: "Add new photo", exact: true })).toBeEnabled();
  expect(attempts.length).toBeGreaterThanOrEqual(2);
  expect(
    attempts.every(
      (attempt) => attempt.url === attempts[0].url && attempt.hash === attempts[0].hash,
    ),
  ).toBe(true);
  expect(attempts[0].hash).toBe(createHash("sha256").update(file).digest("hex"));
  expect(await sources()).toEqual([...saved.draft.photoIds, firstId]);
  await page.unroute(`**${photoPath}/*`);
  let secondId = "";
  await page.route(`**${photoPath}/*`, async (route) => {
    if (route.request().method() !== "PUT") {
      await route.continue();
      return;
    }
    secondId = route.request().url().split("/").pop()!;
    const response = await route.fetch();
    if (response.status() === 429) {
      cooldown = Number(response.headers()["retry-after"]);
      expect(cooldown).toBeGreaterThan(0);
      await route.fulfill({ response });
      return;
    }
    expect(response.status()).toBe(200);
    await route.abort("failed");
  });
  await choose();
  await uploadSelected();
  await expect(
    upload.getByRole("button", { name: "Check photo receipt", exact: true }),
  ).toBeEnabled();
  await page.unroute(`**${photoPath}/*`);
  await upload.getByRole("button", { name: "Check photo receipt", exact: true }).click();
  await expect(upload.getByRole("button", { name: "Add new photo", exact: true })).toBeEnabled();
  const expected = [...saved.draft.photoIds, firstId, secondId];
  expect(await sources()).toEqual(expected);
  await expect(dialog.getByText(description, { exact: true })).toBeVisible();
  const beforeSave = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  expect(beforeSave.draft).toEqual(saved.draft);
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Back to listings", exact: true })).toBeEnabled();
  const current = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  expect(current.draft).toMatchObject({
    photoIds: expected,
    description,
    version: saved.draft.version + 1,
  });
  expect(current.approvedRevision).toEqual(saved.approvedRevision);
  expect(current.publication).toEqual(saved.publication);
  const publicAfter = (await (await context.request.get(cataloguePath)).json()).data;
  expect(publicAfter.items.find((row: { id: string }) => row.id === item.id)).toEqual(item);
  const receiptPath = `${photoPath}/${secondId}/receipt`;
  const receipt = await context.request.get(receiptPath, { headers: await headers(context) });
  expect(receipt.status()).toBe(200);
  expect(receipt.headers()["cache-control"]).toContain("no-store");
  expect(receipt.headers()["x-content-type-options"]).toBe("nosniff");
  const crossBefore = counts()[`${photoPath}/${secondId}`] ?? 0;
  const cross = await context.request.put(`${photoPath}/${secondId}`, {
    headers: {
      ...(await headers(context)),
      Origin: "https://untrusted.example",
      "Content-Type": "image/jpeg",
    },
    data: file,
  });
  expect(cross.status()).toBe(403);
  expect(counts()[`${photoPath}/${secondId}`] ?? 0).toBe(crossBefore);
  await choose();
  const outside = await context.request.post(`${endpoint}/drafts`, {
    headers: await headers(context),
    data: {
      operationId: crypto.randomUUID(),
      expectedVersion: current.draft.version,
      ...listingDraftContents(listingDraft(current.draft)),
      description: "Outside manager updated draft",
      photoIds: [secondId],
    },
  });
  expect(outside.status()).toBe(200);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByText("Newer saved draft available", { exact: true })).toBeVisible();
  await upload.getByRole("button", { name: "Discard selected photo", exact: true }).click();
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  expect(await sources()).toEqual([secondId]);
  await choose();
  await upload.getByRole("button", { name: "Upload photo", exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-photo-upload.png") });
  const other = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const otherPage = await other.newPage();
  await login(otherPage, "lim.mei.ling@example.invalid");
  expect((await other.request.get(receiptPath, { headers: await headers(other) })).status()).toBe(
    404,
  );
  expect(
    (
      await other.request.put(`${photoPath}/${crypto.randomUUID()}`, {
        headers: { ...(await headers(other)), "Content-Type": "image/jpeg" },
        data: file,
      })
    ).status(),
  ).toBe(404);
  await other.close();
  const sales = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const salesPage = await sales.newPage();
  await login(salesPage, "aina.sofia@example.invalid");
  const deniedId = crypto.randomUUID();
  const deniedBefore = counts()[`${photoPath}/${deniedId}`] ?? 0;
  expect(
    (
      await sales.request.put(`${photoPath}/${deniedId}`, {
        headers: { ...(await headers(sales)), "Content-Type": "image/jpeg" },
        data: file,
      })
    ).status(),
  ).toBe(403);
  expect(counts()[`${photoPath}/${deniedId}`] ?? 0).toBe(deniedBefore);
  await context.addCookies(
    (await sales.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  await upload.getByRole("button", { name: "Upload photo", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Listing access unavailable", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("listing-exterior.jpg", { exact: true })).toHaveCount(0);
  await sales.close();
  writeFileSync(
    path.join(evidence, `listing-upload-${info.project.name}.json`),
    JSON.stringify({
      attempts,
      firstId,
      secondId,
      expected,
      receiptReconciled: true,
      sameDraftSaved: true,
      publicationRetained: true,
      pendingTabsRetained: true,
      staleUploadDiscard: true,
      actorFileCleared: true,
    }),
  );
});
