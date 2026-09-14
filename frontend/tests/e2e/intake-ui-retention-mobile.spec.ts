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
