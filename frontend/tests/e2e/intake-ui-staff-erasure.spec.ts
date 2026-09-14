import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import { signInAs } from "./staff-session";

const origin = process.env.APP_BASE_URL!;
const administrator = "muhammad.hafiz@example.invalid";
// Provisioned by the browser harness for this journey only: a current employee with no open work.
const employee = "retiring.employee@example.invalid";
const employeeName = "Hanis Binti Roslan";
const password = "Password123!";

test("an administrator erases an employee with no open work", async ({ page, browser }, info) => {
  test.skip(info.project.name === "mobile", "Administration is a desktop workflow.");
  test.setTimeout(120_000);

  await signInAs(page, administrator, "Administrator");
  await page.getByRole("searchbox", { name: "Search staff", exact: true }).fill(employee);
  const row = page.getByRole("row").filter({ hasText: employeeName });
  await expect(row).toBeVisible();
  await row.click();
  const drawer = page.getByRole("dialog").first();
  await drawer.getByRole("button", { name: "Erase account", exact: true }).click();

  const erase = page.getByRole("dialog", { name: "Erase account", exact: true });
  await expect(erase.getByText("This cannot be undone.")).toBeVisible();
  await erase.getByLabel("Reason", { exact: true }).fill("Journey employee erasure");
  await erase.getByLabel("Your password", { exact: true }).fill("Wrong-password-1");
  await erase.getByRole("button", { name: "Erase permanently" }).click();
  await expect(erase.getByText("This cannot be undone.")).toBeVisible();
  // A wrong password must not sign the Administrator out.
  expect((await page.context().cookies()).some((cookie) => cookie.name === "apexauto_token")).toBe(
    true,
  );

  await erase.getByLabel("Your password", { exact: true }).fill(password);
  await erase.getByRole("button", { name: "Erase permanently" }).click();
  const erased = page.getByRole("dialog", { name: "Account erased", exact: true });
  await expect(erased.getByText(/pseudonymized, not anonymous/)).toBeVisible();
  await erased.getByRole("button", { name: "Done" }).click();
  await expect(erased).toHaveCount(0);

  // The erased employee can no longer sign in, and their address no longer finds a person.
  const context = await browser.newContext({ baseURL: origin });
  const login = await context.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: { loginKey: randomUUID(), email: employee, password },
  });
  expect(login.status()).toBe(401);
  await page.getByRole("searchbox", { name: "Search staff", exact: true }).fill(employee);
  await expect(page.getByRole("row").filter({ hasText: employeeName })).toHaveCount(0);
});
