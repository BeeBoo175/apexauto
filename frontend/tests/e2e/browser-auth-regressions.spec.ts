import { randomInt, randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

const origin = process.env.APP_BASE_URL!;
const evidence = process.env.AUTH_EVIDENCE_DIR!;

type DelayedResponse = { release?: () => void };
declare global {
  interface Window {
    delayedRefresh: DelayedResponse;
    delayedResend: DelayedResponse;
  }
}

async function delayResend(page: Page) {
  await page.evaluate(() => {
    const state: DelayedResponse = {};
    window.delayedResend = state;
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (input, options) => {
      const response = await originalFetch(input, options);
      if (input !== "/api/auth/email-verification/resend") {
        return response;
      }

      const body = await response.text();
      options?.signal?.addEventListener("abort", () => {
        state.release?.();
        console.info("Delayed verification resend released after abort");
      });
      return new Promise<Response>((resolve) => {
        state.release = () => resolve(new Response(body, { status: response.status }));
      });
    };
  });
  await page.getByRole("button", { name: "Resend verification email" }).click();
  await expect.poll(() => page.evaluate(() => Boolean(window.delayedResend.release))).toBe(true);
}

async function delayRefresh(page: Page, outcome: "success" | "failure") {
  await page.evaluate((mode) => {
    const state: DelayedResponse = {};
    window.delayedRefresh = state;
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (input, options) => {
      const response = await originalFetch(input, options);
      if (input !== "/api/account") {
        return response;
      }

      const body = await response.text();
      options?.signal?.addEventListener("abort", () => {
        // Release the completed response before the actor's document reload replaces this page.
        state.release?.();
        console.info("Delayed account refresh released after abort");
      });
      // Keep a completed old-actor response alive even if transport cancellation is ignored.
      return new Promise<Response>((resolve, reject) => {
        state.release = () => {
          if (mode === "failure") {
            reject(new Error("Delayed refresh failed"));
            return;
          }

          resolve(new Response(body, { status: response.status }));
        };
      });
    };
  }, outcome);
  await page.getByRole("button", { name: "Refresh account" }).click();
  await expect.poll(() => page.evaluate(() => Boolean(window.delayedRefresh.release))).toBe(true);
}

function waitForRefreshAbort(page: Page) {
  return page.waitForEvent("console", {
    predicate: (message) => message.text() === "Delayed account refresh released after abort",
  });
}

async function signIn(page: Page, email: string) {
  await page.goto("/login");
  await page.getByRole("button", { name: "Use demo account" }).click();
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(/@/);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
}

test("cross-tab actor changes abort completed refreshes and discard previous account content", async ({
  page,
  context,
}) => {
  const owners = [];
  const profileFile = path.join(evidence, "profiles.json");
  for (const legalName of ["Siti Hajar binti Abdullah", "Tan Mei Ling"]) {
    const owner = {
      registrationKey: randomUUID(),
      legalName,
      email: `customer-${randomUUID()}@example.invalid`,
      password: "Password123!",
      nric: `92051614${randomInt(1000, 9999)}`,
      mobile: "+60123456789",
      residentialAddress: "18 Jalan SS 2/24, 47300 Petaling Jaya, Selangor",
    };
    const registration = await context.request.post("/api/auth/register", {
      headers: { Origin: origin },
      data: owner,
    });
    expect(registration.status()).toBe(200);
    const profiles = JSON.parse(readFileSync(profileFile, "utf8"));
    profiles.push({
      RegistrationKey: owner.registrationKey,
      LegalName: legalName,
      Email: owner.email,
      Nric: owner.nric,
    });
    writeFileSync(profileFile, JSON.stringify(profiles));
    owners.push({ LegalName: legalName, Email: owner.email });
  }
  await signIn(page, owners[0].Email);
  const actorTab = await context.newPage();

  await delayResend(page);
  await delayRefresh(page, "success");
  const firstAbort = waitForRefreshAbort(page);
  const resendAbort = page.waitForEvent("console", {
    predicate: (message) => message.text() === "Delayed verification resend released after abort",
  });
  await signIn(actorTab, owners[1].Email);
  await firstAbort;
  await resendAbort;
  await expect(page.getByText(owners[0].LegalName, { exact: true })).toHaveCount(0);
  await expect(page.getByText(/account could not be refreshed/)).toHaveCount(0);
  await expect(page.getByText(/verification is still needed/)).toHaveCount(0);
  await expect(page.getByText(owners[1].LegalName, { exact: true })).toBeVisible();

  await delayRefresh(page, "failure");
  const secondAbort = waitForRefreshAbort(page);
  await actorTab.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(actorTab).toHaveURL(`${origin}/login`);
  await secondAbort;
  await expect(page.getByText(owners[1].LegalName, { exact: true })).toHaveCount(0);
  await expect(page.getByText(/account could not be refreshed/)).toHaveCount(0);
  await expect(page.getByText(/These details may be out of date/)).toHaveCount(0);
  await expect(page).toHaveURL(/\/login\?returnTo=/);
});
