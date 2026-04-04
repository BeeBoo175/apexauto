import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { formatCurrency } from "../../src/lib/currency-format";
import { listingDraft, listingDraftContents } from "../../src/lib/vehicles/listing-draft";

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

test("catalogue preview follows the same local draft through invalid edits, photo order and save recovery", async ({
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
  const initialReads = counts()[endpoint] ?? 0;
  const metadataReads = counts()[`${endpoint}/photos`] ?? 0;
  const writesBefore = counts()[postPath] ?? 0;
  const entry = await page.goto(
    `/dashboard/management?branchId=${branch}&listing=${item.id}&section=card`,
  );
  expect(await entry!.text()).not.toContain("Catalogue card preview");
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  const preview = dialog.getByRole("region", { name: "Catalogue card preview", exact: true });
  const card = preview.getByRole("article");
  await expect(card).toContainText(formatCurrency(saved.draft.vehiclePrice));
  expect((counts()[endpoint] ?? 0) - initialReads).toBe(1);
  expect(counts()[`${endpoint}/photos`] ?? 0).toBe(metadataReads);
  const initialCard = await card.innerText();
  await expect(card.locator("img")).toHaveAttribute(
    "src",
    new RegExp(`${endpoint}/photos/${saved.draft.photoIds[0]}$`),
  );
  await expect
    .poll(() => card.locator("img").evaluate((image) => (image as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  const publicPage = await context.newPage();
  await publicPage.goto(`/vehicles?branchId=${branch}`);
  const publicCard = publicPage.getByRole("article").filter({
    has: publicPage.locator(`img[src$="/api/vehicles/${item.id}/photos/${item.coverPhotoId}"]`),
  });
  expect(await publicCard.innerText()).toBe(initialCard);
  await publicPage.close();
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await dialog.getByRole("button", { name: "Edit price", exact: true }).click();
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("123.456");
  await dialog.getByRole("button", { name: "Edit specifications", exact: true }).click();
  await dialog.getByLabel("Transmission", { exact: true }).selectOption("");
  await dialog.getByLabel("Fuel", { exact: true }).selectOption("PlugInHybrid");
  await dialog.getByLabel("Road tax (MYR)", { exact: true }).fill("19.999");
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  const description = `Local description ${info.project.name}`;
  await dialog.getByLabel("Public description", { exact: true }).fill(description);
  await dialog.getByRole("button", { name: "Catalogue card", exact: true }).click();
  await expect(card).toContainText("123.456");
  await expect(card).toContainText("Not set · Plug-in hybrid");
  await expect(preview.getByText("Enter a valid vehicle price before saving.")).toBeVisible();
  await expect(card).not.toContainText(formatCurrency(saved.draft.vehiclePrice));
  await expect(card).not.toContainText(description);
  await expect(
    dialog.getByRole("button", { name: "Back to listings", exact: true }),
  ).toBeDisabled();
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await dialog.getByRole("button", { name: "Edit price", exact: true }).click();
  const price = (saved.draft.vehiclePrice + 321.01).toFixed(2);
  await expect(dialog.getByLabel("Vehicle price (MYR)", { exact: true })).toHaveValue("123.456");
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill(price);
  await dialog.getByRole("button", { name: "Edit specifications", exact: true }).click();
  await expect(dialog.getByLabel("Road tax (MYR)", { exact: true })).toHaveValue("19.999");
  await dialog.getByLabel("Road tax (MYR)", { exact: true }).fill("199.01");
  await dialog.getByLabel("Transmission", { exact: true }).selectOption("Manual");
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  await expect(dialog.getByLabel("Public description", { exact: true })).toHaveValue(description);
  await dialog.getByRole("button", { name: "Edit photos", exact: true }).click();
  const selected = dialog.getByRole("region", { name: "Selected listing photos", exact: true });
  await selected.getByRole("button", { name: "Move photo 2 earlier", exact: true }).click();
  const ordered = [
    saved.draft.photoIds[1],
    saved.draft.photoIds[0],
    ...saved.draft.photoIds.slice(2),
  ];
  const coverPath = `${endpoint}/photos/${ordered[0]}`;
  await page.route(`**${coverPath}`, (route) => route.fulfill({ status: 503, body: "" }));
  await dialog.getByRole("button", { name: "Catalogue card", exact: true }).click();
  await expect(card).toContainText(formatCurrency(Number(price)));
  await expect(card).toContainText("Manual · Plug-in hybrid");
  await expect(card.locator("img")).toHaveAttribute(
    "src",
    new RegExp(`${endpoint}/photos/${ordered[0]}$`),
  );
  await expect
    .poll(() =>
      card
        .locator("img")
        .evaluate(
          (image) =>
            (image as HTMLImageElement).complete && (image as HTMLImageElement).naturalWidth === 0,
        ),
    )
    .toBe(true);
  await page.unroute(`**${coverPath}`);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect
    .poll(() => card.locator("img").evaluate((image) => (image as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);
  await expect(card).toContainText(formatCurrency(Number(price)));
  const local = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  expect(local.draft).toEqual(saved.draft);
  expect(counts()[postPath] ?? 0).toBe(writesBefore);
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({ status: 503, json: { status: 503, data: null, message: "Unavailable" } }),
  );
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "record could not be refreshed" }),
  ).toBeVisible();
  await expect(card).toContainText(formatCurrency(Number(price)));
  await expect(card.locator("img")).toHaveAttribute(
    "src",
    new RegExp(`${endpoint}/photos/${ordered[0]}$`),
  );
  await page.unroute(`**${endpoint}`);
  const attempts: string[] = [];
  await page.route(`**${postPath}`, async (route) => {
    attempts.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    const body = await response.json();
    await route.fulfill(
      attempts.length === 1
        ? { response, json: { ...body, data: { ...body.data, vehicleId: branch } } }
        : { response },
    );
  });
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    dialog.getByRole("button", { name: "Reconcile draft save", exact: true }),
  ).toBeEnabled();
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await dialog.getByRole("button", { name: "Catalogue card", exact: true }).click();
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  await expect(card).toContainText(formatCurrency(Number(price)));
  await expect(card.locator("img")).toHaveAttribute(
    "src",
    new RegExp(`${endpoint}/photos/${ordered[0]}$`),
  );
  await expect(dialog.getByRole("button", { name: "Discard changes", exact: true })).toBeDisabled();
  await dialog.getByRole("button", { name: "Reconcile draft save", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Back to listings", exact: true })).toBeEnabled();
  expect(attempts).toHaveLength(2);
  expect(attempts[0]).toBe(attempts[1]);
  await page.unroute(`**${postPath}`);
  const current = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  expect(current.draft).toMatchObject({
    vehiclePrice: Number(price),
    roadTax: 199.01,
    transmission: "Manual",
    fuel: "PlugInHybrid",
    photoIds: ordered,
    description,
    version: saved.draft.version + 1,
  });
  expect(current.approvedRevision).toEqual(saved.approvedRevision);
  expect(current.publication).toEqual(saved.publication);
  const publicAfter = (await (await context.request.get(cataloguePath)).json()).data;
  expect(publicAfter.items.find((row: { id: string }) => row.id === item.id)).toEqual(item);
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  await dialog.getByRole("button", { name: "Edit photos", exact: true }).click();
  for (let index = 0; index < ordered.length; index += 1) {
    await selected.getByRole("button", { name: "Remove photo 1", exact: true }).click();
  }
  await dialog.getByRole("button", { name: "Catalogue card", exact: true }).click();
  await expect(card.getByText("Photography unavailable", { exact: true })).toBeVisible();
  const outside = await context.request.post(postPath, {
    headers: await headers(context),
    data: {
      operationId: crypto.randomUUID(),
      expectedVersion: current.draft.version,
      ...listingDraftContents(listingDraft(current.draft)),
      vehiclePrice: 99876.54,
    },
  });
  expect(outside.status()).toBe(200);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByText("Newer saved draft available", { exact: true })).toBeVisible();
  await expect(card).toContainText(formatCurrency(Number(price)));
  await expect(card.locator("img")).toHaveCount(0);
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  await expect(card).toContainText(formatCurrency(99876.54));
  await expect(card.locator("img")).toHaveAttribute(
    "src",
    new RegExp(`${endpoint}/photos/${ordered[0]}$`),
  );
  await expect(page.getByRole("table", { name: "Listings", exact: true })).toBeVisible();
  await expect(card).toBeVisible();
  await preview
    .getByRole("heading", { name: "Catalogue card preview", exact: true })
    .scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-card-preview.png") });
  const sales = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const salesPage = await sales.newPage();
  await login(salesPage, "aina.sofia@example.invalid");
  await context.addCookies(
    (await sales.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(
    page.getByRole("region", { name: "Catalogue card preview", exact: true }),
  ).toHaveCount(0);
  await sales.close();
  writeFileSync(
    path.join(evidence, `listing-preview-${info.project.name}.json`),
    JSON.stringify({
      initialCard,
      ordered,
      exactAttempts: attempts,
      sameDraft: true,
      invalidValuesRetained: true,
      approvedProjectionRetained: true,
      failedRefreshRetained: true,
      staleDraftRetained: true,
      emptyCover: true,
      actorPreviewCleared: true,
      metadataReadsBeforeOpen: metadataReads,
    }),
  );
});
