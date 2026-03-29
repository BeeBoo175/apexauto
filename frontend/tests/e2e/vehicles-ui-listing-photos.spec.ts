import { expect, test, type Page, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

async function login(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  const signIn = page.getByRole("button", { name: "Sign in", exact: true });
  const response = page.waitForResponse(
    (entry) => entry.url().endsWith("/api/auth/login") && entry.request().method() === "POST",
  );
  await signIn.click();
  const result = await response;
  if (result.status() === 429) {
    const retrySeconds = Number(result.headers()["retry-after"]);
    expect(retrySeconds).toBeGreaterThan(0);
    expect(retrySeconds).toBeLessThanOrEqual(60);
    // The compressed journey shares the real ingress budget; respect its advertised cooldown.
    await new Promise((resolve) => setTimeout(resolve, retrySeconds * 1000));
    await signIn.click();
  }
  await page.waitForURL(/\/(?:dashboard\/intake|auth\/continue)$/);
  if (new URL(page.url()).pathname === "/auth/continue") {
    const recovery = page.getByRole("button", { name: "Try checking session again", exact: true });
    await expect(recovery).toBeVisible();
    const probe = await page.context().request.get(`${process.env.API_BASE_URL}/api/auth/session`, {
      headers: { Authorization: `Bearer ${await token(page.context())}` },
    });
    if (probe.status() === 429) {
      const retrySeconds = Number(probe.headers()["retry-after"]);
      expect(retrySeconds).toBeGreaterThan(0);
      expect(retrySeconds).toBeLessThanOrEqual(60);
      await new Promise((resolve) => setTimeout(resolve, retrySeconds * 1000));
    } else {
      expect(probe.status()).toBe(200);
    }
    await recovery.click();
  }
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
}

async function token(context: BrowserContext) {
  return (await context.cookies()).find((entry) => entry.name === "apexauto_token")!.value;
}

async function headers(context: BrowserContext) {
  return { Cookie: `apexauto_token=${await token(context)}` };
}

test("manager selects and reorders photos across pages in the same recoverable listing draft", async ({
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
  const photoPath = `${endpoint}/photos`;
  const postPath = `${endpoint}/drafts`;
  const saved = (
    await (await context.request.get(endpoint, { headers: await headers(context) })).json()
  ).data;
  const bearer = await token(context);
  const uploaded: string[] = [];
  for (let index = 0; index < 8; index += 1) {
    const id = crypto.randomUUID();
    const response = await context.request.put(`${process.env.API_BASE_URL}${photoPath}/${id}`, {
      headers: { Authorization: `Bearer ${bearer}`, "Content-Type": "image/jpeg" },
      data: readFileSync(path.join(evidence, "upload.jpg")),
    });
    expect(response.status()).toBe(200);
    uploaded.push(id);
  }
  const photoResponse = await context.request.get(`${photoPath}?page=1`, {
    headers: await headers(context),
  });
  expect(photoResponse.status()).toBe(200);
  const firstPage = (await photoResponse.json()).data;
  expect(firstPage).toMatchObject({ page: 1, pageSize: 10, total: 12 });
  const before = counts()[photoPath] ?? 0;
  await page.goto(`/dashboard/management?branchId=${branch}&listing=${item.id}`);
  const dialog = page.getByRole("dialog", { name: saved.vehicle.registrationPlate, exact: true });
  await expect(dialog.getByRole("button", { name: "Edit photos", exact: true })).toBeEnabled();
  expect(counts()[photoPath] ?? 0).toBe(before);
  await dialog.getByRole("button", { name: "Edit photos", exact: true }).click();
  const choices = dialog.getByRole("region", { name: "Uploaded photo choices", exact: true });
  const selected = dialog.getByRole("region", { name: "Selected listing photos", exact: true });
  await expect(
    choices.getByText("Page 1 of 2 · 12 uploaded photos", { exact: true }),
  ).toBeVisible();
  expect((counts()[photoPath] ?? 0) - before).toBe(1);
  await selected.getByRole("button", { name: "Remove photo 1", exact: true }).click();
  await selected.getByRole("button", { name: "Move photo 2 earlier", exact: true }).click();
  await choices.getByRole("button", { name: "Select uploaded photo 1", exact: true }).click();
  await choices.getByRole("button", { name: "Select uploaded photo 2", exact: true }).click();
  const expected = [
    saved.draft.photoIds[2],
    saved.draft.photoIds[1],
    saved.draft.photoIds[3],
    firstPage.items[0].id,
    firstPage.items[1].id,
  ];
  const sources = () =>
    selected
      .locator("img")
      .evaluateAll((images) => images.map((image) => image.getAttribute("src")!.split("/").pop()));
  expect(await sources()).toEqual(expected);
  await expect(
    dialog.getByRole("button", { name: "Back to listings", exact: true }),
  ).toBeDisabled();
  await page.route(`**${photoPath}?page=2`, (route) =>
    route.fulfill({
      status: 503,
      json: { status: 503, data: null, message: "Unavailable", uncertain: true },
    }),
  );
  await choices.getByRole("button", { name: "Next photos", exact: true }).click();
  await expect(
    choices.getByRole("button", { name: "Retry uploaded photos", exact: true }),
  ).toBeEnabled();
  await expect(
    choices.getByText("Page 1 of 2 · 12 uploaded photos", { exact: true }),
  ).toBeVisible();
  expect(await sources()).toEqual(expected);
  await page.unroute(`**${photoPath}?page=2`);
  await choices.getByRole("button", { name: "Retry uploaded photos", exact: true }).click();
  await expect(
    choices.getByText("Page 2 of 2 · 12 uploaded photos", { exact: true }),
  ).toBeVisible();
  expect(await sources()).toEqual(expected);
  await dialog.getByRole("button", { name: "Edit description", exact: true }).click();
  const description = `Manager reviewed photography for ${info.project.name}.`;
  await dialog.getByLabel("Public description", { exact: true }).fill(description);
  await dialog.getByRole("button", { name: "Publication", exact: true }).click();
  await dialog.getByRole("button", { name: "Listing page", exact: true }).click();
  expect(await sources()).toEqual(expected);
  await dialog.getByRole("button", { name: "Edit photos", exact: true }).click();
  await expect(
    choices.getByText("Page 2 of 2 · 12 uploaded photos", { exact: true }),
  ).toBeVisible();
  await page.route(`**${photoPath}?page=2`, async (route) => {
    const response = await route.fetch();
    const body = await response.json();
    await route.fulfill({ response, json: { ...body, data: { ...body.data, page: 1 } } });
  });
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(
    choices.getByRole("button", { name: "Retry uploaded photos", exact: true }),
  ).toBeEnabled();
  expect(await sources()).toEqual(expected);
  await page.unroute(`**${photoPath}?page=2`);
  await choices.getByRole("button", { name: "Retry uploaded photos", exact: true }).click();
  await expect(choices.getByRole("button", { name: "Previous photos", exact: true })).toBeEnabled();
  const attempts: string[] = [];
  await page.route(`**${postPath}`, async (route) => {
    attempts.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    const body = await response.json();
    await route.fulfill(
      attempts.length === 1
        ? {
            response,
            json: { ...body, data: { ...body.data, photoIds: [...body.data.photoIds].reverse() } },
          }
        : { response },
    );
  });
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(
    dialog.getByRole("button", { name: "Reconcile draft save", exact: true }),
  ).toBeEnabled();
  await expect(
    selected.getByRole("button", { name: "Remove photo 1", exact: true }),
  ).toBeDisabled();
  await expect(dialog.getByRole("button", { name: "Discard changes", exact: true })).toBeDisabled();
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  expect(await sources()).toEqual(expected);
  await dialog.getByRole("button", { name: "Reconcile draft save", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Back to listings", exact: true })).toBeEnabled();
  expect(attempts).toHaveLength(2);
  expect(attempts[0]).toBe(attempts[1]);
  expect(JSON.parse(attempts[0]).photoIds).toEqual(expected);
  await page.unroute(`**${postPath}`);
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
  await expect(selected.locator("figure").first()).toContainText("Cover photo");
  await selected.locator("figure").first().scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "listing-photo-selection.png") });
  await selected.getByRole("button", { name: "Remove photo 1", exact: true }).click();
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  expect(await sources()).toEqual(expected.slice(1));
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  expect(await sources()).toEqual(expected);
  await selected.getByRole("button", { name: "Move photo 2 earlier", exact: true }).click();
  const outside = await context.request.post(postPath, {
    headers: { ...(await headers(context)), Origin: process.env.APP_BASE_URL! },
    data: {
      ...JSON.parse(attempts[0]),
      operationId: crypto.randomUUID(),
      expectedVersion: current.draft.version,
      photoIds: [expected[4]],
    },
  });
  expect(outside.status()).toBe(200);
  await dialog.getByRole("button", { name: "Refresh listing", exact: true }).click();
  await expect(dialog.getByText("Newer saved draft available", { exact: true })).toBeVisible();
  expect(await sources()).toEqual([expected[1], expected[0], ...expected.slice(2)]);
  await dialog.getByRole("button", { name: "Discard changes", exact: true }).click();
  expect(await sources()).toEqual([expected[4]]);
  const managerContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const managerPage = await managerContext.newPage();
  await login(managerPage, "lim.mei.ling@example.invalid");
  expect(
    (
      await managerContext.request.get(`${photoPath}?page=1`, {
        headers: await headers(managerContext),
      })
    ).status(),
  ).toBe(404);
  await managerContext.close();
  const salesContext = await browser.newContext({
    ...info.project.use,
    baseURL: process.env.APP_BASE_URL,
  });
  const salesPage = await salesContext.newPage();
  await login(salesPage, "aina.sofia@example.invalid");
  const deniedBefore = counts()[photoPath] ?? 0;
  expect(
    (
      await salesContext.request.get(`${photoPath}?page=1`, {
        headers: await headers(salesContext),
      })
    ).status(),
  ).toBe(403);
  expect(counts()[photoPath] ?? 0).toBe(deniedBefore);
  await context.addCookies(
    (await salesContext.cookies()).filter((cookie) => cookie.name === "apexauto_token"),
  );
  await choices.getByRole("button", { name: "Previous photos", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "Listing access unavailable", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Selected listing photos", exact: true }),
  ).toHaveCount(0);
  await salesContext.close();
  writeFileSync(
    path.join(evidence, `listing-photos-${info.project.name}.json`),
    JSON.stringify({
      uploaded,
      photoOrder: expected,
      exactAttempts: attempts,
      selectionRetainedAcrossPages: true,
      failedReadRetained: true,
      malformedPageRetained: true,
      approvedRevisionRetained: true,
      staleSelectionRetained: true,
      actorChoicesCleared: true,
    }),
  );
});
