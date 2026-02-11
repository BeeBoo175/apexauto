import { setAppointmentDates, expectWorklistPage } from "./intake-ui-controls";
import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.AUTH_EVIDENCE_DIR!;
const assignment = (command: string) =>
  writeFileSync(path.join(evidence, "assignment.txt"), command);
const day = (offset: number) =>
  new Date(Date.now() + (offset * 24 + 8) * 3600000).toISOString().slice(0, 10);

test("staff reassignment clears old branch records and applies current permissions", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("aina.sofia@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await page.getByRole("link", { name: "Table", exact: true }).click();
  await setAppointmentDates(page, day(0), day(29));
  await page.getByRole("button", { name: "Apply filters", exact: true }).click();
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expectWorklistPage(page, 2);
  const selectedName = await page
    .getByRole("link", { name: /^Open / })
    .first()
    .getAttribute("aria-label");
  await page.getByRole("link", { name: selectedName!, exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  await dialog.getByLabel("VIN", { exact: true }).fill("PL1BT65R84B123456");
  await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
  const navigation = page.getByRole("link", { name: "Intake appointments", exact: true });
  async function navigateWorkspace() {
    if (!(await navigation.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    await navigation.click();
    await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toBeVisible();
    await page.getByRole("link", { name: "Table", exact: true }).click();
  }
  try {
    assignment("second-branch");
    await navigateWorkspace();
    await expect(page.getByRole("link", { name: /^Open VCA / })).toHaveCount(0);
    await expect(page.getByLabel("Branch", { exact: true })).toHaveCount(0);
    await setAppointmentDates(page, day(0), day(29));
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await expect(page.getByRole("link", { name: /^Open BPL / }).first()).toBeVisible();
    assignment("group");
    await navigateWorkspace();
    if (!(await navigation.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    await expect(page.getByText("General manager", { exact: true })).toBeVisible();
    await setAppointmentDates(page, day(0), day(29));
    await expect(page.getByLabel("Branch", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Apply filters", exact: true }).click();
    await page
      .getByRole("link", { name: /^Open / })
      .first()
      .click();
    await expect(dialog.getByText("Your role has read-only intake access.")).toBeVisible();
    await expect(dialog.getByLabel("VIN", { exact: true })).toHaveCount(0);
  } finally {
    assignment("restore");
    await page.reload();
    if (await dialog.isVisible()) {
      await dialog.getByRole("button", { name: "Close dialog", exact: true }).click();
    }
    if (!(await navigation.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    await expect(page.getByText("Sales agent", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Branch", { exact: true })).toHaveCount(0);
  }
});
