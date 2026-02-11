import { randomUUID, randomInt } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

const evidence = process.env.AUTH_EVIDENCE_DIR!;
const password = "Password123!";
const origin = process.env.APP_BASE_URL!;

function customer(legalName: string) {
  return {
    registrationKey: randomUUID(),
    legalName,
    email: `customer-${randomUUID()}@example.invalid`,
    password,
    nric: `90041514${randomInt(1000, 9999)}`,
    mobile: "+60123456789",
    residentialAddress: "18 Jalan SS 2/24, 47300 Petaling Jaya, Selangor",
  };
}

function counts(): Record<string, number> {
  return JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
}

function fault(mode: string) {
  writeFileSync(path.join(evidence, "fault.txt"), mode);
}

function rememberProfile(registration: ReturnType<typeof customer>) {
  const file = path.join(evidence, "profiles.json");
  const profiles = JSON.parse(readFileSync(file, "utf8"));
  profiles.push({
    RegistrationKey: registration.registrationKey,
    LegalName: registration.legalName,
    Email: registration.email,
    Nric: registration.nric,
  });
  writeFileSync(file, JSON.stringify(profiles));
}

async function assertHydrationGuard(page: Page, html: string) {
  const guarded = await page.evaluate((initialHtml) => {
    const document = new DOMParser().parseFromString(initialHtml, "text/html");
    const form = document.querySelector("form");
    return (
      form?.method === "post" &&
      form.querySelector("fieldset")?.disabled === true &&
      form.querySelector<HTMLButtonElement>("button[type=submit]")?.disabled === true &&
      form.querySelector("noscript")?.textContent?.includes("JavaScript") === true
    );
  }, html);
  expect(guarded, "Credentials and submit remain disabled until hydration").toBe(true);
}

async function signIn(page: Page, email: string) {
  const response = await page.goto("/login?returnTo=https://outside.invalid");
  await assertHydrationGuard(page, await response!.text());
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
}

test.afterEach(() => fault(""));

test("registration, unverified own account, bounded refresh and current-session logout", async ({
  page,
  context,
  browser,
}, testInfo) => {
  test.setTimeout(90_000);
  const owner = customer("Nur Aisyah binti Rahman");
  const other = customer("Lim Wei Jian");
  const signedOut = await context.request.get("/account", { maxRedirects: 0 });
  expect(signedOut.status()).toBe(307);
  expect(signedOut.headers().location).toBe("/login");
  expect(signedOut.headers()["cache-control"]).toContain("no-store");
  expect(signedOut.headers()["x-robots-tag"]).toContain("noindex");
  const registrationPage = await page.goto("/register");
  await assertHydrationGuard(page, await registrationPage!.text());
  await page.screenshot({
    path: path.join(evidence, `${testInfo.project.name}-register-layout.png`),
    fullPage: true,
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  const before = counts();
  await page.getByLabel("Legal name", { exact: true }).fill(owner.legalName);
  await page.getByLabel("Email", { exact: true }).fill(owner.email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByLabel("NRIC", { exact: true }).fill(owner.nric);
  await page.getByLabel("Mobile", { exact: true }).fill(owner.mobile);
  await page.getByLabel("Residential address", { exact: true }).fill(owner.residentialAddress);
  const registrationRequest = page.waitForRequest((request) =>
    request.url().endsWith("/api/auth/register"),
  );
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/login`);
  rememberProfile((await registrationRequest).postDataJSON());
  expect(
    (await context.cookies()).find((cookie) => cookie.name === "apexauto_token"),
  ).toBeUndefined();
  expect(counts()["/api/auth/register"] - (before["/api/auth/register"] ?? 0)).toBe(1);
  await signIn(page, owner.email);
  await expect(page.getByText(owner.legalName, { exact: true })).toBeVisible();
  await expect(page.getByText("Not verified", { exact: true })).toBeVisible();
  await page.screenshot({
    path: path.join(evidence, `${testInfo.project.name}-account.png`),
    fullPage: true,
  });
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  writeFileSync(path.join(evidence, "revoked-token.txt"), cookie.value);
  expect(cookie.httpOnly).toBe(true);
  expect(cookie.sameSite).toBe("Lax");
  expect(cookie.secure).toBe(true);
  expect(await page.evaluate(() => document.cookie)).not.toContain("apexauto_token");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );

  const sameActorTab = await context.newPage();
  const accountDocument = await sameActorTab.goto("/account");
  expect(accountDocument).not.toBeNull();
  const accountHeaders = accountDocument!.headers();
  expect(accountHeaders["cache-control"]).toContain("no-store");
  expect(accountHeaders["x-robots-tag"]).toContain("noindex");
  expect(accountHeaders["x-frame-options"]).toBe("DENY");
  expect(accountHeaders["x-content-type-options"]).toBe("nosniff");
  await expect(sameActorTab.getByText(owner.legalName, { exact: true })).toBeVisible();

  const independent = await browser.newContext({ baseURL: origin });
  const independentPage = await independent.newPage();
  await signIn(independentPage, owner.email);
  expect(
    (await independent.cookies()).find((entry) => entry.name === "apexauto_token")?.value,
  ).not.toBe(cookie.value);
  const registration = await independent.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: other,
  });
  expect(registration.status()).toBe(200);
  rememberProfile(other);
  expect(
    (
      await independent.request.post("/api/auth/register", {
        headers: { Origin: origin },
        data: { ...other, registrationKey: randomUUID() },
      })
    ).status(),
  ).toBe(409);
  const outsider = await browser.newContext({ baseURL: origin });
  const outsiderPage = await outsider.newPage();
  await signIn(outsiderPage, other.email);
  await expect(outsiderPage.getByText(other.legalName, { exact: true })).toBeVisible();
  await expect(outsiderPage.getByText(owner.legalName, { exact: true })).toHaveCount(0);
  const ownerId = JSON.parse(Buffer.from(cookie.value.split(".")[1], "base64url").toString()).sub;
  const scopedRequest = outsiderPage.waitForRequest((request) =>
    request.url().includes("/api/account?userId="),
  );
  // Browser fetch exercises the Secure loopback cookie used by the production build.
  const scoped = await outsiderPage.evaluate(async (id) => {
    const response = await fetch(`/api/account?userId=${id}`, { cache: "no-store" });
    return {
      status: response.status,
      account: await response.json(),
      cacheControl: response.headers.get("cache-control"),
    };
  }, ownerId);
  const scopedHeaders = await (await scopedRequest).allHeaders();
  expect(scopedHeaders.cookie?.includes("apexauto_token=")).toBe(true);
  expect(scoped.status).toBe(200);
  expect(scoped.account.legalName).toBe(other.legalName);
  expect(scoped.cacheControl).toContain("no-store");

  const refreshBefore = counts()["/api/account"];
  await page.getByRole("button", { name: "Refresh account" }).click();
  await expect(page.getByRole("button", { name: "Refresh account" })).toBeEnabled();
  expect(counts()["/api/account"] - refreshBefore).toBe(1);
  fault("unavailable");
  await page.getByRole("button", { name: "Refresh account" }).click();
  await expect(page.getByText(/last confirmed details/)).toBeVisible();
  await expect(page.getByText(owner.legalName, { exact: true })).toBeVisible();
  await expect(page.getByRole("alert").filter({ hasText: "last confirmed details" })).toHaveCount(
    1,
  );
  const retry = page.getByRole("button", { name: "Retry account refresh" });
  fault("");
  await retry.click();
  await expect(page.getByRole("button", { name: "Refresh account" })).toBeEnabled();
  fault("unavailable");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(page.getByRole("button", { name: "Retry sign out" })).toBeVisible();
  expect((await context.cookies()).find((entry) => entry.name === "apexauto_token")?.value).toBe(
    cookie.value,
  );
  fault("");
  await page.getByRole("button", { name: "Retry sign out" }).click();
  await expect(page).toHaveURL(`${origin}/login`);
  await expect(sameActorTab).toHaveURL(/\/login$/);
  await expect(sameActorTab.getByText(owner.legalName, { exact: true })).toHaveCount(0);
  expect(
    (await context.cookies()).find((entry) => entry.name === "apexauto_token"),
  ).toBeUndefined();
  const revoked = await context.request.get("/api/account", {
    headers: { Cookie: `apexauto_token=${cookie.value}` },
  });
  expect(revoked.status()).toBe(401);
  const revokedDocument = await context.request.get("/account", {
    headers: { Cookie: `apexauto_token=${cookie.value}` },
    maxRedirects: 0,
  });
  expect(revokedDocument.status()).toBe(307);
  expect(revokedDocument.headers().location).toBe("/login");
  const independentAccountStatus = await independentPage.evaluate(
    async () => (await fetch("/api/account", { cache: "no-store" })).status,
  );
  expect(independentAccountStatus).toBe(200);
  await page.goto("/account");
  await expect(page).toHaveURL(/\/login$/);
  await independent.close();
  await outsider.close();
});

test("lost registration receipt replays the same persisted request without dropping the draft", async ({
  page,
}) => {
  const owner = customer("Kavitha a/p Subramaniam");
  await page.goto("/register");
  for (const [label, value] of Object.entries({
    "Legal name": owner.legalName,
    Email: owner.email,
    Password: password,
    NRIC: owner.nric,
    Mobile: owner.mobile,
    "Residential address": owner.residentialAddress,
  })) {
    await page.getByLabel(label, { exact: true }).fill(value);
  }

  const bodies: string[] = [];
  await page.route("**/api/auth/register", async (route) => {
    bodies.push(route.request().postData()!);
    await route.fetch();
    await route.abort();
  });
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page.getByRole("button", { name: "Retry same request" })).toBeVisible();
  await expect(page.getByLabel("Legal name", { exact: true })).toHaveValue(owner.legalName);
  await expect(page.getByLabel("Email", { exact: true })).toBeDisabled();
  await page.unroute("**/api/auth/register");
  page.on("request", (request) => {
    if (request.url().endsWith("/api/auth/register")) {
      bodies.push(request.postData()!);
    }
  });
  await page.getByRole("button", { name: "Retry same request" }).click();
  await expect(page).toHaveURL(`${origin}/login`);
  expect(bodies[1]).toBe(bodies[0]);
  rememberProfile(JSON.parse(bodies[0]));
});
