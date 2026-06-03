import { expect, test, type Page } from "@playwright/test";

test("failed dashboard entry retains recovery and progress until a deliberate retry commits", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("farah.aziz@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  await expectEntryRecovery(page);
  expect(errors).toEqual([]);
});

async function expectEntryRecovery(page: Page) {
  const calendarRoute = "**/api/staff/intake/appointments?**";
  await page.route(calendarRoute, (route) => route.abort("failed"));
  await page.goto("/dashboard/intake");
  const retry = page.getByRole("button", { name: "Retry appointments", exact: true });
  await expect(retry).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
  await page.unroute(calendarRoute);

  let release = () => {};
  let observed = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const requested = new Promise<void>((resolve) => {
    observed = resolve;
  });
  await page.route(calendarRoute, async (route) => {
    const response = await route.fetch();
    observed();
    await gate;
    await route.fulfill({ response });
  });
  try {
    await retry.click();
    await requested;
    await expect(retry).toBeVisible();
    await expect(page.getByRole("progressbar")).toBeVisible();
    await expect(
      page.getByRole("status", { name: "Loading workspace content", exact: true }),
    ).toHaveCount(0);
  } finally {
    release();
  }
  await expect(
    page.getByRole("table", { name: "Scoped intake appointments", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
  await page.unroute(calendarRoute);
}
