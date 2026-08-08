import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { listingDraft, listingDraftContents } from "../../src/lib/vehicles/listing-draft";
import { apiLogin, privateHeaders } from "./staff-session";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

test("manager withdraws an exact publication with retained drafts, receipts, refunds and actor isolation", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(150_000);
  const mobile = info.project.name === "mobile";
  const branch = mobile
    ? "68f371ad-223a-43d6-b45b-b878eece79ae"
    : "c77708bf-1a31-42f5-9a69-0c8766697a52";
  await apiLogin(context, mobile ? "lim.mei.ling@example.invalid" : "farah.aziz@example.invalid");
  const cataloguePath = `/api/vehicles?branchId=${branch}&page=1`;
  const catalogue = (await (await context.request.get(cataloguePath)).json()).data;
  const item = catalogue.items[mobile ? 1 : 0];
  const publicDetail = `${process.env.API_BASE_URL}/api/vehicles/${item.id}`;
  expect((await context.request.get(publicDetail)).status()).toBe(200);
  const endpoint = `/api/staff/listings/${item.id}`;
  const postPath = `${endpoint}/withdrawals`;
  const readWorkspace = async () =>
    (await (await context.request.get(endpoint, { headers: await privateHeaders(context) })).json())
      .data;
  const saved = await readWorkspace();
  // Gallery recovery has separate acceptance; keep command checks within the unchanged request budget.
  await page.route(/\/api\/staff\/listings\/[^/]+\/photos\/[^/?]+$/, (route) =>
    route.fulfill({ status: 503, body: "" }),
  );
  const initialReads = counts()[endpoint] ?? 0;
  const entry = await page.goto(`/dashboard/listings/${item.id}/publication?branchId=${branch}`);
  expect(await entry!.text()).not.toContain("Withdraw approved listing");
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  const button = (name: string) => dialog.getByRole("button", { name, exact: true });
  async function showFields() {
    await button("Listing").click();
    if (mobile) {
      await button("Fields").click();
    } else if (await button("Show listing fields").isVisible()) {
      await button("Show listing fields").click();
    }
  }
  async function showCard() {
    await button("Listing").click();
    if (mobile) {
      await button("Preview").click();
    }
    await button("Catalogue card").click();
  }
  const reason = dialog.getByLabel("Withdrawal reason", { exact: true });
  const confirm = (title: string, label: string) =>
    page
      .getByRole("dialog", { name: title, exact: true })
      .getByRole("button", { name: label, exact: true })
      .click();
  const withdraw = button("Withdraw listing");
  const reconcile = button("Reconcile listing withdrawal");
  // The withdraw action appears once a reason is being typed and stays disabled until it is valid.
  await expect(reason).toBeVisible();
  await expect(withdraw).toHaveCount(0);
  expect((counts()[endpoint] ?? 0) - initialReads).toBe(1);
  const beforeValidation = counts()[postPath] ?? 0;
  await reason.fill("   ");
  await expect(withdraw).toBeDisabled();
  await reason.fill("Showroom\nreview");
  await expect(withdraw).toBeDisabled();
  expect(counts()[postPath] ?? 0).toBe(beforeValidation);
  await reason.fill("");
  await reason.fill("Reason retained while discarding listing edits");
  await showFields();
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("123.456");
  await button("Discard changes").click();
  await confirm("Discard listing changes?", "Discard changes");
  await button("Publication").click();
  await expect(reason).toHaveValue("Reason retained while discarding listing edits");
  // Unsaved reason text guards closing behind a discard prompt.
  await button("Close drawer").click();
  await page
    .getByRole("dialog", { name: "Discard unsaved changes?", exact: true })
    .getByRole("button", { name: "Keep editing", exact: true })
    .click();
  await reason.fill("");

  await showFields();
  await dialog.getByRole("button", { name: "Remove photo 1", exact: true }).click();
  await dialog
    .getByLabel("New listing photo", { exact: true })
    .setInputFiles(path.join(evidence, "upload.jpg"));
  await button("Publication").click();
  await expect(withdraw).toHaveCount(0);
  await showFields();
  await button("Discard").click();
  await button("Discard changes").click();
  await confirm("Discard listing changes?", "Discard changes");
  const savePath = `${endpoint}/drafts`;
  const publicationPath = `${endpoint}/publications`;
  await button("Publication").click();
  const prepared = await readWorkspace();
  await reason.fill("  Showroom stock review  ");
  const outsideDraft = await context.request.post(savePath, {
    headers: await privateHeaders(context),
    data: {
      ...listingDraftContents(listingDraft(prepared.draft)),
      operationId: crypto.randomUUID(),
      expectedVersion: prepared.draft.version,
      description: "Newer outside publication",
    },
  });
  expect(outsideDraft.status()).toBe(200);
  const newerDraft = (await outsideDraft.json()).data;
  expect(
    (
      await context.request.post(publicationPath, {
        headers: await privateHeaders(context),
        data: {
          operationId: crypto.randomUUID(),
          revisionId: newerDraft.id,
          signedRevisionId: prepared.currentSignedRevisionId,
        },
      })
    ).status(),
  ).toBe(200);
  const conflict = page.waitForResponse(
    (response) => response.url().endsWith(postPath) && response.status() === 409,
  );
  await withdraw.click();
  await confirm("Withdraw this listing?", "Withdraw listing");
  await conflict;
  await expect(reason).toHaveValue("  Showroom stock review  ");
  // Refreshing is locked while a reason is typed, so clear it before reading the newer publication.
  await expect(button("Refresh listing")).toBeDisabled();
  await reason.fill("");
  await expect(withdraw).toHaveCount(0);
  await button("Refresh listing").click();
  await expect(button("Refresh listing")).toBeEnabled();
  const target = await readWorkspace();
  await reason.fill("  Showroom stock review  ");
  await showFields();
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("123.456");
  await button("Publication").click();
  await expect(reason).toHaveValue("  Showroom stock review  ");
  await expect(withdraw).toBeEnabled();

  const attempts: string[] = [];
  const mismatches = ["publicationId", "actorId", "branchId", "vehicleId", "id", "reason"];
  await page.route(`**${postPath}`, async (route) => {
    attempts.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    expect(response.headers()["cache-control"]).toContain("no-store");
    expect(response.headers()["x-robots-tag"]).toContain("noindex");
    const body = await response.json();
    const key = mismatches[attempts.length - 1];
    await route.fulfill({
      response,
      json: key ? { ...body, data: { ...body.data, [key]: crypto.randomUUID() } } : body,
    });
  });
  await withdraw.click();
  await confirm("Withdraw this listing?", "Withdraw listing");
  await expect(reconcile).toBeEnabled();
  await expect(reason).toBeDisabled();
  await expect(button("Save draft")).toHaveCount(0);
  await expect(button("Approve publication")).toHaveCount(0);
  await expect(button("Discard changes")).toHaveCount(0);
  await expect(button("Close drawer")).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await showCard();
  await expect(dialog.getByRole("region", { name: "Listing preview", exact: true })).toContainText(
    "Price incomplete",
  );
  await expect(reconcile).toBeEnabled();
  await showFields();
  await expect(dialog.getByLabel("Vehicle price (MYR)", { exact: true })).toBeDisabled();
  await expect(button("Save draft")).toBeDisabled();
  await expect(button("Discard changes")).toBeDisabled();
  const failedRefresh = () =>
    page.waitForResponse(
      (response) => response.url().endsWith(endpoint) && response.status() === 503,
    );
  // An unconfirmed withdrawal locks Refresh; reconciling is the only way to read the result.
  await expect(button("Refresh listing")).toBeDisabled();
  await expect(reconcile).toBeEnabled();
  await button("Publication").click();
  await expect(reason).toHaveValue("  Showroom stock review  ");
  for (let index = 1; index < mismatches.length; index += 1) {
    await reconcile.click();
    await expect.poll(() => attempts.length).toBe(index + 1);
    await expect(reconcile).toBeEnabled();
  }
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Unavailable", uncertain: true },
    }),
  );
  const confirmedRefresh = failedRefresh();
  await reconcile.click();
  await confirmedRefresh;
  await expect(reconcile).toHaveCount(0);
  await expect(dialog.getByText(/^Withdrawn /)).toBeVisible();
  await expect(withdraw).toHaveCount(0);
  await expect(button("Close drawer")).toBeEnabled();
  await page.unroute(`**${endpoint}`);
  await page.unroute(`**${postPath}`);
  expect(new Set(attempts).size).toBe(1);
  expect(attempts).toHaveLength(mismatches.length + 1);
  const captured = JSON.parse(attempts[0]);
  expect(captured).toMatchObject({
    publicationId: target.publication.id,
    reason: "Showroom stock review",
  });
  const persisted = await readWorkspace();
  expect(persisted.withdrawal).toMatchObject({
    publicationId: target.publication.id,
    reason: captured.reason,
  });
  expect(persisted.draft.photoIds).toEqual(saved.draft.photoIds);
  expect((await context.request.get(publicDetail)).status()).toBe(404);
  expect(
    (
      await context.request.get(`/api/vehicles/${item.id}/photos/${saved.draft.photoIds[0]}`)
    ).status(),
  ).toBe(404);
  const publicAfter = (await (await context.request.get(cataloguePath)).json()).data;
  expect(publicAfter.total).toBe(catalogue.total - 1);
  expect(publicAfter.items.some((row: { id: string }) => row.id === item.id)).toBe(false);
  // The retained price edit keeps the drawer locked, so Refresh stays disabled.
  await expect(button("Refresh listing")).toBeDisabled();
  await expect(dialog.getByText(/^Withdrawn /)).toBeVisible();
  await expect(withdraw).toHaveCount(0);
  await showFields();
  await expect(dialog.getByLabel("Vehicle price (MYR)", { exact: true })).toHaveValue("123.456");
  await button("Discard changes").click();
  await confirm("Discard listing changes?", "Discard changes");
  await button("Publication").click();
  await expect(button("Close drawer")).toBeEnabled();
  const republicationAttempts: Record<"drafts" | "publications", string[]> = {
    drafts: [],
    publications: [],
  };
  for (const action of ["drafts", "publications"] as const) {
    await page.route(`**${endpoint}/${action}`, async (route) => {
      const requests = republicationAttempts[action];
      requests.push(route.request().postData()!);
      const response = await route.fetch();
      expect(response.status()).toBe(200);
      await route.fulfill(
        requests.length === 1
          ? {
              status: 503,
              json: { status: 503, data: null, message: "Lost receipt", uncertain: true },
            }
          : { response },
      );
    });
  }
  await button("Republish listing").click();
  await confirm("Republish this listing?", "Republish listing");
  await expect(button("Reconcile republication")).toBeEnabled();
  expect(republicationAttempts.publications).toHaveLength(0);
  const preparedRepublication = await readWorkspace();
  expect(preparedRepublication.draft.version).toBe(persisted.draft.version + 1);
  expect(preparedRepublication.publication).toEqual(persisted.publication);
  await button("Reconcile republication").click();
  await expect.poll(() => republicationAttempts.publications.length).toBe(1);
  await expect(button("Reconcile republication")).toBeEnabled();
  await expect(button("Close drawer")).toBeDisabled();
  await button("Reconcile republication").click();
  await expect(button("Close drawer")).toBeEnabled();
  for (const action of ["drafts", "publications"] as const) {
    expect(republicationAttempts[action]).toHaveLength(2);
    expect(new Set(republicationAttempts[action]).size).toBe(1);
    await page.unroute(`**${endpoint}/${action}`);
  }
  expect(JSON.parse(republicationAttempts.drafts[0])).toMatchObject({
    expectedVersion: persisted.draft.version,
    ...listingDraftContents(listingDraft(persisted.draft)),
  });
  const capturedPublication = JSON.parse(republicationAttempts.publications[0]);
  expect(capturedPublication).toMatchObject({
    revisionId: preparedRepublication.draft.id,
    signedRevisionId: persisted.currentSignedRevisionId,
  });
  const republished = await readWorkspace();
  expect(republished.draft).toEqual(preparedRepublication.draft);
  expect(republished.publication.id).toBe(capturedPublication.operationId);
  expect((await context.request.get(publicDetail)).status()).toBe(200);
  await expect(page.getByRole("table", { name: "Listings", exact: true })).toBeVisible();
  await dialog
    .getByRole("heading", { name: "Saved listing", exact: true })
    .scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-withdrawal.png") });

  const beforeOrigin = counts()[postPath] ?? 0;
  expect(
    (
      await context.request.post(postPath, {
        headers: { ...(await privateHeaders(context)), Origin: "https://outside.invalid" },
        data: captured,
      })
    ).status(),
  ).toBe(403);
  expect(counts()[postPath] ?? 0).toBe(beforeOrigin);
  const denied = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await apiLogin(denied, mobile ? "siti.aminah@example.invalid" : "lim.mei.ling@example.invalid");
  expect(
    (
      await denied.request.post(postPath, {
        headers: await privateHeaders(denied),
        data: { ...captured, operationId: crypto.randomUUID() },
      })
    ).status(),
  ).toBe(404);
  await denied.close();
  await button("Close drawer").click();

  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  const backendHeaders = { Authorization: `Bearer ${cookie.value}` };
  const listingsPath = `${process.env.API_BASE_URL}/api/staff/listings?branchId=${branch}&pageSize=50`;
  const listings = await (
    await context.request.get(listingsPath, { headers: backendHeaders })
  ).json();
  const hold = (listings.items as { vehicleId: string; isReserved: boolean }[]).find(
    (item) => item.isReserved,
  )!;
  const heldEndpoint = `/api/staff/listings/${hold.vehicleId}`;
  const held = (
    await (
      await context.request.get(heldEndpoint, { headers: await privateHeaders(context) })
    ).json()
  ).data;
  await page.goto(`/dashboard/listings/${hold.vehicleId}/publication?branchId=${branch}`);
  const heldDialog = page.getByRole("dialog", {
    name: held.vehicle.registrationPlate,
    exact: true,
  });
  await heldDialog
    .getByLabel("Withdrawal reason", { exact: true })
    .fill("Withdraw held vehicle for stock review");
  await page.route(`**${heldEndpoint}/withdrawals`, async (route) => {
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    await route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Lost withdrawal", uncertain: true },
    });
  });
  await heldDialog.getByRole("button", { name: "Withdraw listing", exact: true }).click();
  await confirm("Withdraw this listing?", "Withdraw listing");
  await expect(
    heldDialog.getByRole("button", { name: "Reconcile listing withdrawal", exact: true }),
  ).toBeEnabled();
  // Withdrawing a held car cancels its sale, which moves to the managers' sales history.
  const registration = encodeURIComponent(held.vehicle.registrationPlate);
  const closed = await (
    await context.request.get(
      `${process.env.API_BASE_URL}/api/staff/sales?view=history&search=${registration}&pageSize=50`,
      { headers: backendHeaders },
    )
  ).json();
  expect(closed.items).toContainEqual(
    expect.objectContaining({ registration: held.vehicle.registrationPlate, status: "Cancelled" }),
  );
  const sales = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await apiLogin(sales, "aina.sofia@example.invalid");
  const beforeSales = counts()[postPath] ?? 0;
  expect(
    (
      await sales.request.post(postPath, { headers: await privateHeaders(sales), data: captured })
    ).status(),
  ).toBe(403);
  expect(counts()[postPath] ?? 0).toBe(beforeSales);
  await context.addCookies(
    (await sales.cookies()).filter((entry) => entry.name === "apexauto_token"),
  );
  await page.unroute(`**${heldEndpoint}/withdrawals`);
  await heldDialog
    .getByRole("button", { name: "Reconcile listing withdrawal", exact: true })
    .click();
  await expect(heldDialog).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Listing access unavailable", exact: true }),
  ).toBeVisible();
  await sales.close();
  writeFileSync(
    path.join(evidence, `listing-withdrawal-${info.project.name}.json`),
    JSON.stringify({
      initialReads: 1,
      exactAttempts: attempts,
      publication: target.publication.id,
      withdrawal: persisted.withdrawal,
      republicationAttempts,
      cancelledSale: held.vehicle.registrationPlate,
      role: mobile ? "BranchManager" : "GeneralManager",
    }),
  );
});
