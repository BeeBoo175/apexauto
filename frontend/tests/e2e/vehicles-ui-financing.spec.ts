import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;
const fault = (value: string) => writeFileSync(path.join(evidence, "fault.txt"), value);
const currency = (value: number) =>
  `MYR ${value.toLocaleString("en-MY", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

test("financing edits are local, reconcile payments and retain drafts through failed and successful refresh", async ({
  page,
  request,
}, info) => {
  const branchId =
    info.project.name === "mobile"
      ? "68f371ad-223a-43d6-b45b-b878eece79ae"
      : "c77708bf-1a31-42f5-9a69-0c8766697a52";
  await page.goto(`/vehicles?branchId=${branchId}`);
  const destination = (await page
    .getByRole("link", { name: /^View details for / })
    .first()
    .getAttribute("href"))!;
  const response = await page.goto(destination);
  expect(await response!.text()).toContain("Financing estimate");
  const calculator = page.getByRole("region", { name: "Financing estimate", exact: true });
  const cash = calculator.getByLabel("Cash contribution (MYR)", { exact: true });
  const term = calculator.getByLabel("Financing term", { exact: true });
  const detail = await request.get(`/api${destination}`);
  expect(detail.status()).toBe(200);
  const total = (await detail.json()).data.price.total as number;
  await expect(cash).toHaveValue((Math.round(total * 10) / 100).toFixed(2));
  await expect(term).toHaveValue("7");
  await page.waitForLoadState("networkidle");
  const before = counts();

  await cash.fill("15000");
  await term.selectOption("9");
  const principal = Math.max(0, total - 15000);
  const interest = Math.round(principal * 27) / 100;
  await expect(calculator.locator("dd").nth(0)).toHaveText(currency(principal));
  await expect(calculator.locator("dd").nth(1)).toHaveText(currency(interest));
  await expect(
    calculator.getByText("First 107 monthly installments", { exact: true }),
  ).toBeVisible();
  expect(counts()).toEqual(before);

  const confirmed = await calculator.locator("dl").textContent();
  const refresh = page.getByRole("button", { name: "Refresh vehicle", exact: true });
  fault("unavailable");
  try {
    await refresh.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "Your last confirmed details are still shown" }),
    ).toBeVisible();
    await expect(cash).toHaveValue("15000");
    await expect(term).toHaveValue("9");
    expect(await calculator.locator("dl").textContent()).toBe(confirmed);
  } finally {
    fault("");
  }
  await refresh.click();
  await expect(refresh).toBeEnabled();
  await expect(cash).toHaveValue("15000");
  await expect(term).toHaveValue("9");

  await cash.fill("1.001");
  await expect(cash).toHaveAttribute("aria-invalid", "true");
  await expect(
    calculator.getByText("Enter a non-negative cash contribution", { exact: false }),
  ).toBeVisible();
  await expect(calculator.locator("dl")).toHaveCount(0);
  await refresh.click();
  await expect(refresh).toBeEnabled();
  await expect(cash).toHaveValue("1.001");
  await cash.fill(String(total + 1000));
  await expect(calculator.getByText("No financing required", { exact: true })).toBeVisible();
  for (const value of await calculator.locator("dd").all()) {
    await expect(value).toContainText("MYR 0.00");
  }
  await cash.fill("15000");
  await term.selectOption("7");
  await calculator.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "financing.png") });
  writeFileSync(
    path.join(evidence, "financing.json"),
    JSON.stringify({ total, editsReadUpstream: false, draftsRetained: true, noFinancing: true }),
  );
});
