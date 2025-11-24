import { randomInt, randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";
import type { Page } from "@playwright/test";

const origin = process.env.APP_BASE_URL!;
const evidence = process.env.AUTH_EVIDENCE_DIR!;

type DelayedRefresh = { release?: () => void; aborted: boolean };
declare global {
  interface Window {
    delayedRefresh: DelayedRefresh;
  }
}

async function delayRefresh(page: Page, outcome: "success" | "failure") {
  await page.evaluate((mode) => {
    const state: DelayedRefresh = { aborted: false };
    window.delayedRefresh = state;
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (input, options) => {
      const response = await originalFetch(input, options);
      if (input !== "/api/account") {
        return response;
      }

      const body = await response.text();
      options?.signal?.addEventListener("abort", () => {
        state.aborted = true;
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

async function releaseRefresh(page: Page) {
  await page.evaluate(async () => {
    window.delayedRefresh.release!();
    await new Promise<void>((resolve) =>
      requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
    );
  });
}

async function signIn(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
}

test("late refresh success and failure cannot restore content or feedback after cross-tab actor changes", async ({
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
  let continueNavigation: (() => void) | undefined;
  await page.route(
    (url) => url.pathname === "/account",
    async (route) => {
      await new Promise<void>((resolve) => {
        continueNavigation = resolve;
      });
      await route.continue();
    },
  );

  await delayRefresh(page, "success");
  await signIn(actorTab, owners[1].Email);
  await expect.poll(() => Boolean(continueNavigation)).toBe(true);
  await expect.poll(() => page.evaluate(() => window.delayedRefresh.aborted)).toBe(true);
  await releaseRefresh(page);
  await expect(page.getByText(owners[0].LegalName, { exact: true })).toHaveCount(0);
  await expect(page.getByText(/account could not be refreshed/)).toHaveCount(0);
  continueNavigation!();
  await expect(page.getByText(owners[1].LegalName, { exact: true })).toBeVisible();
  continueNavigation = undefined;

  await delayRefresh(page, "failure");
  await actorTab.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(actorTab).toHaveURL(`${origin}/login`);
  await expect.poll(() => Boolean(continueNavigation)).toBe(true);
  await expect.poll(() => page.evaluate(() => window.delayedRefresh.aborted)).toBe(true);
  await releaseRefresh(page);
  await expect(page.getByText(owners[1].LegalName, { exact: true })).toHaveCount(0);
  await expect(page.getByText(/account could not be refreshed/)).toHaveCount(0);
  await expect(page.getByText(/These details may be out of date/)).toHaveCount(0);
  continueNavigation!();
  await expect(page).toHaveURL(/\/login\?returnTo=/);
});
