import { expect, test } from "@playwright/test";

test("administrator cannot enter the workshop workspace", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("muhammad.hafiz@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/accounts$/);
  await expect(page.getByRole("heading", { name: "Accounts", exact: true })).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
  await page.goto("/dashboard/workshop");
  await expect(
    page.getByRole("heading", { name: "You don't have access to this page", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
});
