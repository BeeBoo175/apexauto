import { expect, type Page } from "@playwright/test";

// Navigation and queue review shared by the staff workspaces, not only intake.
export async function openStaffWorkspace(page: Page, name: string, blocked = false) {
  const link = page.getByRole("link", { name, exact: true });
  if (!(await link.isVisible())) {
    await page.getByRole("button", { name: "Workspaces", exact: true }).click();
  }
  if (blocked) {
    await expect(link).toHaveAttribute("aria-disabled", "true");
  }
  await link.click({ force: blocked });
  if (blocked) {
    await closeStaffWorkspaces(page);
  }
}

export async function closeStaffWorkspaces(page: Page) {
  const close = page.getByRole("button", { name: "Close workspaces", exact: true });
  if (await close.isVisible()) {
    await close.click();
  }
}

export async function reviewAgentQueue(
  page: Page,
  area: "sales" | "inspection",
  capture: (page: Page, name: string) => Promise<void>,
) {
  await page.goto(`/dashboard/${area}`);
  const label = area === "sales" ? "Sales queue tabs" : "Workshop worklists";
  await expect(page.getByRole("navigation", { name: label, exact: true })).toBeVisible();
  const tab = page.getByRole("button", { name: area === "sales" ? /^Handling\b/ : /^To claim\b/ });
  await tab.click();
  await expect(tab).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("searchbox")).toBeVisible();
  const jobs = page.locator("ul li button");
  await expect(jobs.first()).toBeVisible();
  const identities = await jobs.allTextContents();
  const next = page.getByRole("button", { name: "Next records", exact: true });
  if (await next.isEnabled()) {
    await next.click();
    await expect(page.getByLabel("Page number", { exact: true })).toHaveValue("2");
    expect(await jobs.allTextContents()).not.toEqual(identities);
  }
  await capture(page, `${area}-queue`);
}
