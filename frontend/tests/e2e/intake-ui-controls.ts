import { expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import path from "node:path";

export async function setAppointmentDates(page: Page, from: string, to: string) {
  const filters = page.getByRole("region", { name: "Inspection time", exact: true });
  if (!(await filters.isVisible())) {
    await page.getByRole("button", { name: /^Filter Inspection time/ }).click();
  }
  await filters.getByLabel("From", { exact: true }).fill(from);
  await filters.getByLabel("To", { exact: true }).fill(to);
}

export async function expectWorklistPage(page: Page, number: number) {
  await expect.poll(() => Number(new URL(page.url()).searchParams.get("page") ?? 1)).toBe(number);
}

export async function showIntakeQueue(page: Page) {
  const show = page.getByRole("button", { name: "Show queue", exact: true });
  if (await show.isVisible()) {
    await show.click();
  }
}

export async function openStaffWorkspace(page: Page, name: string, blocked = false) {
  const link = page.getByRole("link", { name, exact: true });
  if (!(await link.isVisible())) {
    await page.getByRole("button", { name: "Workspaces", exact: true }).click();
  }
  if (blocked) {
    await expect(link).toHaveAttribute("aria-disabled", "true");
  }
  await link.click({ force: blocked });
  if (blocked) {
    await closeStaffWorkspaces(page);
  }
}

export async function closeStaffWorkspaces(page: Page) {
  const close = page.getByRole("button", { name: "Close workspaces", exact: true });
  if (await close.isVisible()) {
    await close.click();
  }
}

export async function hideIntakeQueue(page: Page) {
  const hide = page.getByRole("button", { name: "Hide queue", exact: true });
  if (await hide.isVisible()) {
    await hide.click();
  }
}

export async function captureIntake(page: Page, name: string) {
  await expect(page.locator('[aria-label="Notifications"] > *')).toHaveCount(0);
  await hideIntakeQueue(page);
  await page
    .getByRole("region", { name: "Selected appointment", exact: true })
    .evaluateAll((items) => {
      items.forEach((item) => item.scrollTo({ top: 0 }));
    });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: path.join(process.env.BROWSER_EVIDENCE_DIR!, `${name}.png`),
    fullPage: false,
  });
}

export async function reviewAgentQueue(page: Page, area: "sales" | "inspection") {
  await page.goto(`/dashboard/${area}`);
  const label = area === "sales" ? "Sales queue tabs" : "Workshop worklists";
  await expect(page.getByRole("navigation", { name: label, exact: true })).toBeVisible();
  const tab = page.getByRole("button", { name: area === "sales" ? /^Handling\b/ : /^To claim\b/ });
  await tab.click();
  await expect(tab).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByRole("searchbox")).toBeVisible();
  const jobs = page.locator("ul li button");
  await expect(jobs.first()).toBeVisible();
  const identities = await jobs.allTextContents();
  const next = page.getByRole("button", { name: "Next records", exact: true });
  if (await next.isEnabled()) {
    await next.click();
    await expect(page.getByLabel("Page number", { exact: true })).toHaveValue("2");
    expect(await jobs.allTextContents()).not.toEqual(identities);
  }
  await captureIntake(page, `${area}-queue`);
}

export async function exerciseOwnership(page: Page) {
  const content = page.getByRole("region", { name: "Selected appointment", exact: true });
  await content
    .locator("footer")
    .getByRole("button", { name: "Release appointment", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "Release this appointment?", exact: true })
    .getByRole("button", { name: "Release appointment", exact: true })
    .click();
  await expect(
    content.locator("footer").getByRole("button", { name: "Claim appointment", exact: true }),
  ).toBeEnabled();
  await expect(content.getByLabel("VIN", { exact: true })).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: /^To claim\b/, includeHidden: true }),
  ).toHaveAttribute("aria-pressed", "true");
  const unassigned = new URL(page.url());
  unassigned.searchParams.delete("queue");
  await page.goto(unassigned.toString());
  await expect(
    content.locator("footer").getByRole("button", { name: "Claim appointment", exact: true }),
  ).toBeEnabled();
  await expect(
    page.getByRole("button", { name: /^To claim\b/, includeHidden: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await captureIntake(page, "agent-unassigned");
  const bodies: string[] = [];
  await page.route("**/api/staff/intake/appointments/*/claims", async (route) => {
    bodies.push(route.request().postData()!);
    const response = await route.fetch();
    expect(response.status()).toBe(200);
    if (bodies.length === 1) {
      await route.fulfill({
        status: 502,
        json: {
          status: 502,
          data: null,
          uncertain: true,
          message: "Controlled lost ownership receipt",
        },
      });
      return;
    }
    await route.fulfill({ response });
  });
  await content
    .locator("footer")
    .getByRole("button", { name: "Claim appointment", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "Claim this appointment?", exact: true })
    .getByRole("button", { name: "Claim appointment", exact: true })
    .click();
  await expect(
    content.locator("footer").getByRole("button", { name: "Reconcile ownership", exact: true }),
  ).toBeVisible();
  await openStaffWorkspace(page, "Work calendar", true);
  await expect(content).toBeVisible();
  await content
    .locator("footer")
    .getByRole("button", { name: "Reconcile ownership", exact: true })
    .click();
  await expect(content.getByLabel("VIN", { exact: true })).toBeEditable();
  await expect(
    page.getByRole("button", { name: /^My work\b/, includeHidden: true }),
  ).toHaveAttribute("aria-pressed", "true");
  expect(bodies).toHaveLength(2);
  expect(bodies[1]).toBe(bodies[0]);
  await page.unroute("**/api/staff/intake/appointments/*/claims");
  await page.evaluate(() => {
    window.history.pushState(window.history.state, "", window.location.href);
  });
}

export async function exerciseManagerHandler(page: Page) {
  const handler = "aina.sofia@example.invalid";
  const sort = page.getByRole("button", { name: "Sort by Handler", exact: true });
  const header = page.getByRole("columnheader").filter({ has: sort });
  await page.getByRole("button", { name: /^Filter Handler/ }).click();
  await page.getByLabel("Handler email contains", { exact: true }).fill(handler.toUpperCase());
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("handler"))
    .toBe(handler.toUpperCase());
  const rows = page.locator("tbody tr");
  expect(await rows.count()).toBeGreaterThan(0);
  for (const row of await rows.all()) {
    await expect(row.getByRole("cell").nth(5)).toHaveText(handler);
  }
  await page.getByRole("button", { name: /^Remove handler filter:/ }).click();
  await expect.poll(() => new URL(page.url()).searchParams.has("handler")).toBe(false);
  await sort.click();
  await expect.poll(() => new URL(page.url()).searchParams.get("sort")).toBe("handlerEmail");
  await expect(header).toHaveAttribute("aria-sort", "ascending");
  await sort.click();
  await expect(header).toHaveAttribute("aria-sort", "descending");
  await page.getByRole("button", { name: /^Filter Handler/ }).click();
  await page.getByLabel("Handler email contains", { exact: true }).fill(handler);
  await page.getByRole("button", { name: "Apply", exact: true }).click();
  await expect.poll(() => new URL(page.url()).searchParams.get("handler")).toBe(handler);
  await page.getByRole("checkbox", { name: "Select all loaded rows", exact: true }).check();
  for (const name of ["Export selected CSV", "Export full CSV"]) {
    const download = page.waitForEvent("download");
    await page.getByRole("button", { name, exact: true }).click();
    const file = await (await download).path();
    const csv = readFileSync(file!, "utf8");
    expect(csv.split("\r\n")[0]).toContain('"Handler","Handler eligible"');
    expect(csv).toContain(`"${handler}"`);
    expect(csv).not.toContain('"siti.aminah@example.invalid"');
    if (name === "Export selected CSV") {
      await page.getByRole("button", { name: "Clear selection", exact: true }).click();
    }
  }
  await page.getByRole("button", { name: "Reset filters", exact: true }).click();
  await expect.poll(() => new URL(page.url()).searchParams.has("handler")).toBe(false);
  await expect(header).toHaveAttribute("aria-sort", "descending");
  await captureIntake(page, "manager-handler-table");
}
