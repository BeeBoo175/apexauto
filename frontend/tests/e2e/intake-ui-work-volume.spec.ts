import { expect, test, type Page } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";

test.use({ actionTimeout: 15_000 });
const branchId = "c77708bf-1a31-42f5-9a69-0c8766697a52";

type WorkItem = {
  id: string;
  kind: string;
  registrationPlate: string;
  branchId: string;
  handlerEmail: string;
};

type WorkPage = { items: WorkItem[]; total: number; page: number };

async function login(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard\/intake$/);
}

async function readWork(page: Page, action: () => Promise<unknown>): Promise<WorkPage> {
  const response = page.waitForResponse(
    (value) =>
      new URL(value.url()).pathname === "/api/staff/work-calendar/unscheduled" &&
      value.status() === 200,
  );
  await action();
  return (await (await response).json()).data;
}

async function collectPages(page: Page, first: WorkPage) {
  const work = page.getByRole("region", { name: "Unscheduled work", exact: true });
  const items: WorkItem[] = [];
  let current = first;
  for (let index = 1; index <= Math.ceil(first.total / 25); index++) {
    expect(current.page).toBe(index);
    expect(current.total).toBe(first.total);
    expect(current.items).toHaveLength(Math.min(25, first.total - items.length));
    await expect(work.getByLabel("Page number", { exact: true })).toHaveValue(String(index));
    await expect(work.locator("ul li a")).toHaveCount(current.items.length);
    expect(await work.locator("ul li a strong").allTextContents()).toEqual(
      current.items.map((item) => item.registrationPlate),
    );
    items.push(...current.items);
    if (items.length < first.total) {
      current = await readWork(page, () =>
        work.getByRole("button", { name: "Next records", exact: true }).click(),
      );
    }
  }
  expect(new Set(items.map((item) => `${item.kind}:${item.id}`)).size).toBe(first.total);
  expect(items.map((item) => `${item.registrationPlate}:${item.kind}:${item.id}`)).toEqual(
    items.map((item) => `${item.registrationPlate}:${item.kind}:${item.id}`).sort(),
  );
  await expect(work.getByRole("button", { name: "Next records", exact: true })).toBeDisabled();
  return items;
}

test("agent unscheduled sales span full and partial pages and open the owned sale", async ({
  page,
}, info) => {
  await login(page, "aina.sofia@example.invalid");
  const first = await readWork(page, () => page.goto("/dashboard/work-calendar"));
  expect(first.total).toBe(53);
  const items = await collectPages(page, first);
  expect(items.every((item) => item.kind === "Sale" && item.branchId === branchId)).toBe(true);
  expect(items.every((item) => item.handlerEmail === "aina.sofia@example.invalid")).toBe(true);
  const work = page.getByRole("region", { name: "Unscheduled work", exact: true });
  const fresh = await readWork(page, () =>
    page.getByRole("button", { name: "Refresh work calendar", exact: true }).click(),
  );
  expect(fresh.items.map((item) => item.id)).toEqual(items.slice(50).map((item) => item.id));
  await expect(work.locator("ul li a")).toHaveCount(3);
  await page.screenshot({
    path: path.join(process.env.BROWSER_EVIDENCE_DIR!, `sales-volume-${info.project.name}.png`),
    fullPage: true,
  });
  const sale = fresh.items[0];
  const href = `/dashboard/sales?sale=${sale.id}`;
  await work.locator(`a[href="${href}"]`).click();
  await expect(page).toHaveURL(href);
  await expect(
    page.getByRole("heading", { name: sale.registrationPlate, exact: true }),
  ).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  writeFileSync(
    path.join(process.env.BROWSER_EVIDENCE_DIR!, "sales-volume.json"),
    JSON.stringify({
      total: first.total,
      identities: items.map((item) => item.id),
      opened: sale.id,
    }),
  );
});

test("manager mixed work pages retain global ordering and agent employee refinement", async ({
  page,
}, info) => {
  const group = info.project.name === "mobile";
  await login(page, group ? "farah.aziz@example.invalid" : "siti.aminah@example.invalid");
  let first: WorkPage;
  if (group) {
    await page.goto("/dashboard/work-calendar");
    await expect(
      page.getByText("Choose a branch to view its work calendar.", { exact: true }),
    ).toBeVisible();
    first = await readWork(page, () =>
      page.getByLabel("Branch", { exact: true }).selectOption(branchId),
    );
  } else {
    first = await readWork(page, () => page.goto("/dashboard/work-calendar"));
  }
  expect(first.total).toBeGreaterThan(100);
  const items = await collectPages(page, first);
  expect(items.some((item) => item.kind === "Sale")).toBe(true);
  expect(items.some((item) => item.kind === "Workshop")).toBe(true);
  expect(items.every((item) => item.branchId === branchId)).toBe(true);
  await page.getByLabel("Employee email", { exact: true }).fill("AINA.SOFIA");
  const filtered = await readWork(page, () =>
    page.getByRole("button", { name: "Apply", exact: true }).click(),
  );
  expect(filtered.page).toBe(1);
  expect(filtered.total).toBe(54);
  const agentItems = await collectPages(page, filtered);
  expect(
    agentItems.every(
      (item) => item.kind === "Sale" && item.handlerEmail === "aina.sofia@example.invalid",
    ),
  ).toBe(true);
  await page.screenshot({
    path: path.join(process.env.BROWSER_EVIDENCE_DIR!, `mixed-volume-${info.project.name}.png`),
    fullPage: true,
  });
  writeFileSync(
    path.join(process.env.BROWSER_EVIDENCE_DIR!, "mixed-volume.json"),
    JSON.stringify({ total: first.total, items, agentTotal: filtered.total }),
  );
});
