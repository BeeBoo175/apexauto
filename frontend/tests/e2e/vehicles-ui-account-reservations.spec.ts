import { expect, test, type BrowserContext } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { AccountReservations } from "@/lib/vehicles/account-reservations";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const origin = process.env.APP_BASE_URL!;
const counts = () =>
  JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<string, number>;

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

test("customer account retains all controls, independent scoped history and exact cancellation recovery", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(120_000);
  await page.goto("/vehicles");
  await page
    .getByRole("link", { name: /^View details for / })
    .first()
    .click();
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await page.screenshot({ path: path.join(evidence, "unified-vehicle-detail.png") });
  await page.locator("#overview").scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "unified-vehicle-overview.png") });
  for (const section of ["inspection", "financing", "trade-in"]) {
    await page.locator(`#${section}`).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(evidence, `vehicle-${section}.png`) });
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await login(context, "ahmad.faizal@example.invalid");
  const initialHistory = (
    await (
      await context.request.get("/api/account/reservations", {
        headers: await requestHeaders(context),
      })
    ).json()
  ).data as AccountReservations;
  if (initialHistory.active?.isHandled) {
    const agent = await browser.newContext({ ...info.project.use, baseURL: origin });
    try {
      await login(agent, "aina.sofia@example.invalid");
      const id = initialHistory.active.receipt.id;
      const saleResponse = await agent.request.get(`/api/staff/sales/${id}`, {
        headers: await requestHeaders(agent),
      });
      expect(saleResponse.status()).toBe(200);
      const sale = (await saleResponse.json()).data;
      const released = await agent.request.post(`/api/staff/sales/${id}/releases`, {
        headers: await requestHeaders(agent),
        data: { operationId: crypto.randomUUID(), expectedVersion: sale.sale.version },
      });
      expect(released.status()).toBe(200);
    } finally {
      await agent.close();
    }
  }
  if (!initialHistory.active) {
    // Desktop and mobile share fixtures; the preceding project deliberately released its hold.
    const vehicles = (await (await context.request.get("/api/vehicles")).json()).data.items as {
      id: string;
    }[];
    const vehicle = vehicles[0];
    const detail = (await (await context.request.get(`/api/vehicles/${vehicle.id}`)).json()).data;
    const reserved = await context.request.post(`/api/vehicles/${vehicle.id}/reservations`, {
      headers: await requestHeaders(context),
      data: {
        operationId: crypto.randomUUID(),
        publicationId: detail.publicationId,
        promoId: null,
      },
    });
    expect(reserved.status()).toBe(200);
  }
  const before = counts();
  const documentResponse = await page.goto("/account?reservationPage=2&page=2");
  const html = await documentResponse!.text();
  expect(html).toContain("Your reservations");
  expect(html).toContain("Account details");
  expect(html).toContain("Your appointments");
  const reservations = page.getByRole("region", { name: "Your reservations", exact: true });
  const active = page.getByRole("article", {
    name: "Your reserved car",
    exact: true,
    includeHidden: true,
  });
  await expect(active).toBeVisible();
  await expect(
    reservations.getByRole("button", { name: "Next reservations", exact: true }),
  ).toBeEnabled();
  expect(
    (counts()["/api/account/reservations"] ?? 0) - (before["/api/account/reservations"] ?? 0),
  ).toBe(1);
  const response = await context.request.get("/api/account/reservations?page=2", {
    headers: await requestHeaders(context),
  });
  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toContain("no-store");
  const snapshot = (await response.json()).data as AccountReservations;
  const held = snapshot.active!;
  expect(snapshot.pageSize).toBe(5);
  expect(snapshot.total).toBeGreaterThan(50);
  expect(html).toContain(held.receipt.id);
  expect(held.coverPhotoId).not.toBeNull();
  expect(
    await active
      .getByRole("img")
      .evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0),
  ).toBe(true);
  await page.screenshot({ path: path.join(evidence, "account-overview.png") });

  await page.getByRole("link", { name: "Account details", exact: true }).click();
  const details = page.getByRole("region", { name: "Account details", exact: true });
  await expect(details.getByRole("button", { name: "Refresh account", exact: true })).toHaveCount(
    0,
  );
  await expect(details.getByRole("button", { name: "Sign out", exact: true })).toBeVisible();
  await expect(details.getByRole("link", { name: "Password recovery", exact: true })).toBeVisible();
  await details.scrollIntoViewIfNeeded();
  await page.screenshot({ path: path.join(evidence, "account-existing-features.png") });
  await page.getByRole("link", { name: "Reservations", exact: true }).click();
  await reservations.getByRole("button", { name: "Next reservations", exact: true }).click();
  await expect(page).toHaveURL(/reservationPage=3&page=2/);
  await expect(
    reservations.getByText(`${snapshot.total} reservations · Page 3 · Newest first`, {
      exact: true,
    }),
  ).toBeVisible();
  await expect(active).toContainText(held.receipt.number);
  await page.getByRole("link", { name: "Selling appointments", exact: true }).click();
  const appointments = page.getByRole("region", { name: "Your appointments", exact: true });
  await appointments.getByRole("button", { name: "Next appointments", exact: true }).click();
  await expect(page).toHaveURL(/reservationPage=3&page=3/);
  await expect(active).toContainText(held.receipt.number);

  await page.getByRole("link", { name: "Reservations", exact: true }).click();
  await active.locator("summary").click();
  await expect(active.getByRole("heading", { name: "Reservation", exact: true })).toBeVisible();
  const confirmed = await reservations.innerText();
  await page.route("**/api/account/reservations?**", (route) =>
    route.fulfill({
      status: 502,
      json: {
        status: 502,
        data: null,
        uncertain: true,
        message: "Controlled failed reservation refresh",
      },
    }),
  );
  await page.getByRole("link", { name: "Reservations", exact: true }).click();
  await reservations.getByRole("button", { name: "Next reservations", exact: true }).click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Your last confirmed records remain shown." }),
  ).toBeVisible();
  await expect(active.getByRole("heading", { name: "Reservation", exact: true })).toBeVisible();
  expect(await reservations.innerText()).toContain(held.receipt.number);
  expect(confirmed).toContain(held.receipt.number);
  await page.unroute("**/api/account/reservations?**");

  const outsider = await browser.newContext({ ...info.project.use, baseURL: origin });
  const outsiderEmail = `nur.hanani.${crypto.randomUUID()}@example.invalid`;
  const registration = await outsider.request.post("/api/auth/register", {
    headers: { Origin: origin },
    data: {
      registrationKey: crypto.randomUUID(),
      legalName: "Nur Hanani Abdullah",
      email: outsiderEmail,
      password: "Password123!",
      nric: `900415${Math.floor(100000 + Math.random() * 900000)}`,
      mobile: "+60123456789",
      residentialAddress: "18 Jalan SS 2/24, 47300 Petaling Jaya, Selangor",
    },
  });
  expect(registration.status()).toBe(200);
  await login(outsider, outsiderEmail);
  const outsiderPage = await outsider.newPage();
  await outsiderPage.goto("/account");
  await outsiderPage.getByRole("link", { name: "Account details", exact: true }).click();
  await expect(
    outsiderPage.getByRole("button", { name: "Resend verification email", exact: true }),
  ).toBeVisible();
  await outsiderPage.getByRole("link", { name: "Selling appointments", exact: true }).click();
  await expect(
    outsiderPage.getByText("Verify your email to view your appointment history.", { exact: true }),
  ).toBeVisible();
  await expect(
    outsiderPage.getByText("This account cannot perform this action.", { exact: true }),
  ).toHaveCount(0);
  await outsiderPage.getByRole("link", { name: "Reservations", exact: true }).click();
  const emptyReservations = outsiderPage.getByRole("region", {
    name: "Your reservations",
    exact: true,
  });
  await expect(
    emptyReservations.getByRole("link", { name: "Browse cars", exact: true }),
  ).toBeVisible();
  await expect(emptyReservations.getByText("Reservation history", { exact: true })).toHaveCount(0);
  await expect(
    emptyReservations.getByRole("navigation", { name: "Reservation history pages" }),
  ).toHaveCount(0);
  await outsiderPage.screenshot({ path: path.join(evidence, "account-empty.png"), fullPage: true });
  expect(
    await outsiderPage.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBe(true);
  const cancellationPath = `/api/account/reservations/${held.receipt.id}/cancellations`;
  expect(
    (
      await outsider.request.post(cancellationPath, {
        headers: await requestHeaders(outsider),
        data: { operationId: crypto.randomUUID(), reason: null },
      })
    ).status(),
  ).toBe(404);
  expect(
    (
      await outsider.request.get(
        `/api/account/reservations/${held.receipt.id}/photos/${held.coverPhotoId}`,
        { headers: await requestHeaders(outsider) },
      )
    ).status(),
  ).toBe(404);
  expect(
    (
      await context.request.post(cancellationPath, {
        headers: { ...(await requestHeaders(context)), Origin: "https://outside.invalid" },
        data: { operationId: crypto.randomUUID(), reason: null },
      })
    ).status(),
  ).toBe(403);
  await outsider.close();

  const requests: string[] = [];
  await page.route(`**${cancellationPath}`, async (route) => {
    requests.push(route.request().postData()!);
    const result = await route.fetch();
    expect(result.status()).toBe(200);
    if (requests.length === 1) {
      await route.abort("failed");
      return;
    }
    await route.fulfill({ response: result });
  });
  await active.getByRole("button", { name: "Cancel reservation", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Cancel this reservation?", exact: true });
  await expect(dialog).toContainText("No real money moves");
  await dialog.getByRole("button", { name: "Confirm cancellation", exact: true }).click();
  await expect(
    dialog.getByRole("button", { name: "Keep reservation", exact: true }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Reconcile cancellation", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(active).toHaveCount(0);
  await expect(page.getByText("No active reservation")).toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[0]).toBe(requests[1]);
  const refunded = (
    await (
      await context.request.get("/api/account/reservations?page=1", {
        headers: await requestHeaders(context),
      })
    ).json()
  ).data as AccountReservations;
  expect(refunded.active).toBeNull();
  const receipt = refunded.items.find((item) => item.receipt.id === held.receipt.id);
  expect(receipt?.release?.id).toBe(JSON.parse(requests[0]).operationId);
  expect(receipt?.release?.refund).toBe(500);
  writeFileSync(
    path.join(evidence, "customer-account-cancellation.json"),
    JSON.stringify({ reservationId: held.receipt.id, requests, release: receipt?.release }),
  );
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.goto("/account");
  await expect(page.getByText("No active reservation")).toBeVisible();
  const retained = page.getByRole("region", { name: "Your reservations", exact: true });
  await expect(retained.getByText("Cancelled by you", { exact: true }).first()).toBeVisible();

  const manager = await browser.newContext({ ...info.project.use, baseURL: origin });
  await login(manager, "siti.aminah@example.invalid");
  await context.addCookies(await manager.cookies());
  await retained.getByRole("button", { name: "Next reservations", exact: true }).click();
  await expect(page.getByRole("region", { name: "Account details", exact: true })).toHaveCount(0);
  await expect(page).toHaveURL(/\/dashboard\//);
  await manager.close();
});
