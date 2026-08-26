import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { listingDraft, listingDraftContents } from "../../src/lib/vehicles/listing-draft";
import { formatCurrency } from "../../src/lib/currency-format";
import { privateHeaders, signInAs } from "./staff-session";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

test("manager explicitly approves only saved drafts with bound receipts, exact replay and actor isolation", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(120_000);
  await (info.project.name === "mobile"
    ? signInAs(page, "siti.aminah@example.invalid", "BranchManager")
    : signInAs(page, "farah.aziz@example.invalid", "GeneralManager"));
  const cataloguePath = `/api/vehicles?branchId=${branch}&page=1`;
  const catalogue = (await (await context.request.get(cataloguePath)).json()).data;
  const item = catalogue.items[info.project.name === "mobile" ? 1 : 0];
  const endpoint = `/api/staff/listings/${item.id}`;
  const postPath = `${endpoint}/publications`;
  const savePath = `${endpoint}/drafts`;
  const readWorkspace = async () =>
    (await (await context.request.get(endpoint, { headers: await privateHeaders(context) })).json())
      .data;
  const saved = await readWorkspace();
  // Media recovery has separate acceptance; this journey keeps the approval controls usable during an image outage.
  await page.route(new RegExp(`${endpoint}/photos/[^/?]+$`), (route) =>
    route.fulfill({ status: 503, body: "" }),
  );
  const initialReads = counts()[endpoint] ?? 0;
  const entry = await page.goto(`/dashboard/listings/${item.id}/publication?branchId=${branch}`);
  expect(await entry!.text()).not.toContain("Saved listing");
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  await expect(dialog.getByRole("heading", { name: "Saved listing", exact: true })).toBeVisible();
  const approve = dialog.getByRole("button", { name: "Approve publication", exact: true });
  const confirm = (title: string, label: string) =>
    page
      .getByRole("dialog", { name: title, exact: true })
      .getByRole("button", { name: label, exact: true })
      .click();
  async function showFields() {
    await dialog.getByRole("button", { name: "Listing", exact: true }).click();
    const show = dialog.getByRole("button", { name: "Show listing fields", exact: true });
    if (await show.isVisible()) {
      await show.click();
    }
  }
  const reconcile = dialog.getByRole("button", {
    name: "Reconcile publication approval",
    exact: true,
  });
  await expect(approve).toHaveCount(0);
  expect((counts()[endpoint] ?? 0) - initialReads).toBe(1);
  const writesBefore = counts()[postPath] ?? 0;
  await showFields();
  const price = (saved.draft.vehiclePrice + 441.01).toFixed(2);
  await dialog.getByLabel("Vehicle price (RM)", { exact: true }).fill(price);
  const order = saved.draft.photoIds;
  const saveAttempts: string[] = [];
  await page.route(`**${savePath}`, async (route) => {
    saveAttempts.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    expect(response.headers()["cache-control"]).toContain("no-store");
    if (saveAttempts.length === 1) {
      await route.fulfill({
        status: 503,
        json: { status: 503, data: null, uncertain: true, message: "Lost save" },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    dialog.getByRole("button", { name: "Reconcile draft save", exact: true }),
  ).toBeEnabled();
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toHaveCount(0);
  await dialog.getByRole("button", { name: "Reconcile draft save", exact: true }).click();
  await expect(approve).toBeEnabled();
  expect(saveAttempts[1]).toBe(saveAttempts[0]);
  await page.unroute(`**${savePath}`);
  const prepared = await readWorkspace();
  expect(prepared.draft.photoIds).toEqual(order);
  expect(prepared.publication).toEqual(saved.publication);
  const publicBefore = (await (await context.request.get(cataloguePath)).json()).data;
  expect(publicBefore.items.find((row: { id: string }) => row.id === item.id)).toEqual(item);
  await showFields();
  await dialog.getByLabel("Vehicle price (RM)", { exact: true }).fill("123.456");
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toHaveCount(0);
  await showFields();
  await expect(dialog.getByLabel("Vehicle price (RM)", { exact: true })).toHaveValue("123.456");
  await dialog.getByLabel("Vehicle price (RM)", { exact: true }).fill("45000.01");
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toHaveCount(0);
  await showFields();
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  await confirm("Discard listing changes?", "Discard changes");
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toBeEnabled();
  await showFields();
  await dialog.getByRole("button", { name: "Remove photo 1", exact: true }).click();
  await dialog
    .getByLabel("New listing photo", { exact: true })
    .setInputFiles(path.join(evidence, "upload.jpg"));
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toHaveCount(0);
  await showFields();
  await dialog.getByRole("button", { name: "Discard", exact: true }).click();
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  await confirm("Discard listing changes?", "Discard changes");
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toBeEnabled();
  expect(counts()[postPath] ?? 0).toBe(writesBefore);

  const attempts: string[] = [];
  const mismatches = ["signedRevisionId", "actorId", "branchId", "vehicleId", "id", "revisionId"];
  await page.route(`**${postPath}`, async (route) => {
    attempts.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    const body = await response.json();
    const key = mismatches[attempts.length - 1];
    await route.fulfill({
      response,
      json: key ? { ...body, data: { ...body.data, [key]: crypto.randomUUID() } } : body,
    });
  });
  await approve.click();
  await confirm("Approve publication?", "Approve publication");
  await expect(reconcile).toBeEnabled();
  await expect(dialog.getByRole("button", { name: "Save draft", exact: true })).toHaveCount(0);
  await expect(dialog.getByRole("button", { name: "Close drawer", exact: true })).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Listing", exact: true }).click();
  await dialog.getByRole("button", { name: "Catalogue card", exact: true }).click();
  await expect(dialog.getByRole("region", { name: "Listing preview", exact: true })).toContainText(
    formatCurrency(Number(price)),
  );
  await expect(reconcile).toBeEnabled();
  await showFields();
  await expect(dialog.getByLabel("Vehicle price (RM)", { exact: true })).toBeDisabled();
  // An unconfirmed approval locks Refresh; reconciling is the only way to read the result.
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeDisabled();
  await expect(reconcile).toBeEnabled();
  for (let index = 1; index < mismatches.length; index += 1) {
    await reconcile.click();
    await expect.poll(() => attempts.length).toBe(index + 1);
    await expect(reconcile).toBeEnabled();
  }
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Unavailable", uncertain: true },
    }),
  );
  const failedApprovalRefresh = page.waitForResponse(
    (response) => response.url().endsWith(endpoint) && response.status() === 503,
  );
  await reconcile.click();
  await failedApprovalRefresh;
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  await expect(
    dialog.getByRole("region", { name: "Listing vehicle and publication", exact: true }),
  ).toContainText(formatCurrency(Number(price)));
  await expect(approve).toHaveCount(0);
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Close drawer", exact: true })).toBeEnabled();
  await page.unroute(`**${endpoint}`);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Refresh listing", exact: true })).toBeEnabled();
  await expect(
    dialog.getByRole("region", { name: "Listing vehicle and publication", exact: true }),
  ).toContainText(formatCurrency(Number(price)));
  await page.unroute(`**${postPath}`);
  expect(attempts).toHaveLength(mismatches.length + 1);
  expect(new Set(attempts).size).toBe(1);
  const captured = JSON.parse(attempts[0]);
  const current = await readWorkspace();
  expect(captured).toMatchObject({
    revisionId: prepared.draft.id,
    signedRevisionId: prepared.currentSignedRevisionId,
  });
  expect(current.publication).toMatchObject({
    id: captured.operationId,
    revisionId: prepared.draft.id,
    signedRevisionId: prepared.currentSignedRevisionId,
  });
  expect(current.approvedRevision).toEqual(prepared.draft);
  const publicAfter = (await (await context.request.get(cataloguePath)).json()).data;
  expect(publicAfter.items.find((row: { id: string }) => row.id === item.id)).toMatchObject({
    vehiclePrice: Number(price),
    coverPhotoId: order[0],
  });
  await expect(page.getByRole("table", { name: "Listings", exact: true })).toBeVisible();
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toHaveCount(0);
  await dialog
    .getByRole("heading", { name: "Saved listing", exact: true })
    .scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-approval.png") });

  const outside = await context.request.post(savePath, {
    headers: await privateHeaders(context),
    data: {
      ...listingDraftContents(listingDraft(current.draft)),
      operationId: crypto.randomUUID(),
      expectedVersion: current.draft.version,
      description: "Newer saved manager draft",
    },
  });
  expect(outside.status()).toBe(200);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await expect(approve).toBeEnabled();
  const replacement = await readWorkspace();
  const newer = await context.request.post(savePath, {
    headers: await privateHeaders(context),
    data: {
      ...listingDraftContents(listingDraft(replacement.draft)),
      operationId: crypto.randomUUID(),
      expectedVersion: replacement.draft.version,
      description: "Outside writer superseded approval target",
    },
  });
  expect(newer.status()).toBe(200);
  const deniedResponse = page.waitForResponse(
    (response) => response.url().endsWith(postPath) && response.status() === 409,
  );
  await approve.click();
  await confirm("Approve publication?", "Approve publication");
  await deniedResponse;
  await expect(approve).toBeEnabled();
  expect((await readWorkspace()).publication).toEqual(current.publication);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(approve).toBeEnabled();
  const securityBefore = counts()[postPath] ?? 0;
  expect(
    (
      await context.request.post(postPath, {
        headers: { ...(await privateHeaders(context)), Origin: "https://outside.invalid" },
        data: captured,
      })
    ).status(),
  ).toBe(403);
  expect(counts()[postPath] ?? 0).toBe(securityBefore);
  const deniedContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const deniedPage = await deniedContext.newPage();
  await signInAs(deniedPage, "lim.mei.ling@example.invalid", "BranchManager");
  expect(
    (
      await deniedContext.request.post(postPath, {
        headers: await privateHeaders(deniedContext),
        data: { ...captured, operationId: crypto.randomUUID() },
      })
    ).status(),
  ).toBe(404);
  await deniedContext.close();
  await page.route(`**${postPath}`, async (route) => {
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    await route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Lost approval", uncertain: true },
    });
  });
  await approve.click();
  await confirm("Approve publication?", "Approve publication");
  await expect(reconcile).toBeEnabled();
  const salesContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const salesPage = await salesContext.newPage();
  await signInAs(salesPage, "aina.sofia@example.invalid", "SalesAgent");
  const beforeSales = counts()[postPath] ?? 0;
  expect(
    (
      await salesContext.request.post(postPath, {
        headers: await privateHeaders(salesContext),
        data: captured,
      })
    ).status(),
  ).toBe(403);
  expect(counts()[postPath] ?? 0).toBe(beforeSales);
  await context.addCookies(
    (await salesContext.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  // A locked drawer cannot refresh, so reload to read as the new actor.
  await page.reload();
  await expect(dialog).toHaveCount(0);
  await expect(reconcile).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "You don't have access to this page", exact: true }),
  ).toBeVisible();
  await salesContext.close();
  writeFileSync(
    path.join(evidence, `listing-approval-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      exactSaveAttempts: saveAttempts,
      exactApprovalAttempts: attempts,
      savedRevision: prepared.draft.id,
      publication: current.publication,
      publicPrice: Number(price),
      publicPhotoOrder: order,
      staleApprovalDenied: true,
      actorPendingApprovalCleared: true,
      confirmedReceiptRetainedOnFailedRefresh: true,
      role: info.project.name === "mobile" ? "BranchManager" : "GeneralManager",
      dirtyAndPendingUploadBlockUnapprovedDraft: true,
    }),
  );
});
