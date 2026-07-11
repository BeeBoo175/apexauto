import { expect, type BrowserContext, type Page } from "@playwright/test";
import { actorDestination, type Role } from "@/lib/auth/actor";

const seededPassword = "Password123!";

// Submits the seeded password through the login form; the caller asserts its own landing page.
export async function signIn(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(seededPassword);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}

// Signs in through the form and expects the role's canonical landing page.
export async function signInAs(page: Page, email: string, role: Role) {
  const destination = actorDestination({ userId: "", role, branchId: null });
  await signIn(page, email);
  await expect(page).toHaveURL(new RegExp(`${destination}$`));
}

export async function apiLogin(context: BrowserContext, email: string) {
  const response = await context.request.post("/api/auth/login", {
    headers: { Origin: process.env.APP_BASE_URL! },
    data: { loginKey: crypto.randomUUID(), email, password: seededPassword },
  });
  expect(response.status()).toBe(200);
}

// APIRequestContext does not apply Chromium's loopback Secure-cookie exception.
export async function privateHeaders(context: BrowserContext) {
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}`, Origin: process.env.APP_BASE_URL! };
}
