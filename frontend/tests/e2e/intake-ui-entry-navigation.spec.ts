import { expect, test, type Page } from "@playwright/test";

test("workspace navigation retains ready content through delayed reads, history and interruption", async ({
  page,
}) => {
  test.setTimeout(120_000);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("farah.aziz@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
  for (const destination of ["Reservations", "Listings"]) {
    if (new URL(page.url()).pathname !== "/dashboard/intake") {
      const intake = page.getByRole("link", { name: "Intake appointments", exact: true });
      if (!(await intake.isVisible())) {
        await page.getByRole("button", { name: "Workspaces", exact: true }).click();
      }
      await intake.click();
      await expect(
        page.getByRole("button", { name: "Refresh worklist", exact: true }),
      ).toBeEnabled();
      const publicSite = page.getByRole("link", { name: "View public site", exact: true });
      if (!(await publicSite.isVisible())) {
        await page.getByRole("button", { name: "Workspaces", exact: true }).click();
      }
      await publicSite.click();
      await expect(page).toHaveURL(/\/$/);
      await page.goBack();
    }
    await expectReadyNavigation(page, destination);
  }
  expect(errors).toEqual([]);
});

async function expectReadyNavigation(page: Page, destination: string) {
  const calendar = page.getByRole("table", { name: "Scoped intake appointments", exact: true });
  await expect(calendar).toBeVisible();
  let release = () => {};
  let observed = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const requested = new Promise<void>((resolve) => {
    observed = resolve;
  });
  await page.route("**/api/staff/listings?**", async (route) => {
    const response = await route.fetch();
    observed();
    await gate;
    await route.fulfill({ response });
  });
  try {
    const listings = page.getByRole("link", { name: "Listings", exact: true });
    if (!(await listings.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    await listings.click();
    await requested;
    await expect(calendar).toBeVisible();
    await expect(page.getByRole("heading", { name: "Listings", exact: true })).toBeVisible();
  } finally {
    release();
  }
  await expect(page.getByRole("button", { name: "Refresh listings", exact: true })).toBeEnabled();
  await expect(calendar).toHaveCount(0);

  let releaseHistory = () => {};
  let observedHistory = () => {};
  const historyGate = new Promise<void>((resolve) => {
    releaseHistory = resolve;
  });
  const historyRequested = new Promise<void>((resolve) => {
    observedHistory = resolve;
  });
  await page.route("**/api/staff/intake/appointments?**", async (route) => {
    const response = await route.fetch();
    observedHistory();
    await historyGate;
    await route.fulfill({ response }).catch(() => {});
  });
  try {
    await page.goBack();
    await historyRequested;
    await expect(page.getByRole("button", { name: "Refresh listings", exact: true })).toBeVisible();
    await expect(
      page.getByRole("status", { name: "Loading workspace content", exact: true }),
    ).toHaveCount(0);
    const reservations = page.getByRole("link", { name: destination, exact: true });
    if (!(await reservations.isVisible())) {
      await page.getByRole("button", { name: "Workspaces", exact: true }).click();
    }
    const aborted = page.waitForEvent("requestfailed", (request) =>
      request.url().includes("/api/staff/intake/appointments?"),
    );
    await reservations.click();
    await aborted;
    await expect(
      page.getByRole("button", { name: `Refresh ${destination.toLowerCase()}`, exact: true }),
    ).toBeEnabled();
  } finally {
    releaseHistory();
  }
  await expect(page.getByRole("heading", { name: destination, exact: true })).toBeVisible();
  await expect(page.getByRole("progressbar")).toBeHidden();
  await expect(calendar).toHaveCount(0);
}
