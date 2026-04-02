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

test("canonical promos update price and financing without stacking and retain confirmation on failed replacement", async ({
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
  const input = page.getByLabel("Promo code", { exact: true });
  const apply = page.getByRole("button", { name: "Apply promo", exact: true });
  const price = page.getByRole("region", { name: "Itemized price estimate", exact: true });
  const calculator = page.getByRole("region", { name: "Financing estimate", exact: true });
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
  await expect(price.getByText("Applied promo: APEX500", { exact: true })).toBeVisible();
  await expect(price.locator("dd").last()).toHaveText(currency(detail.price.total - 500));
  await expect(cash).toHaveValue((Math.round((detail.price.total - 500) * 10) / 100).toFixed(2));
  await cash.fill("15000");
  await term.selectOption("9");
  const confirmed = await calculator.locator("dl").textContent();
  await input.fill("apex500");
  await apply.click();
  await expect(page.getByRole("alert").filter({ hasText: "exact case" })).toBeVisible();
  await expect(price.getByText("Applied promo: APEX500", { exact: true })).toBeVisible();
  await input.fill("RAYA1000");
  fault("unavailable");
  try {
    await apply.click();
    await expect(
      page.getByRole("alert").filter({ hasText: "promo could not be checked" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Refresh vehicle", exact: true }).click();
    await expect(
      page.getByRole("alert").filter({ hasText: "last confirmed details" }),
    ).toBeVisible();
    expect(await calculator.locator("dl").textContent()).toBe(confirmed);
    await expect(input).toHaveValue("RAYA1000");
  } finally {
    fault("");
  }
  const refresh = page.getByRole("button", { name: "Refresh vehicle", exact: true });
  await refresh.click();
  await expect(refresh).toBeEnabled();
  await expect(price.getByText("Applied promo: APEX500", { exact: true })).toBeVisible();
  await apply.click();
  await expect(price.getByText("Applied promo: RAYA1000", { exact: true })).toBeVisible();
  await expect(price.locator("dd").last()).toHaveText(currency(detail.price.total - 1000));
  await expect(calculator.locator("dd").first()).toHaveText(
    currency(detail.price.total - 1000 - 15000),
  );
  await expect(cash).toHaveValue("15000");
  await expect(term).toHaveValue("9");
  await price.scrollIntoViewIfNeeded();
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
      const response = await route.fetch();
      received();
      await gate;
      await route.fulfill({ response }).catch(() => {});
      finished();
    },
    { times: 1 },
  );
  await input.fill("APEX500");
  await apply.click();
  await arrived;
  await input.fill("RAYA1000");
  await apply.click();
  await expect(apply).toBeEnabled();
  await expect(price.getByText("Applied promo: RAYA1000", { exact: true })).toBeVisible();
  release();
  await settled;
  await expect(price.locator("dd").last()).toHaveText(currency(detail.price.total - 1000));

  // A confirmed replacement with changed terms revokes the old quote, preserving drafts.
  await page.route(
    `**/api${destination}`,
    async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      body.data.price.vehiclePrice += 100;
      body.data.price.total += 100;
      body.data.listing.vehiclePrice += 100;
      await route.fulfill({ response, json: body });
    },
    { times: 1 },
  );
  await refresh.click();
  await expect(price.getByText("No promo applied", { exact: true })).toBeVisible();
  await expect(price.locator("dd").last()).toHaveText(currency(detail.price.total + 100));
  await expect(input).toHaveValue("RAYA1000");
  await expect(cash).toHaveValue("15000");
  await expect(term).toHaveValue("9");
  await refresh.click();
  await expect(refresh).toBeEnabled();
  await apply.click();
  await expect(price.getByText("Applied promo: RAYA1000", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Remove promo", exact: true }).click();
  await expect(price.locator("dd").last()).toHaveText(currency(detail.price.total));
  await expect(input).toHaveValue("");

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
      changedTermsProjection: "injected browser response",
      supersededRealQuote: true,
      originGuard: true,
    }),
  );
});
