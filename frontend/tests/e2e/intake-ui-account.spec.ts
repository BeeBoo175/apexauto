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
  await page.getByRole("link", { name: "Selling appointments", exact: true }).click();

  await expect(page.getByRole("button", { name: "Apply dates", exact: true })).toBeEnabled();
  expect(
    (counts()["/api/intake/appointments"] ?? 0) - (before["/api/intake/appointments"] ?? 0),
  ).toBe(1);
  const appointments = page.getByRole("region", { name: "Your appointments", exact: true });
  const records = appointments.getByRole("heading", { level: 3 });
  const historySummary = page.getByText(/^\d+ appointments · Page 1/);
  const total = Number((await historySummary.innerText()).match(/^(\d+) appointments/)![1]);
  const pageSize = 5;
  const finalPage = Math.ceil(total / pageSize);
  expect(finalPage).toBeGreaterThan(1);
  expect(total % pageSize).toBeGreaterThan(0);
  await expect(records).toHaveCount(pageSize);
  await page.getByRole("button", { name: "Next appointments", exact: true }).click();
  await expect(
    page.getByText(`${total} appointments · Page 2 · All dates`, { exact: true }),
  ).toBeVisible();
  await expect(records).toHaveCount(Math.min(pageSize, total - pageSize));
  await page.goto(`/account?page=${finalPage}#appointments`);
  await expect(records).toHaveCount(total - (finalPage - 1) * pageSize);
  await expect(page.getByRole("button", { name: "Next appointments", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Previous appointments", exact: true }).click();
  await expect(
    page.getByText(`${total} appointments · Page ${finalPage - 1} · All dates`, { exact: true }),
  ).toBeVisible();
  await page.goto("/account#appointments");
  await expect(records).toHaveCount(pageSize);
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
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({
      hasText:
        "Appointment history could not be refreshed. The last confirmed records and filters remain shown.",
    }),
  ).toBeVisible();
  await expect(records).toHaveCount(pageSize);
  await page.unroute("**/api/intake/appointments?**");
  await appointments.getByLabel("From (Malaysia)", { exact: true }).fill("2026-01-01");
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(
    page.getByText("Choose both From and To dates, or leave both empty.", { exact: true }).first(),
  ).toBeVisible();
  await expect(records).toHaveCount(pageSize);
  await appointments.getByLabel("To (Malaysia)", { exact: true }).fill("2026-01-01");
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(page.getByText("No appointments match these filters.")).toBeVisible();
  await appointments.getByLabel("From (Malaysia)", { exact: true }).fill("");
  await appointments.getByLabel("To (Malaysia)", { exact: true }).fill("");
  await page.getByRole("button", { name: "Apply dates", exact: true }).click();
  await expect(records).toHaveCount(pageSize);
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
  await expect(
    dialog.getByRole("button", { name: "Keep appointment", exact: true }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Retry same cancellation", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[0]).toBe(requests[1]);
  await expect(page.getByText("Cancelled", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Apply dates", exact: true })).toBeEnabled();

  await expect(page.getByText("Request confirmed.", { exact: true })).toBeVisible();
  await page.screenshot({ path: path.join(evidence, "account-appointments.png") });
});
