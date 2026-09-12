import { randomInt, randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { signInAs } from "./staff-session";

const origin = process.env.APP_BASE_URL!;
const administrator = "muhammad.hafiz@example.invalid";
const password = "Password123!";

test("an administrator erases a customer who cannot sign in", async ({ page, browser }, info) => {
  test.skip(info.project.name === "mobile", "Administration is a desktop workflow.");
  test.setTimeout(120_000);

  const email = `erasure-${randomUUID()}@example.invalid`;
  const legalName = "Zulaikha Binti Ramli";
  const customerContext = await browser.newContext({ baseURL: origin });
  const registered = await customerContext.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: {
      registrationKey: randomUUID(),
      legalName,
      username: `zulaikha.${randomInt(100000, 999999)}`,
      email,
      password,
      nric: `920310${randomInt(100000, 999999)}`,
      mobile: "+60123456782",
      residentialAddress: "21 Jalan Bunga Raya, Klang, Selangor",
    },
  });
  expect(registered.status()).toBe(200);

  await signInAs(page, administrator, "Administrator");
  await page.goto("/dashboard/accounts/customers");
  await page.getByRole("searchbox").first().fill(email);
  const row = page.getByRole("row").filter({ hasText: legalName });
  await expect(row).toBeVisible();
  await row.click();
  const drawer = page.getByRole("dialog").first();
  await drawer.getByRole("button", { name: "Erase account", exact: true }).click();

  const erase = page.getByRole("dialog", { name: "Erase account", exact: true });
  await expect(erase.getByText("This cannot be undone.")).toBeVisible();
  await erase.getByLabel("Reason", { exact: true }).fill("Journey erasure request");
  await erase.getByLabel("Your password", { exact: true }).fill("Wrong-password-1");
  await erase.getByRole("button", { name: "Erase permanently" }).click();
  await expect(erase.getByText("This cannot be undone.")).toBeVisible();
  // A wrong password must not sign the Administrator out.
  expect((await page.context().cookies()).some((cookie) => cookie.name === "apexauto_token")).toBe(
    true,
  );

  await erase.getByLabel("Your password", { exact: true }).fill(password);
  await erase.getByRole("button", { name: "Erase permanently" }).click();
  // The dialog changes its title to the outcome, so it is found again by that name.
  const erased = page.getByRole("dialog", { name: "Account erased", exact: true });
  await expect(erased.getByText(/pseudonymized, not anonymous/)).toBeVisible();
  await erased.getByRole("button", { name: "Done" }).click();
  await expect(erased).toHaveCount(0);

  const login = await customerContext.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: { loginKey: randomUUID(), email, password },
  });
  expect(login.status()).toBe(401);
});
