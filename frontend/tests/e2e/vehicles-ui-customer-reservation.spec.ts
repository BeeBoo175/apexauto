import { expect, test, type BrowserContext } from "@playwright/test";
import { writeFileSync } from "node:fs";
import path from "node:path";
import type { CustomerReservationReceipt } from "@/lib/vehicles/customer-reservation";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const origin = process.env.APP_BASE_URL!;

async function login(context: BrowserContext, email: string) {
  const response = await context.request.post("/api/auth/login", {
    headers: { Origin: origin },
    data: { loginKey: crypto.randomUUID(), email, password: "Password123!" },
  });
  expect(response.status()).toBe(200);
}

async function requestHeaders(context: BrowserContext) {
  const cookie = (await context.cookies()).find((item) => item.name === "apexauto_token")!;
  return { Cookie: `apexauto_token=${cookie.value}`, Origin: origin };
}

async function branchSales(context: BrowserContext, view: string) {
  const response = await context.request.get(`/api/staff/sales?view=${view}&pageSize=50`, {
    headers: await requestHeaders(context),
  });
  expect(response.status()).toBe(200);
  return (await response.json()).data.items as { id: string; version: number }[];
}

test("customer reserves from vehicle detail and reconciles a committed hold after losing its receipt", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(120_000);
  const manager = await browser.newContext({ ...info.project.use, baseURL: origin });
  await login(manager, "siti.aminah@example.invalid");
  // Managers may cancel a sale in any open status, which releases its hold.
  for (const view of ["NeedsApproval", "Open", "BeingHandled", "Approved"]) {
    for (const sale of await branchSales(manager, view)) {
      const cancelled = await manager.request.post(`/api/staff/sales/${sale.id}/cancellations`, {
        headers: await requestHeaders(manager),
        data: {
          operationId: crypto.randomUUID(),
          expectedVersion: sale.version,
          reason: "Release fixture hold for customer purchase acceptance",
        },
      });
      expect(cancelled.status()).toBe(200);
    }
  }

  await page.goto(`/vehicles?branchId=${branch}`);
  const destination = (await page
    .getByRole("link", { name: /^View details for / })
    .first()
    .getAttribute("href"))!;
  const vehicleId = destination.split("/").at(-1)!;
  await page.goto(destination);
  const unverified = await browser.newContext({ ...info.project.use, baseURL: origin });
  await login(unverified, "nur.aisyah@example.invalid");
  const unverifiedPage = await unverified.newPage();
  await unverifiedPage.goto(destination);
  await expect(
    unverifiedPage.getByRole("link", { name: "Verify email to reserve", exact: true }),
  ).toBeVisible();
  await unverifiedPage.getByRole("link", { name: "Verify email to reserve", exact: true }).click();
  await expect(
    unverifiedPage.getByRole("button", { name: "Resend verification email", exact: true }),
  ).toBeVisible();
  await expect(
    unverifiedPage.getByText("Verify your email to view your appointment history.", {
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    unverifiedPage
      .getByRole("alert")
      .filter({ hasText: "This account cannot perform this action" }),
  ).toHaveCount(0);
  expect(
    (
      await unverified.request.post(`/api${destination}/reservations`, {
        headers: await requestHeaders(unverified),
        data: {
          operationId: crypto.randomUUID(),
          publicationId: crypto.randomUUID(),
          promoId: null,
        },
      })
    ).status(),
  ).toBe(403);
  await unverified.close();
  await page.getByRole("link", { name: "Sign in to reserve this car", exact: true }).click();
  await page.getByLabel("Email", { exact: true }).fill("ahmad.faizal@example.invalid");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`${destination}$`));
  const reserve = page.getByRole("button", { name: "Reserve this car", exact: true });
  await expect(reserve).toBeEnabled();
  const rawDetail = (await (await context.request.get(`/api${destination}`)).json()).data;
  const endpoint = `/api${destination}/reservations`;
  const body = {
    operationId: crypto.randomUUID(),
    publicationId: rawDetail.publicationId,
    promoId: null,
  };
  expect(
    (
      await context.request.post(endpoint, {
        headers: { ...(await requestHeaders(context)), Origin: "https://outside.invalid" },
        data: body,
      })
    ).status(),
  ).toBe(403);
  expect(
    (
      await manager.request.post(endpoint, { headers: await requestHeaders(manager), data: body })
    ).status(),
  ).toBe(403);

  const promoInput = page.getByLabel("Promo code", { exact: true });
  await promoInput.fill("APEX500");
  await page
    .locator("form")
    .filter({ has: promoInput })
    .getByRole("button", { name: "Apply", exact: true })
    .click();
  await expect(page.locator("#vehicle-promo-status")).toContainText("APEX500 applied");
  const requests: string[] = [];
  let committed: CustomerReservationReceipt | null = null;
  await page.route(`**${endpoint}`, async (route) => {
    requests.push(route.request().postData()!);
    if (requests.length === 1) {
      const response = await route.fetch();
      expect(response.status()).toBe(200);
      committed = (await response.json()).data;
      await route.abort("failed");
      return;
    }
    await route.continue();
  });
  await reserve.click();
  const dialog = page.getByRole("dialog", { name: "Reserve this car?", exact: true });
  await expect(dialog).toContainText("No real payment is taken");
  await dialog
    .getByRole("button", { name: "Confirm RM500 simulated deposit", exact: true })
    .click();
  const reconcile = dialog.getByRole("button", { name: "Reconcile reservation", exact: true });
  await expect(reconcile).toBeEnabled();
  await expect(dialog.getByRole("button", { name: "Keep browsing", exact: true })).toBeDisabled();
  await page.screenshot({ path: path.join(evidence, "reservation-uncertain.png"), fullPage: true });
  await reconcile.click();
  const receipt = page.getByRole("region", { name: "Reservation confirmation", exact: true });
  await expect(receipt).toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[0]).toBe(requests[1]);
  const saved = committed as CustomerReservationReceipt | null;
  expect(saved).not.toBeNull();
  await expect(receipt).toContainText(saved!.id);
  await expect(receipt).toContainText("APEX500");
  expect(saved!.price.total).toBe(rawDetail.price.total - 500);
  expect(saved!.deposit).toBe(500);
  expect(Date.parse(saved!.expiresAt) - Date.parse(saved!.createdAt)).toBe(48 * 60 * 60 * 1000);
  expect(JSON.stringify(saved)).not.toMatch(/nric|mobile/);

  // The confirmed hold opens its sale under the reservation's id, waiting for an agent.
  const persisted = await branchSales(manager, "Open");
  expect(persisted.filter((item) => item.id === saved!.id)).toHaveLength(1);
  expect((await context.request.get(`/api${destination}`)).status()).toBe(404);
  const catalogue = (await (await context.request.get(`/api/vehicles?branchId=${branch}`)).json())
    .data.items as { id: string }[];
  expect(catalogue.some((item) => item.id === vehicleId)).toBe(false);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await receipt.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "reservation-confirmed.png"), fullPage: true });
  writeFileSync(
    path.join(evidence, "customer-reservation.json"),
    JSON.stringify({ id: saved!.id, vehicleId, requests, confirmed: true }),
  );

  await context.addCookies(await manager.cookies());
  await page.evaluate(() => window.dispatchEvent(new Event("apexauto-session-invalid")));
  await expect(receipt).toHaveCount(0);
  await manager.close();
});
