import { expect, test } from "@playwright/test";

test("administrator cannot enter the inspection workspace", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("muhammad.hafiz@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole("heading", { name: "Your staff workspace", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
  await page.goto("/dashboard/inspection/repairs");
  await expect(
    page.getByRole("heading", { name: "Inspection access unavailable", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
});
