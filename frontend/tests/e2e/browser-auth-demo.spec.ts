import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { test, expect } from "@playwright/test";

const demoAccount = {
  email: "nur.aisyah@example.invalid",
  password: "Password123!",
};

const demoRoles = [
  ["Customer", "nur.aisyah@example.invalid"],
  ["Sales agent", "aina.sofia@example.invalid"],
  ["Workshop technician", "azlan.rahim@example.invalid"],
  ["Branch manager", "siti.aminah@example.invalid"],
  ["General manager", "farah.aziz@example.invalid"],
  ["Administrator", "muhammad.hafiz@example.invalid"],
];

const evidence = process.env.AUTH_EVIDENCE_DIR!;

function loginCount() {
  const counts = JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
  return counts["/api/auth/login"] ?? 0;
}

test("demo account autofill preserves deliberate sign-in and exact uncertain retry", async ({
  page,
  context,
}, testInfo) => {
  test.setTimeout(60_000);
  const requests: string[] = [];
  page.on("request", (request) => {
    if (request.url().endsWith("/api/auth/login")) {
      requests.push(request.postData()!);
    }
  });
  await page.goto("/login");
  const helpers = page
    .getByRole("group", { name: "Demo accounts", exact: true })
    .getByRole("button");
  const helper = page.getByRole("button", { name: "Use Customer demo account", exact: true });
  const email = page.getByLabel("Email", { exact: true });
  const password = page.getByLabel("Password", { exact: true });
  const before = loginCount();
  await expect(email).toHaveValue("");
  await expect(password).toHaveValue("");
  await expect(helpers).toHaveCount(0);
  await password.focus();
  await page.keyboard.press("Tab");
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: "Create an account", exact: true })).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: "Forgot password?", exact: true })).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(page.locator("summary")).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(helpers).toHaveCount(6);
  await page.keyboard.press("Tab");
  await expect(helper).toBeFocused();
  for (const [role, address] of demoRoles) {
    const roleHelper = page.getByRole("button", { name: `Use ${role} demo account`, exact: true });
    await roleHelper.focus();
    await expect(roleHelper).toBeFocused();
    await roleHelper.press("Enter");
    await expect(email).toHaveValue(address);
    await expect(password).toHaveValue(demoAccount.password);
    await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeFocused();
    expect(requests).toHaveLength(0);
    expect(loginCount()).toBe(before);
    await email.fill("edited.customer@example.invalid");
    await password.fill("EditablePassword123!");
  }
  await helper.click();
  await expect(email).toHaveValue(demoAccount.email);
  await expect(password).toHaveValue(demoAccount.password);
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeFocused();
  expect(requests).toHaveLength(0);
  expect(loginCount()).toBe(before);
  const storedValues = await page.evaluate(() => [
    ...Object.values(localStorage),
    ...Object.values(sessionStorage),
  ]);
  expect(storedValues.join(" ")).not.toContain(demoAccount.password);
  for (const [, address] of demoRoles) {
    expect(storedValues.join(" ")).not.toContain(address);
  }
  await email.fill("edited.customer@example.invalid");
  await password.fill("EditablePassword123!");
  await helper.click();
  await expect(email).toHaveValue(demoAccount.email);
  await expect(password).toHaveValue(demoAccount.password);
  await expect(
    page.getByText("Demo details filled. Sign in to continue.", { exact: true }),
  ).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: path.join(evidence, `${testInfo.project.name}-demo-autofill.png`),
    fullPage: true,
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  const viewport = page.viewportSize()!;
  await page.setViewportSize({ width: 320, height: viewport.height });
  await page.evaluate(() => window.scrollTo(0, 0));
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  for (const roleHelper of await helpers.all()) {
    await expect(roleHelper).toBeVisible();
    expect(await roleHelper.evaluate((button) => button.scrollWidth <= button.clientWidth)).toBe(
      true,
    );
  }
  await page.screenshot({
    path: path.join(evidence, `${testInfo.project.name}-demo-320.png`),
    fullPage: true,
  });
  await page.setViewportSize(viewport);

  writeFileSync(path.join(evidence, "fault.txt"), "slow-login");
  try {
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    for (const roleHelper of await helpers.all()) {
      await expect(roleHelper).toBeDisabled();
    }
    const retry = page.getByRole("button", { name: "Retry same request", exact: true });
    await expect(retry).toBeVisible({ timeout: 20_000 });
    for (const roleHelper of await helpers.all()) {
      await expect(roleHelper).toBeDisabled();
    }
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
