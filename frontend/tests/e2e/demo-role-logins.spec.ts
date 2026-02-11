import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";

const evidence = process.env.AUTH_EVIDENCE_DIR!;
const origin = process.env.APP_BASE_URL!;

test("every staff demo helper uses normal login and the current guest-page destination", async ({
  page,
  context,
}) => {
  const roles = [
    "Sales agent",
    "Workshop technician",
    "Branch manager",
    "General manager",
    "Administrator",
  ];
  const counts = () => JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
  const before = counts()["/api/auth/login"] ?? 0;

  for (const role of roles) {
    await page.goto("/login");
    await page.getByText("Demo credentials", { exact: true }).click();
    await page.getByRole("button", { name: `Use ${role} demo account`, exact: true }).click();
    const response = page.waitForResponse((receipt) => receipt.url().endsWith("/api/auth/login"));
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    expect((await response).status()).toBe(200);
    await expect(page).toHaveURL(`${origin}/`);
    const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
    expect(cookie.httpOnly).toBe(true);

    for (const route of ["/login", "/register"]) {
      await page.goto(route);
      await expect(page).toHaveURL(`${origin}/`);
    }

    const accountStatus = await page.evaluate(async () => (await fetch("/api/account")).status);
    expect(accountStatus).toBe(403);
    const logout = await page.evaluate(async () => {
      const receipt = await fetch("/api/auth/logout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: "{}",
      });
      return receipt.status;
    });
    expect(logout).toBe(200);
    expect((await context.cookies()).some((entry) => entry.name === "apexauto_token")).toBe(false);
    writeFileSync(path.join(evidence, "revoked-token.txt"), cookie.value);
  }

  expect((counts()["/api/auth/login"] ?? 0) - before).toBe(5);
});
