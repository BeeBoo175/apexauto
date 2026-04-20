import { expect, type Page } from "@playwright/test";

export async function setAppointmentDates(page: Page, from: string, to: string) {
  const filters = page.getByRole("region", { name: "Inspection time", exact: true });
  if (!(await filters.isVisible())) {
    await page.getByRole("button", { name: /^Filter Inspection time/ }).click();
  }
  await filters.getByLabel("From", { exact: true }).fill(from);
  await filters.getByLabel("To", { exact: true }).fill(to);
}

export async function expectWorklistPage(page: Page, number: number) {
  await expect.poll(() => Number(new URL(page.url()).searchParams.get("page") ?? 1)).toBe(number);
}
