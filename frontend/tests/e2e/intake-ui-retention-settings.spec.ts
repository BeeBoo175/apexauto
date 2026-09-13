import { expect, test } from "@playwright/test";
import { signInAs } from "./staff-session";

const administrator = "muhammad.hafiz@example.invalid";

test("an administrator extends and then shortens a retention period with confirmation", async ({
  page,
}, info) => {
  test.skip(info.project.name === "mobile", "Administration is a desktop workflow.");
  test.setTimeout(120_000);

  await signInAs(page, administrator, "Administrator");
  await page.goto("/dashboard/settings/retention");
  const security = page.getByLabel("Security records (days)", { exact: true });
  await expect(security).toHaveValue("30");
  const save = page.getByRole("button", { name: "Save settings" });

  await security.fill("6");
  await expect(page.getByText("Enter whole days from 7 to 365.")).toBeVisible();
  await expect(save).toBeDisabled();

  // Extending needs no confirmation and survives a reload.
  await security.fill("60");
  await expect(page.getByText(/I understand/)).toHaveCount(0);
  await save.click();
  await expect(page.getByText("Retention settings saved.")).toBeVisible();
  await page.reload();
  await expect(page.getByLabel("Security records (days)", { exact: true })).toHaveValue("60");

  // Shortening cannot be saved until the consequence is confirmed.
  await page.getByLabel("Security records (days)", { exact: true }).fill("14");
  await expect(save).toBeDisabled();
  await page.getByLabel(/I understand/).check();
  await expect(save).toBeEnabled();
  await save.click();
  await expect(page.getByText("Retention settings saved.")).toBeVisible();
  await page.reload();
  await expect(page.getByLabel("Security records (days)", { exact: true })).toHaveValue("14");

  // The section tab is reachable from the other settings sections.
  await page.getByRole("button", { name: "Promos", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/settings\/promos/);
  await page.getByRole("button", { name: "Retention", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/settings\/retention$/);
});
