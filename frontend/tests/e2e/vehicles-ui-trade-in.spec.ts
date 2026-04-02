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

test("canonical trade-in lower bound reduces financing and preserves accepted inputs through replacement failures", async ({
  page,
  request,
}, info) => {
  const branchId =
    info.project.name === "mobile"
      ? "68f371ad-223a-43d6-b45b-b878eece79ae"
      : "c77708bf-1a31-42f5-9a69-0c8766697a52";
  await page.goto(`/vehicles?branchId=${branchId}`);
  const destination = (await page
    .getByRole("link", { name: "View vehicle details", exact: true })
    .first()
    .getAttribute("href"))!;
  await page.goto(destination);
  const detail = (await (await request.get(`/api${destination}`)).json()).data;
  const calculator = page.getByRole("region", { name: "Financing estimate", exact: true });
  const tradeIn = page.getByRole("region", { name: "Indicative trade-in", exact: true });
  const cash = calculator.getByLabel("Cash contribution (MYR)", { exact: true });
  const term = calculator.getByLabel("Financing term", { exact: true });
  const load = tradeIn.getByRole("button", { name: "Load trade-in options", exact: true });
  const value = tradeIn.getByRole("button", { name: "Value trade-in", exact: true });
  const principal = calculator.locator("dd").first();
  await page.waitForLoadState("networkidle");
  expect(counts()["/api/taxonomy"] ?? 0).toBe(0);
  expect(counts()["/api/valuations"] ?? 0).toBe(0);
  await cash.fill("6000");
  await term.selectOption("9");
  fault("unavailable");
  try {
    await load.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "options could not be loaded" }),
    ).toBeVisible();
    await expect(load).toBeEnabled();
  } finally {
    fault("");
  }
  const taxonomyResponse = page.waitForResponse("**/api/intake/taxonomy");
  await load.click();
  const taxonomy = (await (await taxonomyResponse).json()).data;
  const make = taxonomy.makes.find((item: { name: string }) => item.name === "Proton");
  const model = make.models.find((item: { name: string }) => item.name === "Saga");
  const trim = model.trims[0].name;
  const year = tradeIn.getByLabel("Manufacture year", { exact: true });
  const mileage = tradeIn.getByLabel("Declared mileage (km)", { exact: true });
  const beforeTyping = counts();
  await tradeIn.getByLabel("Make", { exact: true }).selectOption("Proton");
  await tradeIn.getByLabel("Model", { exact: true }).selectOption("Saga");
  await tradeIn.getByLabel("Trim", { exact: true }).selectOption(trim);
  await year.fill("2020");
  await mileage.fill("90000");
  expect(counts()).toEqual(beforeTyping);
  const sent = page.waitForRequest("**/api/intake/valuation");
  const valued = page.waitForResponse("**/api/intake/valuation");
  await value.click();
  expect((await sent).postDataJSON()).toEqual({
    make: "Proton",
    model: "Saga",
    trim,
    manufactureYear: 2020,
    mileageKilometres: 90000,
  });
  const quote = (await (await valued).json()).data;
  await expect(tradeIn.getByText(/Using valuation for.*90,000 km/)).toBeVisible();
  await expect(tradeIn.getByText(/Valuation range/)).toContainText(currency(quote.maxValuation));
  await expect(principal).toHaveText(
    currency(Math.max(0, detail.price.total - 6000 - quote.minValuation)),
  );
  const valuationCount = counts()["/api/valuations"];
  await page.getByLabel("Promo code", { exact: true }).fill("APEX500");
  await page.getByRole("button", { name: "Apply promo", exact: true }).click();
  await expect(page.getByText("Applied promo: APEX500", { exact: true })).toBeVisible();
  await expect(principal).toHaveText(
    currency(Math.max(0, detail.price.total - 500 - 6000 - quote.minValuation)),
  );
  expect(counts()["/api/valuations"]).toBe(valuationCount);
  const confirmed = await calculator.locator("dl").textContent();
  await mileage.fill("91000");
  fault("unavailable");
  try {
    await value.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "trade-in could not be valued" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Refresh vehicle", exact: true }).click();
    await expect(
      page.getByRole("alert").filter({ hasText: "last confirmed details" }),
    ).toBeVisible();
    expect(await calculator.locator("dl").textContent()).toBe(confirmed);
    await expect(mileage).toHaveValue("91000");
    await expect(tradeIn.getByText(/Using valuation for.*90,000 km/)).toBeVisible();
  } finally {
    fault("");
  }
  const refresh = page.getByRole("button", { name: "Refresh vehicle", exact: true });
  await refresh.click();
  await expect(refresh).toBeEnabled();
  expect(await calculator.locator("dl").textContent()).toBe(confirmed);
  await expect(cash).toHaveValue("6000");
  await expect(term).toHaveValue("9");
  await year.fill("9999");
  await value.click();
  await expect(page.getByRole("alert").filter({ hasText: "whole-number mileage" })).toBeVisible();
  expect(await calculator.locator("dl").textContent()).toBe(confirmed);
  await year.fill("2020");

  const beforeInvalid = counts();
  await mileage.fill("9e4");
  await value.click();
  await expect(page.getByRole("alert").filter({ hasText: "using digits" })).toBeVisible();
  expect(counts()).toEqual(beforeInvalid);
  expect(await calculator.locator("dl").textContent()).toBe(confirmed);

  // Hold a real response while editing/submitting a different exact vehicle draft.
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
    "**/api/intake/valuation",
    async (route) => {
      const response = await route.fetch();
      received();
      await gate;
      await route.fulfill({ response }).catch(() => {});
      finished();
    },
    { times: 1 },
  );
  await mileage.fill("92000");
  await value.click();
  await arrived;
  await mileage.fill("93000");
  const replacement = page.waitForResponse("**/api/intake/valuation");
  await value.click();
  const latest = (await (await replacement).json()).data;
  await expect(value).toBeEnabled();
  await expect(tradeIn.getByText(/Using valuation for.*93,000 km/)).toBeVisible();
  release();
  await settled;
  await expect(principal).toHaveText(
    currency(Math.max(0, detail.price.total - 500 - 6000 - latest.minValuation)),
  );
  await expect(tradeIn.getByText(/Using valuation for.*93,000 km/)).toBeVisible();
  await expect(page.getByLabel("Notifications", { exact: true }).getByRole("alert")).toHaveCount(0);
  await calculator.screenshot({ path: path.join(evidence, "trade-in.png") });
  await cash.fill(String(detail.price.total));
  await expect(calculator.getByText("No financing required", { exact: true })).toBeVisible();
  for (const amount of await calculator.locator("dd").allTextContents()) {
    expect(amount).toMatch(/^MYR 0\.00(?: each)?$/);
  }
  await cash.fill("6000");
  await tradeIn.getByRole("button", { name: "Remove trade-in allowance", exact: true }).click();
  await expect(principal).toHaveText(currency(detail.price.total - 500 - 6000));
  await expect(mileage).toHaveValue("93000");
  await expect(term).toHaveValue("9");

  // The same extracted controls keep the seller form's original registration and bounds.
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
      supersededRealQuote: true,
      sharedSellerControls: true,
      persistence: "unchanged authority reused; public valuation is stateless",
    }),
  );
});
