import { expect, test } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const counts = () => JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8"));
const fault = (value: string) => writeFileSync(path.join(evidence, "fault.txt"), value);
const currency = (value: number) =>
  `MYR ${value.toLocaleString("en-MY", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

test("trade-in options load locally and confirmed allowance survives replacement failures", async ({
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
  const calculator = page.getByRole("region", { name: "Financing estimate", exact: true });
  const tradeIn = page.getByRole("region", { name: "Trade-in valuation", exact: true });
  const result = tradeIn.getByRole("region", { name: "Trade-in valuation result", exact: true });
  const cash = calculator.getByLabel("Cash contribution (MYR)", { exact: true });
  const term = calculator.getByLabel("Financing term", { exact: true });
  const principal = calculator
    .locator("dl > div")
    .filter({
      has: page.getByText("Amount financed", { exact: true }),
    })
    .locator("dd");
  const allowance = calculator
    .locator("dl > div")
    .filter({
      has: page.getByText("Trade-in allowance", { exact: true }),
    })
    .locator("dd");
  const value = tradeIn.getByRole("button", { name: "Get my valuation", exact: true });

  let failOptions!: () => void;
  const optionsGate = new Promise<void>((resolve) => {
    failOptions = resolve;
  });
  await page.route(
    "**/api/intake/taxonomy",
    async (route) => {
      const response = await route.fetch();
      await optionsGate;
      await route.fulfill({ response, status: 503, body: "" });
    },
    { times: 1 },
  );
  try {
    const response = await page.goto(destination);
    const markup = (await response!.text()).replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "");
    expect(markup).toContain("Estimated total");
    expect(markup).toContain("Inspection report");
    await expect(calculator).toBeVisible();
    await expect(tradeIn.getByLabel("Loading vehicle options", { exact: true })).toBeVisible();
  } finally {
    failOptions();
  }
  const retry = tradeIn.getByRole("button", { name: "Retry vehicle options", exact: true });
  await expect(retry).toBeVisible();
  const taxonomyResponse = page.waitForResponse("**/api/intake/taxonomy");
  await retry.click();
  const taxonomy = (await (await taxonomyResponse).json()).data;
  const make = taxonomy.makes.find((item: { name: string }) => item.name === "Proton");
  const model = make.models.find((item: { name: string }) => item.name === "Saga");
  const trim = model.trims[0].name;
  const detail = (await (await request.get(`/api${destination}`)).json()).data;
  const year = tradeIn.getByLabel("Manufacture year", { exact: true });
  const mileage = tradeIn.getByLabel("Declared mileage (km)", { exact: true });
  await page.waitForLoadState("networkidle");
  expect(counts()["/api/taxonomy"]).toBe(2);
  expect(counts()["/api/valuations"] ?? 0).toBe(0);
  const beforeTyping = counts();
  await cash.fill("6000");
  await term.focus();
  await term.press("End");
  await tradeIn.getByLabel("Make", { exact: true }).selectOption("Proton");
  await tradeIn.getByLabel("Model", { exact: true }).selectOption("Saga");
  await tradeIn.getByLabel("Trim", { exact: true }).selectOption(trim);
  await year.fill("2020");
  await mileage.fill("90000");
  expect(counts()).toEqual(beforeTyping);

  const sent = page.waitForRequest("**/api/intake/valuations");
  const valued = page.waitForResponse("**/api/intake/valuations");
  await value.click();
  expect((await sent).postDataJSON()).toEqual({
    make: "Proton",
    model: "Saga",
    trim,
    manufactureYear: 2020,
    mileageKilometres: 90000,
  });
  const quote = (await (await valued).json()).data;
  await expect(result.getByText(/90,000 km/)).toBeVisible();
  await expect(result.locator("dd").filter({ hasText: "Valuation range" })).toContainText(
    currency(quote.maxValuation),
  );
  await expect(allowance).toHaveText(currency(quote.minValuation));
  await expect(principal).toHaveText(
    currency(Math.max(0, detail.price.total - 6000 - quote.minValuation)),
  );
  const valuationCount = counts()["/api/valuations"];
  await page.getByLabel("Promo code", { exact: true }).fill("APEX500");
  const promo = page
    .locator("form")
    .filter({ has: page.getByLabel("Promo code", { exact: true }) });
  await promo.getByRole("button", { name: "Apply", exact: true }).click();
  await expect(page.locator("#vehicle-promo-status")).toHaveText(
    "APEX500 applied to the total below.",
  );
  const confirmedPrincipal = currency(
    Math.max(0, detail.price.total - 500 - 6000 - quote.minValuation),
  );
  await expect(principal).toHaveText(confirmedPrincipal);
  expect(counts()["/api/valuations"]).toBe(valuationCount);

  await tradeIn.getByRole("button", { name: "Get a new valuation", exact: true }).click();
  await mileage.fill("91000");
  fault("unavailable");
  try {
    await value.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "trade-in could not be valued" }),
    ).toBeVisible();
    await expect(principal).toHaveText(confirmedPrincipal);
    await expect(allowance).toHaveText(currency(quote.minValuation));
    await expect(mileage).toHaveValue("91000");
    await expect(value).toBeEnabled();
  } finally {
    fault("");
  }
  await year.fill("9999");
  await value.click();
  await expect(page.getByRole("alert").filter({ hasText: "whole-number mileage" })).toBeVisible();
  await expect(principal).toHaveText(confirmedPrincipal);
  await year.fill("2020");
  const beforeInvalid = counts();
  await mileage.fill("9e4");
  await value.click();
  await expect(page.getByRole("alert").filter({ hasText: "using digits" })).toBeVisible();
  expect(counts()).toEqual(beforeInvalid);
  await expect(allowance).toHaveText(currency(quote.minValuation));

  let release!: () => void;
  let received!: () => void;
  let finished!: () => void;
  const arrived = new Promise<void>((resolve) => {
    received = resolve;
  });
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const settled = new Promise<void>((resolve) => {
    finished = resolve;
  });
  await page.route(
    "**/api/intake/valuations",
    async (route) => {
      try {
        const response = await route.fetch();
        received();
        await gate;
        await route.fulfill({ response }).catch(() => {});
      } finally {
        finished();
      }
    },
    { times: 1 },
  );
  const replacement = page.waitForResponse("**/api/intake/valuations");
  try {
    await mileage.fill("92000");
    await value.click();
    await arrived;
    await expect(mileage).toBeDisabled();
    await expect(
      tradeIn.getByRole("button", { name: "Valuing trade-in…", exact: true }),
    ).toBeDisabled();
    await expect(allowance).toHaveText(currency(quote.minValuation));
    await page.getByRole("link", { name: "Financing", exact: true }).click();
    await expect(cash).toHaveValue("6000");
    await expect(term).toHaveValue("9");
  } finally {
    release();
    await settled;
  }
  const latest = (await (await replacement).json()).data;
  await expect(result.getByText(/92,000 km/)).toBeVisible();
  await expect(principal).toHaveText(
    currency(Math.max(0, detail.price.total - 500 - 6000 - latest.minValuation)),
  );
  await calculator.screenshot({ path: path.join(evidence, "trade-in.png") });
  await cash.fill(String(detail.price.total));
  await expect(calculator.getByText("No financing required", { exact: true })).toBeVisible();
  await expect(principal).toHaveText(currency(0));

  await cash.fill("6000");
  await tradeIn.getByRole("button", { name: "Get a new valuation", exact: true }).click();
  await expect(mileage).toHaveValue("92000");
  await expect(allowance).toHaveText(currency(latest.minValuation));
  await expect(term).toHaveValue("9");
  await page.reload();
  await expect(cash).toHaveValue((Math.round(detail.price.total * 10) / 100).toFixed(2));
  await expect(term).toHaveValue("7");
  await expect(mileage).toHaveValue("");
  await expect(allowance).toHaveCount(0);

  await page.goto("/sell");
  await expect(page.getByLabel("Registration plate", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Manufacture year", { exact: true })).toHaveAttribute("min", "1900");
  await expect(page.getByLabel("Manufacture year", { exact: true })).toHaveAttribute("max", "2100");
  await page.getByLabel("Make", { exact: true }).selectOption("Proton");
  await page.getByLabel("Model", { exact: true }).selectOption("Saga");
  await page.getByLabel("Trim", { exact: true }).selectOption(trim);
  await page.getByLabel("Make", { exact: true }).selectOption("Perodua");
  await expect(page.getByLabel("Model", { exact: true })).toHaveValue("");
  await expect(page.getByLabel("Trim", { exact: true })).toBeDisabled();
  writeFileSync(
    path.join(evidence, "trade-in.json"),
    JSON.stringify({
      lowerBound: quote.minValuation,
      fullRangeShown: true,
      exactVehicleRequest: true,
      failedReplacementRetained: true,
      pendingValuationGuards: true,
      sectionOnlyOptionsShell: true,
      optionsFailure: "injected real taxonomy response",
      reloadResetsAllowance: true,
      sharedSellerControls: true,
      persistence: "unchanged authority reused; public valuation is stateless",
    }),
  );
});
