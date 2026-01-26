import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";

const evidence = process.env.AUTH_EVIDENCE_DIR!;
const origin = process.env.APP_BASE_URL!;

test("guest pages redirect only verified sessions and retain unavailable-session recovery", async ({
  page,
  context,
}, testInfo) => {
  const accountReads = () => {
    const counts = JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
    return counts["/api/account"] ?? 0;
  };
  const initialReads = accountReads();
  await page.goto("/login");
  await expect(page.getByLabel("Email", { exact: true })).toBeVisible();
  expect(accountReads()).toBe(initialReads);
  await page.getByText("Demo credentials", { exact: true }).click();
  await page.getByRole("button", { name: "Use Customer demo account", exact: true }).click();
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
  const token = (await context.cookies()).find((cookie) => cookie.name === "apexauto_token")!.value;
  const cookieHeader = { Cookie: `apexauto_token=${token}` };

  for (const route of ["/login?returnTo=https://outside.invalid", "/register"]) {
    const before = accountReads();
    const response = await context.request.get(route, { headers: cookieHeader, maxRedirects: 0 });
    expect(response.status()).toBe(307);
    expect(response.headers().location).toBe("/account");
    expect(response.headers()["cache-control"]).toContain("no-store");
    expect(accountReads() - before).toBe(1);
  }

  const faultPath = path.join(evidence, "fault.txt");
  writeFileSync(faultPath, "unavailable");
  try {
    await page.goto("/login");
    await expect(
      page.getByRole("heading", { name: "Check your session", exact: true }),
    ).toBeVisible();
    await expect(page.getByLabel("Password", { exact: true })).toHaveCount(0);
    const retry = page.getByRole("button", { name: "Try checking session again", exact: true });
    expect((await retry.boundingBox())!.height).toBe(44);
    await page.screenshot({
      path: path.join(evidence, `${testInfo.project.name}-session-unavailable.png`),
      fullPage: true,
    });
    expect((await context.cookies()).some((cookie) => cookie.value === token)).toBe(true);
  } finally {
    writeFileSync(faultPath, "");
  }
  await page.getByRole("button", { name: "Try checking session again", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);

  const logout = await context.request.post("/api/auth/logout", {
    headers: { ...cookieHeader, Origin: origin },
    data: {},
  });
  expect(logout.status()).toBe(200);
  for (const route of ["/login", "/register"]) {
    for (const tokenValue of [token, "forged.invalid.cookie"]) {
      const response = await context.request.get(route, {
        headers: { Cookie: `apexauto_token=${tokenValue}` },
        maxRedirects: 0,
      });
      expect(response.status()).toBe(200);
      expect(await response.text()).toContain('name="password"');
    }
  }

  const counts = JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
  expect(counts["/api/account"]).toBeGreaterThan(0);

  const staffLogin = await context.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: {
      loginKey: crypto.randomUUID(),
      email: "guest.staff@example.invalid",
      password: "Password123!",
    },
  });
  expect(staffLogin.status()).toBe(200);
  const staffCookie = staffLogin.headers()["set-cookie"].split(";")[0];
  for (const route of ["/login", "/register"]) {
    const response = await context.request.get(route, {
      headers: { Cookie: staffCookie },
      maxRedirects: 0,
    });
    expect(response.status()).toBe(307);
    expect(response.headers().location).toBe("/");
  }
});
