import { randomInt, randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { signInAs } from "./staff-session";

const administrator = "muhammad.hafiz@example.invalid";

test("the Retention settings tab works at phone width", async ({ page }, info) => {
  test.skip(info.project.name !== "mobile", "The desktop journey covers saving and shortening.");
  test.setTimeout(120_000);

  await signInAs(page, administrator, "Administrator");
  await page.goto("/dashboard/settings/retention");
  const security = page.getByLabel("Security records (days)", { exact: true });
  await expect(security).toBeVisible();
  await expect(page.getByLabel("Business evidence (years)", { exact: true })).toBeVisible();

  // Nothing may force sideways scrolling, and the primary control stays reachable.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  const save = page.getByRole("button", { name: "Save settings" });
  await expect(save).toBeDisabled();
  await security.fill("45");
  await expect(save).toBeEnabled();
  await save.scrollIntoViewIfNeeded();
  const box = await save.boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(36);

  // Shortening cannot be saved until it is confirmed.
  await security.fill("8");
  await expect(save).toBeDisabled();
  await page.getByLabel(/I understand/).check();
  await expect(save).toBeEnabled();
});

test("the account closure dialog works at phone width and shows no sideways scroll", async ({
  page,
  context,
}, info) => {
  test.skip(info.project.name !== "mobile", "The desktop journeys cover the closure flow.");
  test.setTimeout(120_000);
  const origin = process.env.APP_BASE_URL!;
  const email = `closure-mobile-${randomUUID()}@example.invalid`;
  const registered = await context.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: {
      registrationKey: randomUUID(),
      legalName: "Farah Binti Karim",
      username: `farah.mobile.${randomInt(100000, 999999)}`,
      email,
      password: "Password123!",
      nric: `910720${randomInt(100000, 999999)}`,
      mobile: "+60123456781",
      residentialAddress: "7 Jalan Seri Utama, Shah Alam, Selangor",
    },
  });
  expect(registered.status()).toBe(200);
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
  await page.goto("/account/details");

  await page.getByRole("button", { name: "Close account" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  const box = await dialog.boundingBox();
  const viewport = page.viewportSize()!;
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width);

  // The confirmation stays reachable by scrolling the dialog, and Not now leaves the account untouched.
  const confirm = dialog.getByRole("button", { name: "Close my account permanently" });
  await confirm.scrollIntoViewIfNeeded();
  await expect(confirm).toBeVisible();
  await dialog.getByRole("button", { name: "Not now" }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
});
