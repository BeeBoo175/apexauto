import { expect, test } from "@playwright/test";
import { calendarDate, calendarRange } from "../../src/lib/calendar";
import type { WorklistItem } from "../../src/lib/staff/contracts";

const branchId = "c77708bf-1a31-42f5-9a69-0c8766697a52";

for (const actor of [
  { name: "branch", email: "aina.sofia@example.invalid", branch: "", status: "CheckedIn" },
  {
    name: "group filtered to a branch",
    email: "farah.aziz@example.invalid",
    branch: branchId,
    status: "Cancelled",
  },
]) {
  test(`${actor.name} view switches retain ${actor.status} records and applied scope`, async ({
    page,
    context,
  }) => {
    test.setTimeout(120000);
    await page.goto("/login");
    await page.getByLabel("Email", { exact: true }).fill(actor.email);
    await page.getByLabel("Password", { exact: true }).fill("Password123!");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/dashboard\/intake$/);
    const session = (await context.cookies()).find((cookie) => cookie.name === "apexauto_token")!;
    const headers = { Cookie: `apexauto_token=${session.value}` };
    const date = calendarDate();
    const range = calendarRange(date);

    const status = actor.status;
    const query = new URLSearchParams({ ...range, status });
    if (actor.branch) {
      query.set("branchId", actor.branch);
    }
    const response = await context.request.get(`/api/staff/intake/calendar?${query}`, {
      headers,
    });
    expect(response.status()).toBe(200);
    const snapshot = (await response.json()).data as { items: WorklistItem[]; total: number };
    expect(snapshot.total).toBeGreaterThan(0);
    await page.goto(`/dashboard/intake?view=calendar&date=${date}&${query}`);
    await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
    await page.getByRole("link", { name: "Table", exact: true }).click();
    await expect(page.getByRole("button", { name: "Refresh worklist", exact: true })).toBeEnabled();
    const tableUrl = new URL(page.url());
    expect(tableUrl.searchParams.get("from")).toBe(range.from);
    expect(tableUrl.searchParams.get("to")).toBe(range.to);
    expect(tableUrl.searchParams.get("status") ?? "All").toBe(status);
    if (actor.branch) {
      expect(tableUrl.searchParams.get("branchId")).toBe(actor.branch);
    }
    const table: WorklistItem[] = [];
    let nextPage = 1;
    do {
      const result = await context.request.get(
        `/api/staff/intake/appointments?${query}&page=${nextPage}&pageSize=50`,
        { headers },
      );
      expect(result.status()).toBe(200);
      const body = (await result.json()).data;
      expect(body.total).toBe(snapshot.total);
      expect(body.items.length).toBeGreaterThan(0);
      table.push(...body.items);
      nextPage += 1;
    } while (table.length < snapshot.total);
    expect(table.map((item) => item.id).sort()).toEqual(
      snapshot.items.map((item) => item.id).sort(),
    );
    const row = snapshot.items.find((item) => item.status === status)!;
    expect(row).toBeTruthy();
    await expect(
      page.getByRole("link", { name: `Open ${row.registrationPlate}`, exact: true }),
    ).toBeVisible();
    await page.getByRole("link", { name: "Calendar", exact: true }).click();
    await expect(page.getByRole("button", { name: "Refresh calendar", exact: true })).toBeEnabled();
    expect(new URL(page.url()).searchParams.get("date")).toBe(range.from);
  });
}
