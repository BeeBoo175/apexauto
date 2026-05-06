import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;

test("changed customer cookies clear prior snapshots and unavailable session reads keep the cookie", async ({
  page,
  context,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("ahmad.faizal@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/account$/);
  await expect(page.getByText("Ahmad Faizal Hassan", { exact: true })).toBeVisible();
  await page.getByLabel("From (Malaysia)", { exact: true }).fill("2026-01-01");
  await page.route("**/api/intake/appointments/*/cancellations", async (route) => {
    const response = await route.fetch();
    if (response.status() !== 200) {
      await route.fulfill({ response });
      return;
    }
    await route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled lost cancellation receipt",
      },
    });
  });
  await page.getByRole("button", { name: "Request cancellation", exact: true }).first().click();
  await page.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Retry same cancellation", exact: true }),
  ).toBeVisible();
  let unloadPrompts = 0;
  page.on("dialog", async (dialog) => {
    unloadPrompts++;
    await dialog.dismiss();
  });
  const origin = new URL(page.url()).origin;
  const changed = await context.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: {
      loginKey: crypto.randomUUID(),
      email: "tan.hui.min@example.invalid",
      password: "Password123!",
    },
  });
  expect(changed.status()).toBe(200);
  await page.getByRole("button", { name: "Retry same cancellation", exact: true }).click();
  await expect(page.getByText("Tan Hui Min", { exact: true })).toBeVisible();
  await expect(page.getByText("Ahmad Faizal Hassan", { exact: true })).toHaveCount(0);
  await expect(page.getByLabel("From (Malaysia)", { exact: true })).toHaveValue("");
  expect(unloadPrompts).toBe(0);
  const cookie = (await context.cookies()).find((entry) => entry.name === "apexauto_token")!;
  writeFileSync(path.join(evidence, "fault.txt"), "unavailable");
  try {
    await page.goto("/login");
    await expect(
      page.getByRole("heading", { name: "Check your session", exact: true }),
    ).toBeVisible();
    expect((await context.cookies()).some((entry) => entry.value === cookie.value)).toBe(true);
  } finally {
    writeFileSync(path.join(evidence, "fault.txt"), "");
  }
  await page.getByRole("button", { name: "Try checking session again", exact: true }).click();
  await expect(page).toHaveURL(/\/account$/);
  await expect(page.getByText("Tan Hui Min", { exact: true })).toBeVisible();
  const other = await context.newPage();
  await other.goto("/account");
  await other.getByRole("button", { name: "Sign out", exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByText("Tan Hui Min", { exact: true })).toHaveCount(0);
});

for (const navigation of [false, true]) {
  test(`a delayed ${navigation ? "destination" : "initial"} staff read cannot restore private content after sign out`, async ({
    page,
    context,
  }) => {
    await page.goto("/login");
    await page
      .getByLabel("Email", { exact: true })
      .fill(navigation ? "farah.aziz@example.invalid" : "aina.sofia@example.invalid");
    await page.getByLabel("Password", { exact: true }).fill("Password123!");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();

    let release = () => {};
    let observed = () => {};
    let completed = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const requested = new Promise<void>((resolve) => {
      observed = resolve;
    });
    const handled = new Promise<void>((resolve) => {
      completed = resolve;
    });
    const routePattern = navigation
      ? "**/api/staff/listings?**"
      : "**/api/staff/intake/appointments?**";
    await page.route(routePattern, async (route) => {
      const response = await route.fetch();
      observed();
      await gate;
      await route.fulfill({ response }).catch(() => {});
      completed();
    });
    const other = await context.newPage();
    try {
      if (navigation) {
        const listings = page.getByRole("link", { name: "Listings", exact: true });
        if (!(await listings.isVisible())) {
          await page.getByRole("button", { name: "Workspaces", exact: true }).click();
        }
        await listings.click();
        await requested;
        await expect(
          page.getByRole("region", { name: "Intake calendar", exact: true }),
        ).toBeVisible();
      } else {
        const document = await page.goto("/dashboard/intake?view=list");
        expect(document!.headers()["cache-control"]).toContain("no-store");
        await requested;
        await expect(
          page.getByRole("status", { name: "Loading workspace content", exact: true }),
        ).toBeVisible();
      }
      await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toHaveCount(0);
      await other.goto("/dashboard/intake");
      await expect(
        other.getByRole("button", { name: "Refresh calendar", exact: true }),
      ).toBeEnabled();
      const signOut = other.getByRole("button", { name: "Sign out", exact: true });
      if (!(await signOut.isVisible())) {
        await other.getByRole("button", { name: "Workspaces", exact: true }).click();
      }
      await signOut.click();
      await expect(other).toHaveURL(/\/login$/);
      await expect(page).toHaveURL(/\/login(?:\?|$)/);
      release();
      await handled;
      await expect(page.getByRole("table", { name: "Scoped intake appointments" })).toHaveCount(0);
      await expect(page.getByLabel("VIN", { exact: true })).toHaveCount(0);
      await expect(page.getByRole("region", { name: "Intake calendar", exact: true })).toHaveCount(
        0,
      );
      await expect(page.getByRole("button", { name: "Refresh listings", exact: true })).toHaveCount(
        0,
      );
      for (const endpoint of [
        "/api/staff/intake/appointments",
        "/api/staff/listings",
        "/api/staff/sales",
        "/api/staff/inspection/workshop",
      ]) {
        const denied = await context.request.get(endpoint);
        expect(denied.status()).toBe(401);
        expect(denied.headers()["cache-control"]).toContain("no-store");
      }
    } finally {
      release();
      await page.unroute(routePattern);
      await other.close();
    }
  });
}
