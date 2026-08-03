import { expect, test } from "@playwright/test";
import { signInAs } from "./staff-session";

test("history cancels a pending list read and visible refresh retries the requested record", async ({
  page,
}) => {
  await signInAs(page, "farah.aziz@example.invalid", "GeneralManager");
  await page.goto("/dashboard/intake?pageSize=10");
  const refresh = page.getByRole("button", { name: "Refresh worklist", exact: true });
  await expect(refresh).toBeEnabled();
  await page.getByRole("button", { name: "Next records", exact: true }).click();
  await expect(page).toHaveURL(/page=2/);
  await refresh.click();
  await expect(refresh).toBeEnabled();
  const rows = await page.locator("tbody tr").allTextContents();
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let started = false;
  let finished = false;
  await page.route("**/api/staff/intake/appointments?**", async (route) => {
    const response = await route.fetch();
    started = true;
    await gate;
    try {
      await route.fulfill({ response });
    } catch {
      // Superseded reads may have already closed their browser request.
    } finally {
      finished = true;
    }
  });
  try {
    await page.evaluate(() => window.history.back());
    await expect.poll(() => started).toBe(true);
    await page.evaluate(() => window.history.forward());
    await expect(page).toHaveURL(/page=2/);
    await expect(refresh).toBeEnabled();
  } finally {
    release();
  }
  await expect.poll(() => finished).toBe(true);
  await expect(page.locator("tbody tr")).toHaveText(rows);
  await page.unroute("**/api/staff/intake/appointments?**");

  const dialog = page.getByRole("dialog", { name: "Intake appointment", exact: true });
  await page.locator("tbody tr").first().click();
  const detailRefresh = dialog.getByRole("button", { name: "Refresh appointment", exact: true });
  await expect(detailRefresh).toBeEnabled();
  const firstHref = page.url();
  await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();
  await page.locator("tbody tr").nth(1).click();
  await expect(detailRefresh).toBeEnabled();
  const secondHref = page.url();
  await detailRefresh.click();
  await expect(detailRefresh).toBeEnabled();
  const retained = await dialog
    .getByRole("region", { name: "Appointment time and status", exact: true })
    .textContent();
  const target = `/api/staff/intake/appointments/${new URL(firstHref).pathname.split("/").at(-1)}`;
  await page.route(`**${target}`, (route) =>
    route.fulfill({
      status: 502,
      json: { status: 502, data: null, message: "Controlled traversal failure", uncertain: true },
    }),
  );
  await page.evaluate(() => window.history.go(-2));
  await expect(
    page.getByRole("alert").filter({ hasText: "The record could not be refreshed" }),
  ).toBeVisible();
  await expect(page).toHaveURL(firstHref);
  await expect(detailRefresh).toBeEnabled();
  expect(
    await dialog
      .getByRole("region", { name: "Appointment time and status", exact: true })
      .textContent(),
  ).toBe(retained);
  await page.unroute(`**${target}`);
  const retried = page.waitForRequest((request) => new URL(request.url()).pathname === target);
  await detailRefresh.click();
  await retried;
  await expect(detailRefresh).toBeEnabled();
  await expect(page).toHaveURL(firstHref);
  expect(firstHref).not.toBe(secondHref);
});

test("a delayed list confirmation preserves the agent's newer record selection", async ({
  page,
}) => {
  await signInAs(page, "aina.sofia@example.invalid", "SalesAgent");
  await page.goto("/dashboard/intake?queue=history");
  const queue = page.getByRole("complementary", { name: "Intake queues", exact: true });
  await expect(queue.locator("li button").nth(1)).toBeVisible();
  await queue.locator("li button").first().click();
  const refresh = page.getByRole("button", { name: "Refresh appointment", exact: true });
  await expect(refresh).toBeEnabled();
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let started = false;
  let finished = false;
  await page.route("**/api/staff/intake/appointments?**", async (route) => {
    const response = await route.fetch();
    started = true;
    await gate;
    await route.fulfill({ response });
    finished = true;
  });
  try {
    await queue.getByRole("searchbox").fill("VCA");
    await queue.getByRole("searchbox").press("Enter");
    await expect.poll(() => started).toBe(true);
    const previousPath = new URL(page.url()).pathname;
    await queue.locator("li button").nth(1).click();
    await expect.poll(() => new URL(page.url()).pathname).not.toBe(previousPath);
    await expect(refresh).toBeEnabled();
    const selectedPath = new URL(page.url()).pathname;
    release();
    await expect.poll(() => finished).toBe(true);
    await expect(page).toHaveURL(new RegExp(`${selectedPath}\\?.*search=VCA`));
  } finally {
    release();
    await page.unroute("**/api/staff/intake/appointments?**");
  }
});

test("a failed post-write queue reload does not replace the next user history entry", async ({
  page,
}) => {
  await signInAs(page, "aina.sofia@example.invalid", "SalesAgent");
  await page.goto("/dashboard/intake");
  const queue = page.getByRole("complementary", { name: "Intake queues", exact: true });
  await queue.getByRole("button", { name: /^VCA 9701 / }).click();
  const content = page.getByRole("region", { name: "Selected appointment", exact: true });
  await expect(
    content.getByRole("button", { name: "Release appointment", exact: true }),
  ).toBeEnabled();
  await page.route("**/api/staff/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        message: "Controlled post-write reload failure",
        uncertain: true,
      },
    }),
  );
  await content.getByRole("button", { name: "Release appointment", exact: true }).click();
  await page
    .getByRole("dialog", { name: "Release this appointment?", exact: true })
    .getByRole("button", { name: "Release appointment", exact: true })
    .click();
  await expect(
    page.getByRole("alert").filter({ hasText: "The worklist could not be refreshed" }),
  ).toBeVisible();
  await page.unroute("**/api/staff/intake/appointments?**");
  const length = await page.evaluate(() => window.history.length);
  await queue.getByRole("button", { name: /^History\b/ }).click();
  await expect(page).toHaveURL(/queue=history/);
  expect(await page.evaluate(() => window.history.length)).toBe(length + 1);
});
