import { readFileSync } from "node:fs";
import path from "node:path";
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
  const original = await security.inputValue();
  const save = page.getByRole("button", { name: "Save settings" });

  try {
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

    // A preservation hold is placed by reservation number, listed and released.
    const reservation = readFileSync(
      path.join(process.env.BROWSER_EVIDENCE_DIR!, "reservation-number.txt"),
      "utf8",
    ).trim();
    const place = page.getByRole("button", { name: "Place hold", exact: true });
    await expect(page.getByText("No sale is held.")).toBeVisible();
    await expect(place).toBeDisabled();
    await page.getByLabel("Reservation number", { exact: true }).fill("RSV-0000-000000");
    await page.getByLabel("Hold until", { exact: true }).fill("2030-01-31");
    await page.getByLabel("Reason", { exact: true }).fill("Dispute under review");
    await place.click();
    await expect(page.getByText("No reservation has that number.")).toBeVisible();
    await page.getByLabel("Reservation number", { exact: true }).fill(reservation);
    await place.click();
    await expect(page.getByText("Preservation hold placed.")).toBeVisible();
    const holds = page.getByRole("list", { name: "Current holds" });
    await expect(holds).toContainText(reservation);
    await expect(holds).toContainText("Dispute under review");
    await page.reload();
    await expect(page.getByRole("list", { name: "Current holds" })).toContainText(reservation);
    await page.getByRole("button", { name: `Release ${reservation}`, exact: true }).click();
    await expect(page.getByText("Preservation hold released.")).toBeVisible();
    await expect(page.getByText("No sale is held.")).toBeVisible();

    // The section tab is reachable from the other settings sections.
    await page.getByRole("button", { name: "Promos", exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard\/settings\/promos/);
    await page.getByRole("button", { name: "Retention", exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard\/settings\/retention$/);
  } finally {
    // Put the starting period back so later runs and journeys see the seeded retention.
    await page.goto("/dashboard/settings/retention");
    const field = page.getByLabel("Security records (days)", { exact: true });
    await field.fill(original);
    const confirm = page.getByLabel(/I understand/);
    if (await confirm.isVisible()) {
      await confirm.check();
    }
    await save.click();
    await expect(page.getByText("Retention settings saved.")).toBeVisible();
  }
});
