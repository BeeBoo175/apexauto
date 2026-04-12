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

test("canonical promos replace confirmed discounts and browser reload clears document-local quotes", async ({
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
  await page.goto(destination);
  const detail = (await (await request.get(`/api${destination}`)).json()).data;
  const input = page.getByLabel("Promo code", { exact: true });
  const promo = page
    .locator("form")
    .filter({ has: page.getByLabel("Promo code", { exact: true }) });
  const apply = promo.getByRole("button", { name: "Apply", exact: true });
  const applied = page.locator("#vehicle-promo-status");
  const total = page
    .locator('div[aria-live="polite"]')
    .filter({
      has: page.getByText("Estimated total", { exact: true }),
    })
    .locator("strong");
  const calculator = page.getByRole("region", { name: "Financing estimate", exact: true });
  const principal = calculator
    .locator("dl > div")
    .filter({
      has: page.getByText("Amount financed", { exact: true }),
    })
    .locator("dd");
  const cash = calculator.getByLabel("Cash contribution (MYR)", { exact: true });
  const term = calculator.getByLabel("Financing term", { exact: true });
  await page.waitForLoadState("networkidle");
  const before = counts();
  await input.fill("  APEX500  ");
  expect(counts()).toEqual(before);
  const validation = page.waitForRequest("**/api/promos/validate");
  await apply.click();
  expect((await validation).postDataJSON()).toEqual({
    vehicleId: detail.listing.id,
    code: "APEX500",
  });
  await expect(applied).toHaveText("APEX500 applied to the total below.");
  await expect(total).toHaveText(currency(detail.price.total - 500));
  await expect(cash).toHaveValue((Math.round((detail.price.total - 500) * 10) / 100).toFixed(2));
  await cash.fill("15000");
  await term.focus();
  await term.press("End");
  const confirmedPrincipal = await principal.textContent();
  await input.fill("apex500");
  await apply.click();
  await expect(page.getByRole("alert").filter({ hasText: "exact case" })).toBeVisible();
  await expect(applied).toHaveText("APEX500 applied to the total below.");

  await input.fill("RAYA1000");
  fault("unavailable");
  try {
    await apply.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "promo could not be checked" }),
    ).toBeVisible();
    await expect(principal).toHaveText(confirmedPrincipal!);
    await expect(applied).toHaveText("APEX500 applied to the total below.");
    await expect(input).toHaveValue("RAYA1000");
  } finally {
    fault("");
  }
  await apply.click();
  await expect(applied).toHaveText("RAYA1000 applied to the total below.");
  await expect(total).toHaveText(currency(detail.price.total - 1000));
  await expect(principal).toHaveText(currency(Math.max(0, detail.price.total - 1000 - 15000)));
  await expect(cash).toHaveValue("15000");
  await expect(term).toHaveValue("9");
  await promo.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "promo.png") });

  // A delayed real quote cannot overwrite a newer draft or confirmation.
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
    "**/api/promos/validate",
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
  try {
    await input.fill("APEX500");
    await apply.click();
    await arrived;
    await input.fill("RAYA1000");
    await apply.click();
    await expect(apply).toBeEnabled();
    await expect(applied).toHaveText("RAYA1000 applied to the total below.");
  } finally {
    release();
    await settled;
  }
  await expect(total).toHaveText(currency(detail.price.total - 1000));

  // A quote for changed base terms must not replace the current confirmed estimate.
  await page.route(
    "**/api/promos/validate",
    async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      body.data.price.vehiclePrice += 100;
      body.data.price.total += 100;
      await route.fulfill({ response, json: body });
    },
    { times: 1 },
  );
  await input.fill("APEX500");
  await apply.click();
  await expect(page.getByRole("alert").filter({ hasText: "Reload this page" })).toBeVisible();
  await expect(applied).toHaveText("RAYA1000 applied to the total below.");
  await expect(total).toHaveText(currency(detail.price.total - 1000));
  await expect(cash).toHaveValue("15000");
  await expect(term).toHaveValue("9");

  await page.reload();
  await expect(applied).toBeEmpty();
  await expect(total).toHaveText(currency(detail.price.total));
  await expect(input).toHaveValue("");
  await expect(cash).toHaveValue((Math.round(detail.price.total * 10) / 100).toFixed(2));
  await expect(term).toHaveValue("7");
  await input.fill("RAYA1000");
  await apply.click();
  await expect(applied).toHaveText("RAYA1000 applied to the total below.");
  await promo.getByRole("button", { name: "Remove", exact: true }).click();
  await expect(total).toHaveText(currency(detail.price.total));
  await expect(input).toHaveValue("");

  await page.waitForLoadState("networkidle");
  const upstream = counts();
  const denied = await request.post("/api/promos/validate", {
    headers: { Origin: "https://foreign.invalid" },
    data: { vehicleId: detail.listing.id, code: "APEX500" },
  });
  expect(denied.status()).toBe(403);
  const invalid = await request.post("/api/promos/validate", {
    headers: { Origin: new URL(page.url()).origin, "Content-Type": "application/json" },
    data: "{",
  });
  expect(invalid.status()).toBe(400);
  expect(counts()).toEqual(upstream);
  writeFileSync(
    path.join(evidence, "promo.json"),
    JSON.stringify({
      canonicalCodes: ["APEX500", "RAYA1000"],
      failedReplacementRetained: true,
      changedTermsProjection: "injected promo response",
      supersededRealQuote: true,
      browserReloadClearsQuote: true,
      originGuard: true,
    }),
  );
});
