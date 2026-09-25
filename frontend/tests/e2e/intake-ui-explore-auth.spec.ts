import { randomInt, randomUUID } from "node:crypto";
import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { expectClean, guardPage } from "./explore-guards";
import { signIn, signInAs } from "./staff-session";

const origin = process.env.APP_BASE_URL!;
const password = "Password123!";
// The browser logs every 4xx answer as a console error; refusals are deliberate in these scenarios.
const refusalLogs = [/Failed to load resource/];

type Registration = {
  registrationKey: string;
  legalName: string;
  username: string;
  email: string;
  password: string;
  nric: string;
  mobile: string;
  residentialAddress: string;
};

function newRegistration(overrides: Partial<Registration> = {}): Registration {
  return {
    registrationKey: randomUUID(),
    legalName: "Aiman Bin Yusof",
    username: `aiman.${randomInt(100000, 999999)}`,
    email: `explore-${randomUUID()}@example.invalid`,
    password,
    nric: `90041514${randomInt(1000, 9999)}`,
    mobile: "+60123456789",
    residentialAddress: "18 Jalan SS 2/24, 47300 Petaling Jaya, Selangor",
    ...overrides,
  };
}

async function fillRegistration(page: Page, details: Registration) {
  await page.getByLabel("Legal name", { exact: true }).fill(details.legalName);
  await page.getByLabel("NRIC", { exact: true }).fill(details.nric);
  await page.getByLabel("Residential address", { exact: true }).fill(details.residentialAddress);
  await page.getByLabel("Email", { exact: true }).fill(details.email);
  await page.getByLabel("Mobile", { exact: true }).fill(details.mobile);
  await page.getByLabel("Username", { exact: true }).fill(details.username);
  await page.getByLabel("Password", { exact: true }).fill(details.password);
}

async function registerViaApi(context: BrowserContext, details: Registration) {
  return context.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: details,
  });
}

async function loginStatus(context: BrowserContext, email: string, pass = password) {
  const response = await context.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: { loginKey: randomUUID(), email, password: pass },
  });
  return response.status();
}

function countRequests(page: Page, pathSuffix: string) {
  const seen: string[] = [];
  page.on("request", (request) => {
    if (request.method() === "POST" && request.url().endsWith(pathSuffix)) {
      seen.push(request.url());
    }
  });
  return seen;
}

async function hasSessionCookie(context: BrowserContext) {
  return (await context.cookies()).some((cookie) => cookie.name === "apexauto_token");
}

// Visible refusal toast; the route announcer also has role alert but stays empty.
async function refusalText(page: Page) {
  const alert = page.getByRole("alert").filter({ hasText: /\S/ }).first();
  await expect(alert).toBeVisible();
  return (await alert.innerText()).trim();
}

function expectHumanText(text: string) {
  expect(text.length).toBeGreaterThan(5);
  expect(text, "no raw code").not.toMatch(/[a-z]+-[a-z]+-[a-z-]+|\b[A-Z_]{6,}\b|Exception|stack/);
}

test("AUTH-01 expected: registering a customer creates an account that can sign in", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  const details = newRegistration();
  const registrations = countRequests(page, "/api/auth/register");

  await page.goto("/register");
  await fillRegistration(page, details);
  await page.getByRole("button", { name: "Create account", exact: true }).click();

  // The product asks the new customer to sign in; no session is opened by registering.
  await expect(page).toHaveURL(`${origin}/login`);
  expect(registrations).toHaveLength(1);
  expect(await hasSessionCookie(context)).toBe(false);

  await signIn(page, details.email);
  await expect(page).toHaveURL(`${origin}/account`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  expectClean(guard);
});

test("AUTH-02 erratic: invalid registration input is refused and creates nothing", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  const registrations = countRequests(page, "/api/auth/register");
  await page.goto("/register");

  // Empty submit stops in the browser before any request is sent.
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  const formValid = await page
    .locator("form")
    .evaluate((form: HTMLFormElement) => form.checkValidity());
  expect(formValid).toBe(false);
  await expect(page).toHaveURL(`${origin}/register`);
  expect(registrations).toHaveLength(0);

  // An 11 digit NRIC and an invalid email are caught by the field rules.
  await fillRegistration(page, newRegistration({ nric: "90041514123", email: "not-an-email" }));
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  const nricValid = await page
    .getByLabel("NRIC", { exact: true })
    .evaluate((input: HTMLInputElement) => input.validity.valid);
  const emailValid = await page
    .getByLabel("Email", { exact: true })
    .evaluate((input: HTMLInputElement) => input.validity.valid);
  expect(nricValid).toBe(false);
  expect(emailValid).toBe(false);
  expect(registrations).toHaveLength(0);

  // Rules the browser cannot enforce are refused by the server with a readable message.
  const hostile = {
    "short password": newRegistration({ password: "short1!" }),
    "11 digit NRIC": newRegistration({ nric: "90041514123" }),
    "invalid email": newRegistration({ email: "not-an-email" }),
    "5000 character name": newRegistration({ legalName: "A".repeat(5000) }),
  };
  const accepted: string[] = [];
  for (const [label, attempt] of Object.entries(hostile)) {
    const response = await registerViaApi(context, attempt);
    expect(response.status(), label).toBeLessThan(500);
    if (response.status() < 400) {
      accepted.push(label);
    }
  }
  expect(accepted, "invalid registrations the server accepted").toEqual([]);

  // The same 5,000-character name through the form shows a refusal the visitor can act on.
  const longName = newRegistration({ legalName: "B".repeat(5000) });
  await page.reload();
  await fillRegistration(page, longName);
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  expectHumanText(await refusalText(page));
  await expect(page).toHaveURL(`${origin}/register`);
  expect(await loginStatus(context, longName.email)).not.toBe(200);
  expectClean(guard, refusalLogs);
});

test("AUTH-03 erratic: registering with a used email or username is refused", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  const first = newRegistration();
  expect((await registerViaApi(context, first)).status()).toBe(200);

  const sameEmail = newRegistration({ email: first.email });
  const sameUsername = newRegistration({ username: first.username });
  expect((await registerViaApi(context, sameEmail)).status()).toBe(409);
  expect([400, 409]).toContain((await registerViaApi(context, sameUsername)).status());

  await page.goto("/register");
  await fillRegistration(page, newRegistration({ email: first.email }));
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  expectHumanText(await refusalText(page));
  await expect(page).toHaveURL(`${origin}/register`);

  // No second account: the refused attempts' own credentials do not work.
  expect(await loginStatus(context, sameUsername.email)).not.toBe(200);
  expect(await loginStatus(context, first.email)).toBe(200);
  expectClean(guard, refusalLogs);
});

test("AUTH-04 erratic: double-clicking Create account creates one account", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  const details = newRegistration();
  const registrations = countRequests(page, "/api/auth/register");

  await page.goto("/register");
  await fillRegistration(page, details);
  await page.getByRole("button", { name: "Create account", exact: true }).dblclick();

  await expect(page).toHaveURL(`${origin}/login`);
  expect(registrations).toHaveLength(1);
  expect(await loginStatus(context, details.email)).toBe(200);
  // A second registration with the same email proves exactly one account was kept.
  const again = await registerViaApi(context, newRegistration({ email: details.email }));
  expect(again.status()).toBe(409);
  expectClean(guard);
});

const roleSignIns = [
  { email: "nur.aisyah@example.invalid", role: "Customer", landing: "/account" },
  { email: "ahmad.faizal@example.invalid", role: "Customer", landing: "/account" },
  { email: "aina.sofia@example.invalid", role: "SalesAgent", landing: "/dashboard/home" },
  {
    email: "azlan.rahim@example.invalid",
    role: "WorkshopTechnician",
    landing: "/dashboard/workshop",
  },
  { email: "siti.aminah@example.invalid", role: "BranchManager", landing: "/dashboard/home" },
  { email: "farah.aziz@example.invalid", role: "GeneralManager", landing: "/dashboard/home" },
  {
    email: "muhammad.hafiz@example.invalid",
    role: "Administrator",
    landing: "/dashboard/accounts",
  },
] as const;

test("AUTH-05 expected: each seeded role lands on its own page", async ({ page, context }) => {
  test.setTimeout(180_000);
  const guard = guardPage(page);

  for (const account of roleSignIns) {
    await context.clearCookies();
    await page.goto("about:blank");
    await signInAs(page, account.email, account.role);
    expect(new URL(page.url()).pathname, account.email).toBe(account.landing);
  }
  expectClean(guard);
});

test("AUTH-06 erratic: bad credentials get one generic refusal and upper case email works", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);
  const signInRequests = countRequests(page, "/api/auth/login");

  // An empty form is stopped by the browser before any request.
  await page.goto("/login");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  expect(signInRequests).toHaveLength(0);
  await expect(page).toHaveURL(`${origin}/login`);

  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Wrong-password-1!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  const wrongPassword = await refusalText(page);
  expectHumanText(wrongPassword);
  expect(await hasSessionCookie(context)).toBe(false);

  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(`nobody-${randomUUID()}@example.invalid`);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  const unknownEmail = await refusalText(page);
  expect(unknownEmail, "same refusal for unknown email and wrong password").toBe(wrongPassword);
  expect(await hasSessionCookie(context)).toBe(false);

  await signIn(page, "AINA.SOFIA@EXAMPLE.INVALID");
  await expect(page).toHaveURL(`${origin}/dashboard/home`);
  expectClean(guard, refusalLogs);
});

test("AUTH-07 erratic: a visitor opening private pages is sent to sign in", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);

  await page.goto("/account");
  await expect(page).toHaveURL(/\/login/);
  await page.goto("/dashboard/settings");
  await expect(page).toHaveURL(/\/login/);

  await page.goto("/dashboard/home");
  await expect(page).toHaveURL(/\/login/);
  await signIn(page, "aina.sofia@example.invalid");
  await expect(page).toHaveURL(`${origin}/dashboard/home`);

  await context.clearCookies();
  await page.goto("/account");
  await expect(page).toHaveURL(/\/login/);
  await signIn(page, "ahmad.faizal@example.invalid");
  await expect(page).toHaveURL(`${origin}/account`);
  expectClean(guard);
});

test("AUTH-07 erratic: after signing in the visitor returns to the settings page they asked for", async ({
  page,
}) => {
  test.fail(
    true,
    "NEW: /dashboard/settings redirects to sign-in with returnTo=/dashboard and staff always land on their role page",
  );
  const guard = guardPage(page);

  await page.goto("/dashboard/settings");
  await expect(page).toHaveURL(/\/login/);
  await page.getByLabel("Email", { exact: true }).fill("muhammad.hafiz@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/dashboard/settings`);
  expectClean(guard);
});

test("AUTH-08 expected: after signing out, Back and reload do not show private pages", async ({
  page,
  context,
}) => {
  const guard = guardPage(page);

  await signInAs(page, "ahmad.faizal@example.invalid", "Customer");
  await page.goto("/account/details");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect.poll(() => hasSessionCookie(context)).toBe(false);
  await expect(page).not.toHaveURL(/\/account/);

  await page.goBack();
  await expect(page).toHaveURL(/\/login/);
  await expect(page.getByText("Close account")).toHaveCount(0);

  await page.reload();
  await expect(page).toHaveURL(/\/login/);
  await page.goto("/account");
  await expect(page).toHaveURL(/\/login/);
  expectClean(guard);
});

test("AUTH-09 erratic: a second tab notices that the session ended", async ({ page, context }) => {
  const guard = guardPage(page);
  const other = await context.newPage();
  const otherGuard = guardPage(other);

  await signInAs(page, "ahmad.faizal@example.invalid", "Customer");
  await other.goto("/account");
  await expect(other).toHaveURL(`${origin}/account`);
  const detailsLink = other.getByRole("link", { name: "Account details", exact: true });
  await expect(detailsLink).toBeVisible();

  await page.goto("/account/details");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect.poll(() => hasSessionCookie(context)).toBe(false);

  // Acting in the stale tab must not succeed; the visitor is offered sign-in.
  await detailsLink.click({ timeout: 5000 }).catch(() => undefined);
  await expect(other).toHaveURL(/\/login/);
  await expect(other.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  expectClean(guard);
  expectClean(otherGuard, refusalLogs);
});

test("AUTH-10 erratic: password recovery answers the same for unknown and known emails", async ({
  page,
}) => {
  const guard = guardPage(page);
  const confirmation = page.getByText(/If this account can receive a reset email/);

  async function requestLink(email: string) {
    await page.goto("/forgot-password");
    await page.getByLabel("Email", { exact: true }).fill(email);
    await page.getByRole("button", { name: "Send reset link", exact: true }).click();
    await expect(confirmation).toBeVisible();
    return (await confirmation.innerText()).trim();
  }

  const unknown = await requestLink(`nobody-${randomUUID()}@example.invalid`);
  const known = await requestLink("ahmad.faizal@example.invalid");
  expect(known).toBe(unknown);
  expectClean(guard);
});

test("AUTH-11 erratic: reset links without a valid token show an invalid-link page with a way forward", async ({
  page,
}) => {
  const guard = guardPage(page);

  // No token: the form stays locked and a new link can be requested.
  await page.goto("/reset-password");
  expectHumanText(await refusalText(page));
  await expect(page.getByRole("button", { name: "Reset password", exact: true })).toBeDisabled();
  await expect(page.getByRole("link", { name: "Request a new reset link" })).toBeVisible();

  // Garbage and truncated tokens are refused by the server when submitted.
  for (const token of [randomUUID().replace(/-/g, ""), "abc", "%E0%A4%A"]) {
    await page.goto(`/reset-password#token=${token}`);
    await page.getByLabel("New password", { exact: true }).fill("Brand-new-pass1!");
    await page.getByRole("button", { name: "Reset password", exact: true }).click();
    expectHumanText(await refusalText(page));
    await expect(page).toHaveURL(/\/reset-password/);
    await expect(page.getByRole("link", { name: "Request a new reset link" })).toBeVisible();
  }
  expectClean(guard, refusalLogs);
});

test("AUTH-11 erratic: verification links without a valid token are refused", async ({ page }) => {
  const guard = guardPage(page);

  await page.goto("/verify-email");
  expectHumanText(await refusalText(page));
  await expect(page.getByRole("button", { name: "Verify email", exact: true })).toBeDisabled();

  for (const token of [randomUUID().replace(/-/g, ""), "abc"]) {
    await page.goto(`/verify-email#token=${token}`);
    await page.getByRole("button", { name: "Verify email", exact: true }).click();
    expectHumanText(await refusalText(page));
    await expect(page).toHaveURL(/\/verify-email/);
  }
  expectClean(guard, refusalLogs);
});

test("AUTH-11 erratic: the verification page offers a way to request a new link", async ({
  page,
}) => {
  test.fail(true, "NEW: /verify-email shows no way to request a new link when the token is bad");
  await page.goto("/verify-email");
  await expect(page.getByRole("navigation", { name: "Account access" })).toBeVisible({
    timeout: 3000,
  });
});

test("AUTH-02 erratic: a mobile number with letters is refused by the server", async ({
  context,
}) => {
  test.fail(true, "NEW: /api/auth/register accepts a mobile number made of letters (200)");
  const attempt = newRegistration({ mobile: "abcdefghij" });
  const response = await registerViaApi(context, attempt);
  expect(response.status()).toBeGreaterThanOrEqual(400);
  expect(response.status()).toBeLessThan(500);
});
