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

test("financing edits stay local and survive section navigation while browser reload resets drafts", async ({
  page,
  request,
}, info) => {
  test.setTimeout(120_000);
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
  const markup = (await response!.text()).replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "");
  expect(markup).toContain("Financing estimate");
  const calculator = page.getByRole("region", { name: "Financing estimate", exact: true });
  const cash = calculator.getByLabel("Cash contribution (MYR)", { exact: true });
  const term = calculator.getByLabel("Financing term", { exact: true });
  const principal = calculator
    .locator("dl > div")
    .filter({
      has: page.getByText("Amount financed", { exact: true }),
    })
    .locator("dd");
  const repayments = calculator
    .locator("dl > div")
    .filter({
      has: page.getByText("Total repayments", { exact: true }),
    })
    .locator("dd");
  const interestRow = calculator
    .locator("dl > div")
    .filter({
      has: page.getByText("Flat interest", { exact: true }),
    })
    .locator("dd");
  const detail = await request.get(`/api${destination}`);
  expect(detail.status()).toBe(200);
  const total = (await detail.json()).data.price.total as number;
  const defaultCash = (Math.round(total * 10) / 100).toFixed(2);
  await expect(cash).toHaveValue(defaultCash);
  await expect(term).toHaveValue("7");
  await page.waitForLoadState("networkidle");
  const before = counts();

  await cash.fill("15000");
  await term.focus();
  await term.press("End");
  const amountFinanced = Math.max(0, total - 15000);
  const interest = Math.round(amountFinanced * 27) / 100;
  await expect(principal).toHaveText(currency(amountFinanced));
  await expect(repayments).toHaveText(currency(amountFinanced + interest));
  await calculator.getByText("Repayment breakdown", { exact: true }).click();
  await expect(interestRow).toHaveText(currency(interest));
  await expect(calculator.getByText("First 107 installments", { exact: true })).toBeVisible();
  expect(counts()).toEqual(before);

  await page.getByRole("link", { name: "Inspection report", exact: true }).click();
  await page.getByRole("link", { name: "Financing", exact: true }).click();
  await expect(cash).toHaveValue("15000");
  await expect(term).toHaveValue("9");
  await expect(calculator.locator("details")).toHaveAttribute("open", "");

  await cash.fill("1.001");
  await expect(cash).toHaveAttribute("aria-invalid", "true");
  await expect(
    calculator.getByText("Enter a non-negative cash contribution", { exact: false }),
  ).toBeVisible();
  await expect(calculator.locator("dl")).toHaveCount(0);
  await page.getByRole("link", { name: "Overview", exact: true }).click();
  await page.getByRole("link", { name: "Financing", exact: true }).click();
  await expect(cash).toHaveValue("1.001");
  await cash.fill(String(total + 1000));
  await expect(calculator.getByText("No financing required", { exact: true })).toBeVisible();
  await expect(principal).toHaveText(currency(0));
  await expect(repayments).toHaveText(currency(0));

  await cash.fill("15000");
  await calculator.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "financing.png") });
  await page.reload();
  await expect(cash).toHaveValue(defaultCash);
  await expect(term).toHaveValue("7");

  fault("unavailable");
  try {
    await page.reload();
    await expect(page.getByRole("heading", { name: "Vehicle details", exact: true })).toBeVisible();
    await expect(calculator).toHaveCount(0);
  } finally {
    fault("");
  }
  await page.reload();
  await expect(cash).toHaveValue(defaultCash);
  await expect(term).toHaveValue("7");
  writeFileSync(
    path.join(evidence, "financing.json"),
    JSON.stringify({
      total,
      editsReadUpstream: false,
      sectionDraftsRetained: true,
      reloadResetsDrafts: true,
      browserReloadRecovery: true,
      noFinancing: true,
    }),
  );
});
