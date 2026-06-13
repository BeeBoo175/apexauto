import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

test.use({ actionTimeout: 15_000 });

test("test-drive selection retains drafts on failed or mismatched reads and clears denied or closed details", async ({
  page,
  context,
}, info) => {
  test.setTimeout(120_000);
  const login = await context.request.post("/api/auth/login", {
    headers: { Origin: process.env.APP_BASE_URL! },
    data: {
      loginKey: crypto.randomUUID(),
      email: "lim.mei.ling@example.invalid",
      password: "Password123!",
    },
  });
  expect(login.status()).toBe(200);
  const rows = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === "/api/staff/test-drives" && response.status() === 200,
  );
  await page.goto("/dashboard/test-drives?status=Booked");
  const snapshot = (await (await rows).json()).data;
  expect(snapshot.items.length).toBeGreaterThan(0);
  const booking = snapshot.items[0];
  const table = page.getByRole("table", { name: "Scoped test drives", exact: true });
  const row = table.getByRole("row").filter({ hasText: booking.registrationPlate }).first();
  await row.click();
  const dialog = page.getByRole("dialog", { name: "Test drive", exact: true });
  const record = dialog.getByRole("region", { name: "Selected test drive", exact: true });
  await expect(record).toContainText(booking.id);
  await expect(page).toHaveURL(new RegExp(`booking=${booking.id}`));
  await record.getByLabel("Test drive action", { exact: true }).selectOption("cancellations");
  await record.getByLabel("Reason", { exact: true }).fill("Retain this unsaved reason");
  const refresh = dialog.getByRole("button", { name: "Refresh test drives", exact: true });
  const pattern = `**/api/staff/test-drives/${booking.id}`;
  const failure = page.getByRole("alert").filter({ hasText: "The test drive could not be opened" });

  for (const mode of ["unavailable", "actor"] as const) {
    await page.route(pattern, async (route) => {
      if (mode === "unavailable") {
        await route.fulfill({
          status: 502,
          json: { status: 502, data: null, message: "Controlled read failure", uncertain: true },
        });
        return;
      }
      const response = await route.fetch();
      const envelope = await response.json();
      envelope.data.actor.userId = crypto.randomUUID();
      envelope.data.customerEmail = "another.actor@example.invalid";
      await route.fulfill({ response, json: envelope });
    });
    await refresh.click();
    await expect(failure.last()).toBeVisible();
    await expect(refresh).toBeEnabled();
    await expect(record.getByLabel("Reason", { exact: true })).toHaveValue(
      "Retain this unsaved reason",
    );
    await expect(record).not.toContainText("another.actor@example.invalid");
    await expect(record).toContainText(booking.id);
    await page.unroute(pattern);
    await refresh.click();
    await expect(refresh).toBeEnabled();
  }

  await record.getByRole("button", { name: "Discard changes", exact: true }).click();
  await page.route(pattern, (route) =>
    route.fulfill({
      status: 403,
      json: { status: 403, data: null, message: "Controlled denial", uncertain: false },
    }),
  );
  await refresh.click();
  await expect(record).toHaveCount(0);
  await expect(page).toHaveURL(new RegExp(`booking=${booking.id}`));
  await page.unroute(pattern);
  await dialog.getByRole("button", { name: "Retry selected test drive", exact: true }).click();
  await expect(record).toContainText(booking.id);

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
  await page.route(pattern, async (route) => {
    const response = await route.fetch();
    observed();
    await gate;
    await route.fulfill({ response }).catch(() => {});
    completed();
  });
  try {
    await refresh.click();
    await requested;
    await dialog.getByRole("button", { name: "Close drawer", exact: true }).click();
    await expect(dialog).toBeHidden();
    expect(new URL(page.url()).searchParams.has("booking")).toBe(false);
    release();
    await handled;
    await expect(dialog).toBeHidden();
    await page.unroute(pattern);
    await row.click();
    await expect(record).toContainText(booking.id);
    await page.screenshot({
      path: path.join(process.env.BROWSER_EVIDENCE_DIR!, `selection-${info.project.name}.png`),
    });
    writeFileSync(
      path.join(process.env.BROWSER_EVIDENCE_DIR!, `selection-${info.project.name}.json`),
      JSON.stringify({
        bookingId: booking.id,
        failureRetainedDraft: true,
        denialCleared: true,
        closedReadIgnored: true,
      }),
    );
  } finally {
    release();
    await page.unroute(pattern);
  }
});
