import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

test("owned history keeps scoped pages and confirmed content, then reconciles cancellation", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill("ahmad.faizal@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/account$/);
  await expect(page.getByRole("link", { name: "Account", exact: true })).toBeVisible();
  const before = counts();

  const initial = await page.goto("/account");
  expect(await initial!.text()).toContain("Your appointments");

  await expect(
    page.getByRole("button", { name: "Refresh appointments", exact: true }),
  ).toBeEnabled();
  expect(
    (counts()["/api/intake/appointments"] ?? 0) - (before["/api/intake/appointments"] ?? 0),
  ).toBe(1);
  const records = page
    .getByRole("region", { name: "Your appointments", exact: true })
    .getByRole("heading", { level: 3 });
  const historySummary = page.getByText(/^\d+ appointments · Page 1/);
  const total = Number((await historySummary.innerText()).match(/^(\d+) appointments/)![1]);
  const pageSize = 25;
  const finalPage = Math.ceil(total / pageSize);
  expect(finalPage).toBeGreaterThan(1);
  expect(total % pageSize).toBeGreaterThan(0);
  await expect(records).toHaveCount(25);
  for (let current = 2; current <= finalPage; current++) {
    await page.getByRole("button", { name: "Next appointments", exact: true }).click();
    await expect(
      page.getByText(`${total} appointments · Page ${current} · All dates`, { exact: true }),
    ).toBeVisible();
    await expect(records).toHaveCount(Math.min(pageSize, total - (current - 1) * pageSize));
  }
  await expect(page.getByRole("button", { name: "Next appointments", exact: true })).toBeDisabled();
  for (let current = finalPage - 1; current >= 1; current--) {
    await page.getByRole("button", { name: "Previous appointments", exact: true }).click();
    await expect(
      page.getByText(`${total} appointments · Page ${current} · All dates`, { exact: true }),
    ).toBeVisible();
    await expect(records).toHaveCount(pageSize);
  }
  await expect(
    page.getByRole("button", { name: "Previous appointments", exact: true }),
  ).toBeDisabled();

  await page.route("**/api/intake/appointments?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled failed history refresh",
      },
    }),
  );
  await page.getByRole("button", { name: "Refresh appointments", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({
      hasText:
        "Appointment history could not be refreshed. The last confirmed records and filters remain shown.",
    }),
  ).toBeVisible();
  await expect(records).toHaveCount(25);
  await page.unroute("**/api/intake/appointments?**");
  await page.getByLabel("From (Malaysia)", { exact: true }).fill("2026-01-01");
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(
    page.getByText("Choose both From and To dates, or leave both empty.", { exact: true }).first(),
  ).toBeVisible();
  await expect(records).toHaveCount(25);
  await page.getByLabel("To (Malaysia)", { exact: true }).fill("2026-01-01");
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(page.getByText("No appointments match these filters.")).toBeVisible();
  await page.getByLabel("From (Malaysia)", { exact: true }).fill("");
  await page.getByLabel("To (Malaysia)", { exact: true }).fill("");
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(records).toHaveCount(25);
  let loseReceipt = true;
  const requests: string[] = [];
  await page.route("**/api/intake/appointments/*/cancellations", async (route) => {
    requests.push(`${route.request().url()} ${route.request().postData()}`);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (loseReceipt) {
      loseReceipt = false;
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          uncertain: true,
          message: "Controlled lost cancellation receipt",
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await page.getByRole("button", { name: "Request cancellation", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Cancel this appointment?" });
  await dialog.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
  await expect(dialog.getByRole("button", { name: "Close dialog", exact: true })).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Retry same cancellation", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[0]).toBe(requests[1]);
  await expect(page.getByText("Cancelled", { exact: true }).first()).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Refresh appointments", exact: true }),
  ).toBeEnabled();

  await expect(page.getByText("Request confirmed.", { exact: true })).toHaveCount(0);
});
