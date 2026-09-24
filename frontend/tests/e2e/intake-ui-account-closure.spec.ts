import { randomInt, randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";

const origin = process.env.APP_BASE_URL!;
const password = "Password123!";

test("a customer closes their own account and can no longer sign in", async ({ page, context }) => {
  test.setTimeout(90000);

  const email = `closure-${randomUUID()}@example.invalid`;
  const registered = await context.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: {
      registrationKey: randomUUID(),
      legalName: "Farah Binti Karim",
      username: `farah.karim.${randomInt(100000, 999999)}`,
      email,
      password,
      nric: `910720${randomInt(100000, 999999)}`,
      mobile: "+60123456780",
      residentialAddress: "7 Jalan Seri Utama, Shah Alam, Selangor",
    },
  });
  expect(registered.status()).toBe(200);

  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(`${origin}/account`);
  await page.goto("/account/details");

  await page.getByRole("button", { name: "Close account" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("This cannot be undone.")).toBeVisible();
  await expect(dialog.getByText(/Kept:/).first()).toBeVisible();

  await dialog.getByLabel("Your password").fill("Wrong-password-1");
  await dialog.getByRole("button", { name: "Close my account permanently" }).click();
  await expect(
    dialog.getByText("That password is not correct. Nothing was changed."),
  ).toBeVisible();
  // A wrong password must not sign the browser out.
  expect((await context.cookies()).some((cookie) => cookie.name === "apexauto_token")).toBe(true);

  await dialog.getByLabel("Your password").fill(password);
  await dialog.getByRole("button", { name: "Close my account permanently" }).click();
  await expect(dialog.getByText("Account closed")).toBeVisible();
  await expect(dialog.getByText(/pseudonymized, not anonymous/)).toBeVisible();
  await dialog.getByRole("button", { name: "Done" }).click();
  await expect(page).toHaveURL(`${origin}/`);

  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByText(/Email or password is incorrect/)).toBeVisible();
  await expect(page).toHaveURL(/\/login/);
});
