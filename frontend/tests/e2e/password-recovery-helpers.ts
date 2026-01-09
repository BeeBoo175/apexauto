import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { expect } from "@playwright/test";
import type { APIRequestContext, BrowserContext, Page, TestInfo } from "@playwright/test";

export const recoveredPassword = "Recovered123!";

export function recoveryFixture(): {
  UserId: string;
  Email: string;
  LegalName: string;
  OldSessions: string[];
  UnrelatedSession: string | null;
} {
  return JSON.parse(readFileSync(evidencePath("recovery.json"), "utf8"));
}

export function evidencePath(name: string) {
  return path.join(process.env.AUTH_EVIDENCE_DIR!, name);
}

export function upstreamCount(route: string): number {
  const counts = JSON.parse(readFileSync(evidencePath("counts.json"), "utf8"));
  return counts[route] ?? 0;
}

export function rememberSession(token: string) {
  writeFileSync(evidencePath("recovered-session.txt"), token);
}

export async function capturedResetLink(request: APIRequestContext, email: string) {
  const inbox = process.env.AUTH_MAILPIT_BASE_URL;
  expect(inbox, "Recovery acceptance requires the loopback inbox").toBeTruthy();
  let resetLink: URL | null = null;

  await expect
    .poll(
      async () => {
        const response = await request.get(`${inbox}/api/v1/messages?limit=100`);
        expect(response.ok()).toBe(true);
        const summaries = (await response.json()) as {
          messages: { ID: string; To: { Address: string }[] }[];
        };
        const owned = summaries.messages.filter((message) =>
          message.To.some((recipient) => recipient.Address === email),
        );

        for (const message of owned) {
          const detail = await request.get(`${inbox}/api/v1/message/${message.ID}`);
          expect(detail.ok()).toBe(true);
          const mail = (await detail.json()) as { Text: string };
          const found = mail.Text.match(/https?:\/\/[^\s]+\/reset-password#token=[a-f0-9.]+/i)?.[0];

          if (found) {
            resetLink = new URL(found);
            return true;
          }
        }

        return false;
      },
      { timeout: 30_000 },
    )
    .toBe(true);

  expect(
    resetLink!.origin === process.env.APP_BASE_URL,
    "Captured mail points to the actual browser origin",
  ).toBe(true);
  expect(resetLink!.search).toBe("");
  expect(resetLink!.pathname).toBe("/reset-password");
  return resetLink!;
}

export async function screenshot(page: Page, testInfo: TestInfo, name: string) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({
    path: evidencePath(`${testInfo.project.name}-${name}.png`),
    fullPage: true,
  });
}

export async function assertInitialRecoveryPage(page: Page, route: string, heading: string) {
  const before = {
    request: upstreamCount("/api/auth/password-recovery"),
    reset: upstreamCount("/api/auth/password-reset"),
  };
  const started = Date.now();
  const url = new URL(route, process.env.APP_BASE_URL);
  const pathname = url.pathname;
  const response = await page.goto(url.href);
  expect(response).not.toBeNull();
  const html = await response!.text();
  expect(html).toContain(heading);
  expect(html).toContain(pathname === "/forgot-password" ? "Email" : "New password");
  const safeBeforeHydration = await page.evaluate((documentHtml) => {
    const initial = new DOMParser().parseFromString(documentHtml, "text/html");
    return (
      initial.querySelector<HTMLInputElement>("form input")?.disabled === true &&
      initial.querySelector<HTMLButtonElement>("form button[type=submit]")?.disabled === true &&
      initial.querySelector("noscript")?.textContent?.includes("JavaScript is required") === true
    );
  }, html);
  expect(
    safeBeforeHydration,
    "Initial HTML disables credentials and submit until JavaScript is ready",
  ).toBe(true);
  expect(response!.headers()["cache-control"]).toContain("no-store");
  expect(response!.headers()["x-robots-tag"]).toContain("noindex");
  expect(response!.headers()["referrer-policy"]).toBe("no-referrer");
  await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
  const contentVisibleMs = Date.now() - started;
  if (pathname === "/forgot-password") {
    await expect(page.getByLabel("Email", { exact: true })).toBeEnabled();
    await expect(page.getByRole("button", { name: "Send reset link", exact: true })).toBeEnabled();
  }

  if (pathname === "/reset-password" && url.hash) {
    await expect(page.getByLabel("New password", { exact: true })).toBeEnabled();
    await expect(page.getByRole("button", { name: "Reset password", exact: true })).toBeEnabled();
  }
  expect(upstreamCount("/api/auth/password-recovery")).toBe(before.request);
  expect(upstreamCount("/api/auth/password-reset")).toBe(before.reset);
  writeFileSync(
    evidencePath(`${pathname.slice(1)}-initial.json`),
    JSON.stringify({
      initialHtmlBytes: Buffer.byteLength(html),
      contentVisibleMs,
    }),
  );
}

export async function signInRecovered(page: Page, email: string) {
  await page.getByRole("button", { name: "Use demo account", exact: true }).click();
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(/@/);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(recoveredPassword);
  const before = upstreamCount("/api/auth/login");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${process.env.APP_BASE_URL}/account`);
  expect(upstreamCount("/api/auth/login")).toBe(before + 1);
}

export async function installSession(context: BrowserContext, token: string) {
  await context.addCookies([
    {
      name: "apexauto_token",
      value: token,
      url: process.env.APP_BASE_URL!,
      secure: true,
      httpOnly: true,
      sameSite: "Lax",
    },
  ]);
}

export async function expectPreservedSession(context: BrowserContext, token: string) {
  const session = (await context.cookies()).find((cookie) => cookie.name === "apexauto_token");
  expect(session?.value === token, "Recovery preserves the existing browser session cookie").toBe(
    true,
  );
}

export async function assertRecoveryIngress(request: APIRequestContext) {
  const origin = process.env.APP_BASE_URL!;

  for (const route of ["/api/auth/password-recovery", "/api/auth/password-reset"]) {
    const before = upstreamCount(route);
    const fields = route.endsWith("password-recovery")
      ? { email: "request@example.invalid" }
      : { token: "invalid-token", password: "Abandoned123!" };
    const cases = [
      {
        label: "cross-origin",
        origin: "https://outside.invalid",
        type: "application/json",
        body: JSON.stringify(fields),
        expected: 403,
      },
      {
        label: "wrong-media",
        origin,
        type: "text/plain",
        body: JSON.stringify(fields),
        expected: 415,
      },
      { label: "malformed-json", origin, type: "application/json", body: "{", expected: 400 },
      {
        label: "oversized",
        origin,
        type: "application/json",
        body: "x".repeat(17 * 1024),
        expected: 413,
      },
      {
        label: "unexpected-field",
        origin,
        type: "application/json",
        body: JSON.stringify({ ...fields, unexpected: true }),
        expected: 400,
      },
    ];

    for (const denial of cases) {
      const response = await request.post(route, {
        headers: { Origin: denial.origin, "Content-Type": denial.type },
        data: denial.body,
      });
      expect(response.status(), `${route} rejects ${denial.label} before forwarding`).toBe(
        denial.expected,
      );
      expect(response.headers()["cache-control"]).toContain("no-store");
      expect(upstreamCount(route), `${route} denied ingress makes no upstream request`).toBe(
        before,
      );
    }
  }
}
