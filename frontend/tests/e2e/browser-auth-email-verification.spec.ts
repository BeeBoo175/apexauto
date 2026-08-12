import { randomInt, randomUUID } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";
import type { APIRequestContext, Page } from "@playwright/test";

const origin = process.env.APP_BASE_URL!;
const mailpit = process.env.BROWSER_MAILPIT_BASE_URL;
const evidence = process.env.BROWSER_EVIDENCE_DIR!;

function confirmationCount() {
  const counts = JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
  return counts["/api/auth/email-verification"] ?? 0;
}

type MailSummary = {
  ID: string;
  To: { Address: string }[];
};

async function messagesFor(request: APIRequestContext, email: string): Promise<MailSummary[]> {
  const response = await request.get(`${mailpit}/api/v1/messages?limit=100`);
  expect(response.ok()).toBe(true);
  const inbox = (await response.json()) as { messages: MailSummary[] };
  return inbox.messages.filter((message) =>
    message.To.some((recipient) => recipient.Address === email),
  );
}

async function waitForMail(request: APIRequestContext, email: string, count: number) {
  await expect
    .poll(async () => (await messagesFor(request, email)).length, { timeout: 30_000 })
    .toBeGreaterThanOrEqual(count);
  return messagesFor(request, email);
}

async function verificationLink(request: APIRequestContext, id: string) {
  const response = await request.get(`${mailpit}/api/v1/message/${id}`);
  expect(response.ok()).toBe(true);
  const message = (await response.json()) as { Text: string };
  const link = message.Text.match(/https?:\/\/[^\s]+\/verify-email#token=[a-f0-9.]+/i)?.[0];
  expect(link).toBeDefined();
  const url = new URL(link!);
  expect(url.search).toBe("");
  expect(url.pathname).toBe("/verify-email");
  return url;
}

async function waitForNewLink(request: APIRequestContext, email: string, previousHash: string) {
  let newLink: URL | null = null;

  await expect
    .poll(
      async () => {
        const messages = await messagesFor(request, email);

        for (const message of messages) {
          const link = await verificationLink(request, message.ID);

          if (link.hash !== previousHash) {
            newLink = link;
            return true;
          }
        }

        return false;
      },
      { timeout: 30_000 },
    )
    .toBe(true);

  return newLink!;
}

async function signIn(page: Page, email: string) {
  await page.goto("/login");
  await page.getByText("Sample credentials", { exact: true }).click();
  await page.getByRole("button", { name: "Use Customer sample account" }).click();
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(/@/);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
  await page.getByRole("link", { name: "Account details", exact: true }).click();
}

test("email verification journey uses captured mail and deliberate anonymous confirmation", async ({
  page,
  context,
  request,
}, testInfo) => {
  test.skip(!mailpit, "This journey requires the Development-only local Mailpit inbox.");
  test.setTimeout(150_000);

  const registration = {
    registrationKey: randomUUID(),
    legalName: "Nur Aina Abdullah",
    username: `nur.aina.${randomInt(100000, 999999)}`,
    email: `verification-${randomUUID()}@example.invalid`,
    password: "Password123!",
    nric: `940215${randomInt(100000, 999999)}`,
    mobile: "+60123456789",
    residentialAddress: "18 Jalan Melati, Petaling Jaya, Selangor",
  };
  const registered = await context.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: registration,
  });
  expect(registered.status()).toBe(200);
  const profileFile = path.join(evidence, "profiles.json");
  const profiles = JSON.parse(readFileSync(profileFile, "utf8"));
  profiles.push({
    RegistrationKey: registration.registrationKey,
    LegalName: registration.legalName,
    Email: registration.email,
    Nric: registration.nric,
  });
  writeFileSync(profileFile, JSON.stringify(profiles));

  const initialMail = (await waitForMail(request, registration.email, 1))[0];
  const initialLink = await verificationLink(request, initialMail.ID);
  await signIn(page, registration.email);
  await expect(page.getByText("Not verified", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Resend verification email" }).click();
  await expect(page.getByText(/an email is queued/)).toBeVisible();
  await expect(page.getByText(registration.legalName, { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Resend verification email" }).click();
  await expect(
    page.getByText(/Too many attempts\. Wait (?:\d+ seconds )?before trying again\./),
  ).toBeVisible();
  await expect(page.getByText(registration.legalName, { exact: true })).toBeVisible();
  const latestLink = await waitForNewLink(request, registration.email, initialLink.hash);
  expect(latestLink.hash).not.toBe(initialLink.hash);

  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/login`);
  expect((await context.cookies()).some((cookie) => cookie.name === "apexauto_token")).toBe(false);

  const beforeConfirmation = confirmationCount();
  await page.goto(`${origin}${initialLink.pathname}${initialLink.hash}`);
  await expect(page.getByRole("button", { name: "Verify email" })).toBeEnabled();
  expect(confirmationCount()).toBe(beforeConfirmation);
  await page.screenshot({
    path: path.join(evidence, `${testInfo.project.name}-verify-email.png`),
    fullPage: true,
  });
  await page.getByRole("button", { name: "Verify email" }).click();
  await expect(page.getByText("The verification link is invalid or expired.")).toBeVisible();
  expect(confirmationCount()).toBe(beforeConfirmation + 1);

  await page.goto(`${origin}${latestLink.pathname}${latestLink.hash}`);
  await expect(page.getByRole("button", { name: "Verify email" })).toBeEnabled();
  expect(confirmationCount()).toBe(beforeConfirmation + 1);
  await page.getByRole("button", { name: "Verify email" }).click();
  await expect(page).toHaveURL(`${origin}/login`);
  expect(confirmationCount()).toBe(beforeConfirmation + 2);
  const confirmedFile = path.join(evidence, "confirmed-emails.json");
  const confirmed = JSON.parse(readFileSync(confirmedFile, "utf8"));
  confirmed.push(registration.email);
  writeFileSync(confirmedFile, JSON.stringify(confirmed));

  await signIn(page, registration.email);
  await expect(page.getByText("Verified", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Resend verification email" })).toHaveCount(0);
  await page.screenshot({
    path: path.join(evidence, `${testInfo.project.name}-account-verified.png`),
    fullPage: true,
  });
});
