import { expect, test, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { listingDraft, listingDraftContents } from "../../src/lib/vehicles/listing-draft";

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
  await login(context, mobile ? "lim.mei.ling@example.invalid" : "farah.aziz@example.invalid");
  const cataloguePath = `/api/vehicles?branchId=${branch}&page=1`;
  const catalogue = (await (await context.request.get(cataloguePath)).json()).data;
  const item = catalogue.items[mobile ? 1 : 0];
  const publicDetail = `${process.env.API_BASE_URL}/api/vehicles/${item.id}`;
  expect((await context.request.get(publicDetail)).status()).toBe(200);
  const endpoint = `/api/staff/listings/${item.id}`;
  const postPath = `${endpoint}/withdrawals`;
  const readWorkspace = async () =>
    (await (await context.request.get(endpoint, { headers: await headers(context) })).json()).data;
  const saved = await readWorkspace();
  // Gallery recovery has separate acceptance; keep command checks within the unchanged request budget.
  await page.route(/\/api\/staff\/listings\/[^/]+\/photos\/[^/?]+$/, (route) =>
    route.fulfill({ status: 503, body: "" }),
  );
  const initialReads = counts()[endpoint] ?? 0;
  const entry = await page.goto(
    `/dashboard/management?branchId=${branch}&listing=${item.id}&section=publication`,
  );
  expect(await entry!.text()).not.toContain("Withdraw approved listing");
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  const button = (name: string) => dialog.getByRole("button", { name, exact: true });
  const reason = dialog.getByLabel("Withdrawal reason", { exact: true });
  const withdraw = button("Withdraw listing");
  const reconcile = button("Reconcile listing withdrawal");
  await expect(withdraw).toBeEnabled();
  expect((counts()[endpoint] ?? 0) - initialReads).toBe(1);
  const beforeValidation = counts()[postPath] ?? 0;
  await withdraw.click();
  await expect(reason).toHaveAttribute("aria-invalid", "true");
  await reason.fill("   ");
  await withdraw.click();
  await reason.fill("Showroom\nreview");
  await withdraw.click();
  expect(counts()[postPath] ?? 0).toBe(beforeValidation);
  await button("Discard withdrawal reason").click();
  await reason.fill("Reason retained while discarding listing edits");
  await button("Listing page").click();
  await button("Edit price").click();
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("123.456");
  await button("Discard changes").click();
  await button("Publication").click();
  await expect(reason).toHaveValue("Reason retained while discarding listing edits");
  await expect(button("Back to listings")).toBeDisabled();
  await button("Discard withdrawal reason").click();

  await button("Listing page").click();
  await button("Edit photos").click();
  await dialog
    .getByLabel("New listing photo", { exact: true })
    .setInputFiles(path.join(evidence, "upload.jpg"));
  await button("Publication").click();
  await expect(withdraw).toBeDisabled();
  await button("Listing page").click();
  await button("Discard selected photo").click();
  await button("Edit price").click();
  await dialog.getByLabel("Vehicle price (MYR)", { exact: true }).fill("45000.01");
  const savePath = `${endpoint}/drafts`;
  await page.route(`**${savePath}`, async (route) => {
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    await route.fulfill({
      status: 503,
      json: { status: 503, data: null, uncertain: true, message: "Lost save" },
    });
  });
  await button("Save draft").click();
  await expect(button("Reconcile draft save")).toBeEnabled();
  await button("Publication").click();
  await expect(withdraw).toBeDisabled();
  await page.unroute(`**${savePath}`);
  await button("Reconcile draft save").click();
  await expect(button("Approve publication")).toBeEnabled();
  const publicationPath = `${endpoint}/publications`;
  await page.route(`**${publicationPath}`, async (route) => {
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    await route.fulfill({
      status: 503,
      json: { status: 503, data: null, uncertain: true, message: "Lost approval" },
    });
  });
  await button("Approve publication").click();
  await expect(button("Reconcile publication approval")).toBeEnabled();
  await expect(withdraw).toBeDisabled();
  await page.unroute(`**${publicationPath}`);
  await button("Reconcile publication approval").click();
  await expect(button("Refresh listing")).toBeEnabled();
  await expect(withdraw).toBeEnabled();
  const prepared = await readWorkspace();
  await reason.fill("  Showroom stock review  ");
  const outsideDraft = await context.request.post(savePath, {
    headers: await headers(context),
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
        headers: await headers(context),
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
  await conflict;
  await expect(reason).toHaveValue("  Showroom stock review  ");
  await button("Refresh listing").click();
  await expect(
    dialog.getByText(
      "The approved publication changed. Discard this reason before withdrawing another.",
      { exact: true },
    ),
  ).toBeVisible();
  await expect(withdraw).toBeDisabled();
  await button("Discard withdrawal reason").click();
  await expect(withdraw).toBeEnabled();
  const target = await readWorkspace();
  await reason.fill("  Showroom stock review  ");
  await button("Listing page").click();
  await button("Edit price").click();
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
  await expect(reconcile).toBeEnabled();
  await expect(reason).toBeDisabled();
  await expect(button("Save draft")).toBeDisabled();
  await expect(button("Approve publication")).toBeDisabled();
  await expect(button("Discard changes")).toBeDisabled();
  await expect(button("Discard withdrawal reason")).toBeDisabled();
  await expect(button("Back to listings")).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await button("Catalogue card").click();
  await expect(
    dialog.getByRole("region", { name: "Catalogue card preview", exact: true }),
  ).toContainText("123.456");
  await expect(reconcile).toBeEnabled();
  await button("Listing page").click();
  await expect(button("Edit price")).toBeDisabled();
  await expect(button("Edit photos")).toBeDisabled();
  await page.route(`**${endpoint}`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Unavailable", uncertain: true },
    }),
  );
  const failedRefresh = () =>
    page.waitForResponse(
      (response) => response.url().endsWith(endpoint) && response.status() === 503,
    );
  const unconfirmedRefresh = failedRefresh();
  await button("Refresh listing").click();
  await unconfirmedRefresh;
  await expect(reconcile).toBeEnabled();
  await page.unroute(`**${endpoint}`);
  await button("Refresh listing").click();
  await expect(button("Refresh listing")).toBeEnabled();
  await expect(reconcile).toBeEnabled();
  await button("Publication").click();
  await expect(dialog.getByText("Availability", { exact: true }).locator("..")).toContainText(
    "Withdrawn",
  );
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
  await expect(dialog.getByText("Confirmed withdrawal receipt", { exact: true })).toBeVisible();
  await expect(dialog.getByText("Confirmed reason", { exact: true }).locator("..")).toContainText(
    "Showroom stock review",
  );
  await expect(withdraw).toBeDisabled();
  await expect(button("Back to listings")).toBeDisabled();
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
  await button("Refresh listing").click();
  await expect(button("Refresh listing")).toBeEnabled();
  await expect(dialog.getByText("Confirmed withdrawal receipt", { exact: true })).toBeVisible();
  await expect(withdraw).toBeDisabled();
  await button("Listing page").click();
  await button("Edit price").click();
  await expect(dialog.getByLabel("Vehicle price (MYR)", { exact: true })).toHaveValue("123.456");
  await button("Discard changes").click();
  await button("Publication").click();
  await expect(button("Back to listings")).toBeEnabled();
  await expect(page.getByRole("table", { name: "Listings", exact: true })).toBeVisible();
  await dialog
    .getByRole("heading", { name: "Withdraw approved listing", exact: true })
    .scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-withdrawal.png") });

  const beforeOrigin = counts()[postPath] ?? 0;
  expect(
    (
      await context.request.post(postPath, {
        headers: { ...(await headers(context)), Origin: "https://outside.invalid" },
        data: captured,
      })
    ).status(),
  ).toBe(403);
  expect(counts()[postPath] ?? 0).toBe(beforeOrigin);
  const denied = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await login(denied, mobile ? "siti.aminah@example.invalid" : "lim.mei.ling@example.invalid");
  expect(
    (
      await denied.request.post(postPath, {
        headers: await headers(denied),
        data: { ...captured, operationId: crypto.randomUUID() },
      })
    ).status(),
  ).toBe(404);
  await denied.close();
  await button("Back to listings").click();

  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  const backendHeaders = { Authorization: `Bearer ${cookie.value}` };
  const historyPath = `${process.env.API_BASE_URL}/api/staff/reservations?branchId=${branch}`;
  const holds = await (
    await context.request.get(`${historyPath}&isActive=true`, { headers: backendHeaders })
  ).json();
  const hold = holds.items[0];
  expect(hold.isActive).toBe(true);
  const heldEndpoint = `/api/staff/listings/${hold.receipt.vehicleId}`;
  const held = (
    await (await context.request.get(heldEndpoint, { headers: await headers(context) })).json()
  ).data;
  await page.goto(
    `/dashboard/management?branchId=${branch}&listing=${hold.receipt.vehicleId}&section=publication`,
  );
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
  await expect(
    heldDialog.getByRole("button", { name: "Reconcile listing withdrawal", exact: true }),
  ).toBeEnabled();
  const released = await (
    await context.request.get(`${historyPath}&isActive=false`, { headers: backendHeaders })
  ).json();
  const refund = released.items.find(
    (row: { receipt: { id: string } }) => row.receipt.id === hold.receipt.id,
  );
  expect(refund).toMatchObject({
    isActive: false,
    release: { refund: 500, kind: "Withdrawn" },
  });
  const sales = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  await login(sales, "aina.sofia@example.invalid");
  const beforeSales = counts()[postPath] ?? 0;
  expect(
    (
      await sales.request.post(postPath, { headers: await headers(sales), data: captured })
    ).status(),
  ).toBe(403);
  expect(counts()[postPath] ?? 0).toBe(beforeSales);
  await context.addCookies(
    (await sales.cookies()).filter((entry) => entry.name === "apexauto_token"),
  );
  await heldDialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
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
      refund: refund.release,
      role: mobile ? "BranchManager" : "GeneralManager",
    }),
  );
});
