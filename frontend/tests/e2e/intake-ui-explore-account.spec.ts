import { randomInt, randomUUID } from "node:crypto";
import { writeFileSync } from "node:fs";
import path from "node:path";
import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { expectClean, expectNoSidewaysScroll, guardPage } from "./explore-guards";
import { apiLogin, privateHeaders } from "./staff-session";

const origin = process.env.APP_BASE_URL!;
const password = "Password123!";
const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const rawCodes =
  /customer-has-reservation|customer-has-|invalid-username|username-taken|operation-/i;

test.use({ actionTimeout: 15000 });

// Registers a fresh customer and signs the browser context in, so scenarios never share state.
async function signedInCustomer(context: BrowserContext) {
  const email = `account-${randomUUID()}@example.invalid`;
  const username = `acc.${randomInt(100000, 999999)}`;
  const registered = await context.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: {
      registrationKey: randomUUID(),
      legalName: "Aminah Binti Rashid",
      username,
      email,
      password,
      nric: `920315${randomInt(100000, 999999)}`,
      mobile: "+60123456781",
      residentialAddress: "12 Jalan Bunga Raya, Petaling Jaya, Selangor",
    },
  });
  expect(registered.status()).toBe(200);
  await apiLogin(context, email);
  return { email, username };
}

async function hasSessionCookie(context: BrowserContext) {
  return (await context.cookies()).some((cookie) => cookie.name === "apexauto_token");
}

async function expectNoRawCodes(page: Page) {
  await expect(page.locator("body")).not.toContainText(rawCodes);
}

function writeUploadFiles() {
  const jpeg = path.join(evidence, "upload.jpg");
  const bad = path.join(evidence, "not-a-picture.txt");
  writeFileSync(bad, "this is plain text, not an image");
  return { jpeg, bad };
}

async function openUsernameDialog(page: Page) {
  await page.getByRole("button", { name: "Change username" }).click();
  return page.getByRole("dialog", { name: "Change username" });
}

async function trySaveUsername(page: Page, value: string) {
  const dialog = page.getByRole("dialog", { name: "Change username" });
  await dialog.getByLabel("Username", { exact: true }).fill(value);
  await dialog.getByRole("button", { name: "Save username" }).click();
}

test("ACC-01 every account section loads at its own address and Back moves between them", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  await signedInCustomer(context);

  const sections = [
    { label: "Reservations", path: "/account" },
    { label: "Selling appointments", path: "/account/appointments" },
    { label: "Test drives", path: "/account/test-drives" },
    { label: "Account details", path: "/account/details" },
  ];

  await page.goto("/account");
  await expect(page.getByRole("heading", { name: "Your account", level: 1 })).toBeVisible();
  const tabs = page.getByRole("navigation", { name: "Account sections" });

  for (const section of sections) {
    await tabs.getByRole("link", { name: section.label, exact: true }).click();
    await expect(page).toHaveURL(`${origin}${section.path}`);
    await expect(tabs.getByRole("link", { name: section.label, exact: true })).toHaveAttribute(
      "aria-current",
      "location",
    );
    await expectNoSidewaysScroll(page);
  }
  await expect(page.getByRole("heading", { name: "Account details" })).toBeVisible();

  await page.goBack();
  await expect(page).toHaveURL(`${origin}/account/test-drives`);
  await page.goBack();
  await expect(page).toHaveURL(`${origin}/account/appointments`);
  await page.goBack();
  await expect(page).toHaveURL(`${origin}/account`);
  await expect(tabs.getByRole("link", { name: "Reservations", exact: true })).toHaveAttribute(
    "aria-current",
    "location",
  );

  for (const section of sections.slice(1)) {
    await page.goto(section.path);
    await expect(page.getByRole("heading", { name: "Your account", level: 1 })).toBeVisible();
  }
  await expectNoRawCodes(page);
  expectClean(guard);
});

test("ACC-02 unknown sections and another customer's sale documents are not found", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  await signedInCustomer(context);

  const nonsense = await page.goto("/account/nonsense");
  expect(nonsense?.status()).toBe(404);
  await expect(page.getByText("We can't find that page")).toBeVisible();

  const nested = await page.goto("/account/details/extra");
  expect(nested?.status()).toBe(404);

  const stranger = await page.goto(`/account/sales/${randomUUID()}/documents`);
  expect(stranger?.status()).toBe(404);
  await expect(page.getByText("We can't find that page")).toBeVisible();
  await expect(page.getByRole("heading", { name: /sale documents/i })).toHaveCount(0);

  const malformed = await page.goto("/account/sales/not-a-uuid/documents");
  expect(malformed?.status()).toBe(404);

  await expectNoSidewaysScroll(page);
  await expectNoRawCodes(page);
  // The browser logs the 404 answers it was given on purpose; only real faults fail the guard.
  expectClean(guard, [/status of 404/]);
});

test("ACC-03 username changes are refused with a message until a valid new one sticks", async ({
  page,
  context,
}) => {
  test.setTimeout(90000);
  const guard = guardPage(page);
  const { username } = await signedInCustomer(context);
  await page.goto("/account/details");
  await expect(page.getByText(username, { exact: true })).toBeVisible();

  const dialog = await openUsernameDialog(page);
  const problem = dialog.getByRole("alert");

  // A username already used by a seeded customer.
  await trySaveUsername(page, "ahmad.faizal");
  await expect(problem).toBeVisible();
  await expect(problem).not.toBeEmpty();
  await expect(dialog).toBeVisible();

  await trySaveUsername(page, "");
  await expect(problem).toContainText("Use 3 to 30");

  await trySaveUsername(page, "   ");
  await expect(problem).toContainText("Use 3 to 30");

  // The field itself caps typing at 30 characters, so an oversized value never reaches a save.
  await dialog.getByLabel("Username", { exact: true }).fill("x".repeat(200));
  await expect(dialog.getByLabel("Username", { exact: true })).toHaveValue(/^x{1,30}$/);
  const oversized = await context.request.put("/api/account/username", {
    headers: { ...(await privateHeaders(context)), "Content-Type": "application/json" },
    data: { operationId: randomUUID(), username: "x".repeat(200) },
  });
  expect(oversized.status()).toBeGreaterThanOrEqual(400);
  expect(oversized.status()).toBeLessThan(500);

  await trySaveUsername(page, "<b>x</b>");
  await expect(problem).toContainText("Use 3 to 30");
  await expectNoRawCodes(page);

  // Nothing changed behind the open dialog.
  await dialog.getByRole("button", { name: "Not now" }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText(username, { exact: true })).toBeVisible();

  const fresh = `renamed.${randomInt(100000, 999999)}`;
  await openUsernameDialog(page);
  await trySaveUsername(page, fresh);
  await expect(page.getByText("Username updated.")).toBeVisible();
  await expect(page.getByText(fresh, { exact: true })).toBeVisible();

  await page.reload();
  await expect(page.getByText(fresh, { exact: true })).toBeVisible();
  await expectNoSidewaysScroll(page);
  expectClean(guard, [/status of 4\d\d/]);
});

test("ACC-04 a bad picture is refused, a valid one shows, and removing twice is harmless", async ({
  page,
  context,
}) => {
  test.setTimeout(90000);
  const guard = guardPage(page);
  await signedInCustomer(context);
  const files = writeUploadFiles();
  await page.goto("/account/details");

  const panel = page.getByRole("region", { name: "Account details" });
  const pictureImage = panel.locator("img[src^='/api/accounts/']");
  const chooser = page.getByLabel("Choose a profile picture");

  await chooser.setInputFiles(files.bad);
  await expect(
    page.getByRole("alert").filter({ hasText: "Choose a JPEG, PNG or WebP" }),
  ).toBeVisible();
  await expect(pictureImage).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Remove picture" })).toHaveCount(0);

  await chooser.setInputFiles(files.jpeg);
  await expect(page.getByText("Picture updated.")).toBeVisible();
  await expect(pictureImage).toBeVisible();
  const source = (await pictureImage.getAttribute("src"))!;
  await expect
    .poll(() => pictureImage.evaluate((image) => (image as HTMLImageElement).naturalWidth))
    .toBeGreaterThan(0);

  await page.reload();
  await expect(pictureImage).toBeVisible();

  await page.getByRole("button", { name: "Remove picture" }).click();
  await expect(page.getByText("Picture removed.")).toBeVisible();
  await expect(pictureImage).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Remove picture" })).toHaveCount(0);

  // A second removal of the same picture, sent directly, must not fault or restore anything.
  const headers = await privateHeaders(context);
  const again = await context.request.delete(source.split("?")[0], { headers });
  expect(again.status()).toBeLessThan(500);
  await page.reload();
  await expect(pictureImage).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Add picture" })).toBeVisible();

  await expectNoSidewaysScroll(page);
  await expectNoRawCodes(page);
  expectClean(guard, [/status of 4\d\d/]);
});

test("ACC-05 closing is cancelled by Escape and Not now, and a wrong password keeps the session", async ({
  page,
  context,
}) => {
  test.setTimeout(90000);
  const guard = guardPage(page);
  await signedInCustomer(context);
  await page.goto("/account/details");

  const open = page.getByRole("button", { name: "Close account" });
  const dialog = page.getByRole("dialog", { name: "Close your account" });

  await open.click();
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(`${origin}/account/details`);

  await open.click();
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await dialog.getByRole("button", { name: "Not now" }).click();
  await expect(dialog).toBeHidden();
  expect(await hasSessionCookie(context)).toBe(true);

  await open.click();
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await dialog.getByLabel("Your password").fill("Wrong-password-1");
  await dialog.getByRole("button", { name: "Close my account permanently" }).click();
  // The refusal itself must appear before the session is checked.
  const refusal = page.getByRole("alert").first();
  await expect(refusal).toBeVisible();
  await expect(refusal).not.toBeEmpty();
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await expect(dialog.getByText("Account closed")).toHaveCount(0);
  expect(await hasSessionCookie(context)).toBe(true);
  await expectNoRawCodes(page);

  await dialog.getByRole("button", { name: "Not now" }).click();
  await page.reload();
  await expect(page.getByRole("heading", { name: "Your account", level: 1 })).toBeVisible();
  await expectNoSidewaysScroll(page);
  expectClean(guard, [/status of 4\d\d/]);
});

test("ACC-06 the closure dialog does not promise removal of details it keeps", async ({
  page,
  context,
}) => {
  test.fail(
    true,
    "PB-05: the dialog says name, NRIC and address are removed while the retained list keeps them",
  );
  const guard = guardPage(page);
  await signedInCustomer(context);
  await page.goto("/account/details");

  await page.getByRole("button", { name: "Close account" }).click();
  const dialog = page.getByRole("dialog", { name: "Close your account" });
  const promise = dialog.locator("p").filter({ hasText: "This cannot be undone." });
  await expect(promise).toBeVisible();

  const wording = (await promise.innerText()).replace(/\s+/g, " ");
  expect(wording).not.toMatch(/\b(name|NRIC|address)\b[^.]*\bremoved\b/i);
  expectClean(guard);
});

test("ACC-07 a customer with an active reservation is blocked with how to resolve it", async ({
  page,
  context,
}) => {
  test.setTimeout(90000);
  const guard = guardPage(page);
  // The seeded verified customer keeps an active reservation in the canonical data.
  await apiLogin(context, "ahmad.faizal@example.invalid");
  await page.goto("/account/details");

  await page.getByRole("button", { name: "Close account" }).click();
  const dialog = page.getByRole("dialog", { name: "Close your account" });
  await expect(dialog.getByText(/Closing is blocked/)).toBeVisible();
  const blocker = dialog.locator("li").first();
  await expect(blocker).toBeVisible();
  test.info().annotations.push({
    type: "blocker",
    description: (await blocker.innerText()).replace(/\s+/g, " "),
  });
  await expect(blocker).toContainText(/Cancel|Pay the|Ask the|Claim|Close again/);
  await expect(dialog.getByLabel("Your password")).toHaveCount(0);
  await expect(dialog.getByRole("button", { name: "Close my account permanently" })).toHaveCount(0);
  await expectNoRawCodes(page);

  await dialog.getByRole("button", { name: "Not now" }).click();
  await expect(dialog).toBeHidden();
  expect(await hasSessionCookie(context)).toBe(true);
  await expectNoSidewaysScroll(page);
  expectClean(guard);
});
