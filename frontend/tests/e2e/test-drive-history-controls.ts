import { expect, type Page } from "@playwright/test";

export async function openTestDriveHistoryDuringRefresh(page: Page) {
  const drawer = page.getByRole("dialog", { name: "Test drive", exact: true });
  const refresh = drawer.getByRole("button", { name: "Refresh test drives", exact: true });
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let started = false;
  const pattern = "**/api/staff/test-drives?**";
  await page.route(pattern, async (route) => {
    const response = await route.fetch();
    started = true;
    await gate;
    await route.fulfill({ response });
  });
  try {
    const delivered = page.waitForResponse(
      (response) => new URL(response.url()).pathname === "/api/staff/test-drives",
    );
    await refresh.click();
    await expect.poll(() => started).toBe(true);
    await drawer.getByRole("button", { name: "History", exact: true }).click();
    release();
    expect((await delivered).status()).toBe(200);
    await expect(refresh).toBeEnabled();
  } finally {
    release();
    await page.unroute(pattern);
  }
}
