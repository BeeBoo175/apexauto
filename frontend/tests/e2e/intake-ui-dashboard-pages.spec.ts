import { expect, test } from "@playwright/test";
import { signInAs } from "./staff-session";

const pages = [
  "/dashboard/home",
  "/dashboard/home?view=workshop&page=1&pageSize=10",
  "/dashboard/work-calendar",
  "/dashboard/work-calendar?view=workshop&assignment=assigned&pageSize=10",
  "/dashboard/performance",
  "/dashboard/performance?view=branches",
  "/dashboard/performance?view=sales&period=thisMonth&status=active",
  "/dashboard/performance?view=workshop&sort=name",
  "/dashboard/accounts",
  "/dashboard/accounts/customers",
  "/dashboard/settings",
  "/dashboard/settings/promos",
  "/dashboard/settings/warranties",
  "/dashboard/management",
  "/dashboard/sales",
  "/dashboard/test-drives",
  "/dashboard/inspection",
  "/dashboard/intake",
  "/dashboard/account",
];

test("every dashboard address renders its workspace without a client error", async ({ page }) => {
  test.setTimeout(240_000);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(`${page.url()} ${error.message}`));
  await signInAs(page, "farah.aziz@example.invalid", "GeneralManager");
  const report: string[] = [];
  for (const address of pages) {
    await page.goto(address);
    await page.waitForTimeout(2500);
    const text = (await page.locator("body").innerText()).replace(/\n+/g, " / ").slice(0, 90);
    const broken = (await page.getByText("This page couldn’t load").count()) > 0;
    report.push(
      `${broken ? "BROKEN" : "ok"} ${address} -> ${new URL(page.url()).pathname + new URL(page.url()).search} :: ${text}`,
    );
  }
  console.log("SMOKE\n" + report.join("\n"));
  console.log("SMOKEERRORS", JSON.stringify(errors));
  expect(report.filter((line) => line.startsWith("BROKEN"))).toEqual([]);
  expect(errors).toEqual([]);
});
