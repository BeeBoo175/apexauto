import { randomInt, randomUUID } from "node:crypto";
import { expect, test, type BrowserContext, type Page } from "@playwright/test";

const origin = process.env.APP_BASE_URL!;
const password = "Password123!";
const confirmLabel = "Close my account permanently";

async function registerAndSignIn(page: Page, context: BrowserContext) {
  const email = `closure-adversarial-${randomUUID()}@example.invalid`;
  const registered = await context.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: {
      registrationKey: randomUUID(),
      legalName: "Farah Binti Karim",
      username: `farah.karim.${randomInt(100000, 999999)}`,
      email,
      password,
      nric: `910720${randomInt(100000, 999999)}`,
      mobile: "+60123456780",
      residentialAddress: "7 Jalan Seri Utama, Shah Alam, Selangor",
    },
  });
  expect(registered.status()).toBe(200);
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
  await page.goto("/account/details");
}

async function signedIn(context: BrowserContext) {
  return (await context.cookies()).some((cookie) => cookie.name === "apexauto_token");
}

// The login budget is five requests a minute per address and closure confirmations share it, so the two journeys
// keep to a handful of confirmations each and the second waits out the window.
test.describe.configure({ mode: "serial" });

test("awkward input and a double click never close the account twice or sign the browser out early", async ({
  page,
  context,
}, info) => {
  test.skip(info.project.name === "mobile", "The mobile project covers the dialog layout.");
  test.setTimeout(120_000);
  await registerAndSignIn(page, context);
  const confirmations: string[] = [];
  page.on("request", (request) => {
    if (request.url().endsWith("/api/account/closure") && request.method() === "POST") {
      confirmations.push(request.postData() ?? "");
    }
  });

  // Closing the dialog with Escape or Not now never submits anything.
  await page.getByRole("button", { name: "Close account" }).click();
  await expect(page.getByRole("dialog").getByText("This cannot be undone.")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page.getByRole("button", { name: "Close account" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Not now" }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(confirmations).toHaveLength(0);

  // A password pasted with surrounding whitespace is a wrong password, and a wrong password keeps the session.
  await page.getByRole("button", { name: "Close account" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Your password").fill(`  ${password}  `);
  await dialog.getByRole("button", { name: confirmLabel }).click();
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await expect.poll(() => confirmations.length).toBe(1);
  expect(await signedIn(context)).toBe(true);

  // Editing the password after the refusal is a new attempt with a new operation id, and a double click is one request.
  await dialog.getByLabel("Your password").fill(password);
  await dialog.getByRole("button", { name: confirmLabel }).dblclick();
  await expect(dialog.getByText("Account closed")).toBeVisible();
  expect(confirmations).toHaveLength(2);
  const first = JSON.parse(confirmations[0]) as { operationId: string };
  const second = JSON.parse(confirmations[1]) as { operationId: string };
  expect(second.operationId).not.toBe(first.operationId);

  // Back after the closure never returns to a private page.
  await dialog.getByRole("button", { name: "Done" }).click();
  await expect(page).toHaveURL(`${origin}/`);
  await page.goBack();
  await expect(page.getByRole("button", { name: "Close account" })).toHaveCount(0);
});

test("a lost response is reconciled by replay and offline attempts keep the session", async ({
  page,
  context,
}, info) => {
  test.skip(info.project.name === "mobile", "The mobile project covers the dialog layout.");
  test.setTimeout(180_000);
  await page.waitForTimeout(62_000);
  await registerAndSignIn(page, context);
  await page.getByRole("button", { name: "Close account" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await dialog.getByLabel("Your password").fill(password);

  // Offline, the browser cannot tell whether the request arrived, so the confirmation is held as unconfirmed with its
  // captured body; the session stays signed in and nothing is reported as closed.
  await context.setOffline(true);
  await dialog.getByRole("button", { name: confirmLabel }).click();
  await expect(dialog.getByRole("button", { name: "Confirm again" })).toBeVisible();
  await context.setOffline(false);
  expect(await signedIn(context)).toBe(true);
  await expect(dialog.getByText("Account closed")).toHaveCount(0);

  // The server applies the closure but the browser never sees the answer.
  await page.route("**/api/account/closure", async (route) => {
    if (route.request().method() !== "POST") {
      await route.continue();
      return;
    }
    await route.fetch();
    await route.abort("failed");
  });
  await dialog.getByRole("button", { name: "Confirm again" }).click();
  await expect(dialog.getByRole("button", { name: "Confirm again" })).toBeVisible();
  await expect(dialog.getByText("Account closed")).toHaveCount(0);
  // The password cannot be changed while the result is unknown.
  await expect(dialog.getByLabel("Your password")).toBeDisabled();

  // Replaying the captured body meets the closed session and reports the closure as recorded.
  await page.unroute("**/api/account/closure");
  await dialog.getByRole("button", { name: "Confirm again" }).click();
  await expect(dialog.getByText("Account closed")).toBeVisible();
  await expect(dialog.getByText(/erasure is recorded/)).toBeVisible();
});
