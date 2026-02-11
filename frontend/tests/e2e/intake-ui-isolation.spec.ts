import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.AUTH_EVIDENCE_DIR!;

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
  await page.route("**/api/intake/appointments/*/cancellation", async (route) => {
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
