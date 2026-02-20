import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";

const demoAccount = {
  email: "nur.aisyah@example.invalid",
  password: "Password123!",
};

const evidence = process.env.BROWSER_EVIDENCE_DIR!;

function loginCount() {
  const counts = JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
  return counts["/api/auth/login"] ?? 0;
}

test("login retains the exact operation when the service response is unconfirmed", async ({
  page,
  context,
}) => {
  test.setTimeout(60_000);
  const requests: string[] = [];
  page.on("request", (request) => {
    if (request.url().endsWith("/api/auth/login")) {
      requests.push(request.postData()!);
    }
  });
  await page.goto("/login");
  const email = page.getByLabel("Email", { exact: true });
  const password = page.getByLabel("Password", { exact: true });
  const before = loginCount();
  await email.fill(demoAccount.email);
  await password.fill(demoAccount.password);

  writeFileSync(path.join(evidence, "fault.txt"), "slow-login");
  try {
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    const retry = page.getByRole("button", { name: "Retry same request", exact: true });
    await expect(retry).toBeVisible({ timeout: 20_000 });
    await expect(email).toBeDisabled();
    await expect(password).toBeDisabled();
    expect(requests).toHaveLength(1);
    const original = JSON.parse(requests[0]);
    expect(Object.keys(original).sort()).toEqual(["email", "loginKey", "password"]);
    expect(original.email).toBe(demoAccount.email);
    expect(original.password).toBe(demoAccount.password);
    writeFileSync(path.join(evidence, "fault.txt"), "");
    await retry.click();
    await expect(page).toHaveURL(`${process.env.APP_BASE_URL}/account`);
    expect(requests).toHaveLength(2);
    expect(requests[1]).toBe(requests[0]);
    expect(loginCount() - before).toBe(2);
    const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
    writeFileSync(path.join(evidence, "revoked-token.txt"), cookie.value);
    await page.getByRole("button", { name: "Sign out", exact: true }).click();
    await expect(page).toHaveURL(`${process.env.APP_BASE_URL}/login`);
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
});
