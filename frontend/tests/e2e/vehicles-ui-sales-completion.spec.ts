import { expect, test, type BrowserContext, type Page } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import type { Sale } from "@/lib/sales/contracts";
import type { SaleDocument } from "@/lib/sales/documents";
import { apiLogin, privateHeaders } from "./staff-session";

const evidence = process.env.BROWSER_EVIDENCE_DIR!;
const origin = process.env.APP_BASE_URL!;
const branch = "c77708bf-1a31-42f5-9a69-0c8766697a52";

async function read(context: BrowserContext, resource: string) {
  return context.request.get(resource, { headers: await privateHeaders(context) });
}

async function command(
  context: BrowserContext,
  id: string,
  name: string,
  version: number,
  fields = {},
) {
  const response = await context.request.post(`/api/staff/sales/${id}/${name}`, {
    headers: await privateHeaders(context),
    data: { operationId: crypto.randomUUID(), expectedVersion: version, ...fields },
  });
  expect(response.status(), await response.text()).toBe(200);
}

async function readSale(context: BrowserContext, id: string): Promise<Sale> {
  const response = await read(context, `/api/staff/sales/${id}`);
  expect(response.status()).toBe(200);
  return (await response.json()).data;
}

async function confirm(page: Page, title: string, action: string) {
  const dialog = page.getByRole("dialog", { name: title, exact: true });
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: action, exact: true }).click();
  await expect(dialog).not.toBeVisible();
}

test("sales settlement, handover, corrections and own customer PDFs retain exact recovery", async ({
  page,
  context,
  browser,
}, info) => {
  test.setTimeout(240_000);
  const customer = await browser.newContext({ ...info.project.use, baseURL: origin });
  const manager = await browser.newContext({ ...info.project.use, baseURL: origin });
  const outsider = await browser.newContext({ ...info.project.use, baseURL: origin });
  try {
    await apiLogin(context, "aina.sofia@example.invalid");
    await apiLogin(customer, "ahmad.faizal@example.invalid");
    await apiLogin(manager, "siti.aminah@example.invalid");
    await apiLogin(outsider, "nur.aisyah@example.invalid");
    const holds = (await (await read(customer, "/api/account/reservations")).json()).data;
    let id: string = holds.active?.receipt.id;
    if (!id) {
      const catalogue = (await (await read(customer, `/api/vehicles?branchId=${branch}`)).json())
        .data;
      const vehicle = catalogue.items[0];
      const listing = (await (await read(customer, `/api/vehicles/${vehicle.id}`)).json()).data;
      const reserved = await customer.request.post(`/api/vehicles/${vehicle.id}/reservations`, {
        headers: await privateHeaders(customer),
        data: {
          operationId: crypto.randomUUID(),
          publicationId: listing.publicationId,
          promoId: null,
        },
      });
      expect(reserved.status()).toBe(200);
      id = (await reserved.json()).data.id;
    }
    let sale = await readSale(context, id);
    if (sale.sale.status === "Open") {
      await command(context, id, "claims", sale.sale.version);
      sale = await readSale(context, id);
    }
    if (sale.sale.status === "Handling") {
      await command(context, id, "submissions", sale.sale.version);
      sale = await readSale(context, id);
    }
    if (sale.sale.status === "PendingApproval") {
      await command(manager, id, "approvals", sale.sale.version);
      sale = await readSale(context, id);
    }
    expect(sale.sale.status).toBe("Approved");
    await page.goto(`/dashboard/sales/${id}?view=settling`);
    const selected = page.getByRole("region", { name: "Selected sale", exact: true });
    await selected.getByRole("button", { name: "Open delivery", exact: true }).click();
    await selected.getByRole("button", { name: "Review settlement", exact: true }).click();
    await selected
      .getByLabel("Buyer address", { exact: true })
      .fill("24 Jalan Anggerik, Shah Alam, Selangor");
    await selected
      .getByRole("navigation", { name: "Sale sections" })
      .getByRole("button", { name: "Deal", exact: true })
      .click();
    await expect(
      selected.getByRole("heading", { name: "Agree deal terms", exact: true }),
    ).toBeVisible();
    await selected
      .getByRole("navigation", { name: "Sale sections" })
      .getByRole("button", { name: "Customer", exact: true })
      .click();
    await selected.getByRole("button", { name: "Return to editing", exact: true }).click();
    await expect(selected.getByLabel("Buyer address", { exact: true })).toHaveValue(
      "24 Jalan Anggerik, Shah Alam, Selangor",
    );
    await selected
      .getByLabel("I have reviewed these buyer and vehicle details for the issued documents")
      .check();
    const hirePurchase = info.project.name === "mobile";
    if (hirePurchase) {
      await selected.getByLabel("Payment method", { exact: true }).selectOption("HirePurchase");
      await selected.getByLabel("Received customer contribution (RM)", { exact: true }).fill("0");
      await selected.getByLabel("Financing bank", { exact: true }).fill("Maybank");
      await selected
        .getByLabel("Financing amount (RM)", { exact: true })
        .fill(String(sale.sale.balancePayable));
      await selected
        .getByLabel("Letter of Undertaking reference", { exact: true })
        .fill("LOU-48271");
      await selected.getByLabel("I have verified the Letter of Undertaking").check();
    } else {
      await selected
        .getByLabel("Received amount (RM)", { exact: true })
        .fill(String(sale.sale.balancePayable));
      await selected.getByLabel("Payment reference", { exact: true }).fill("CASH-48271");
      await selected.getByLabel("Payment date", { exact: true }).fill("2026-11-02");
    }
    await page.screenshot({ path: path.join(evidence, "settlement-form.png"), fullPage: true });
    const settlementRequests: string[] = [];
    await page.route(`**/api/staff/sales/${id}/settlements`, async (route) => {
      settlementRequests.push(route.request().postData()!);
      const response = await route.fetch();
      expect(response.status(), await response.text()).toBe(200);
      if (settlementRequests.length === 1) {
        await route.abort("failed");
      } else {
        await route.fulfill({ response });
      }
    });
    await selected.getByRole("button", { name: "Confirm settlement", exact: true }).click();
    await confirm(page, "Confirm settlement?", "Confirm settlement");
    await selected.getByRole("button", { name: "Reconcile settlement", exact: true }).click();
    await expect(
      selected.getByRole("button", { name: "Prepare handover", exact: true }),
    ).toBeVisible();
    expect(settlementRequests).toHaveLength(2);
    expect(settlementRequests[0]).toBe(settlementRequests[1]);
    await page.unroute(`**/api/staff/sales/${id}/settlements`);
    const settled = await readSale(context, id);
    expect(settled.sale.status).toBe("Settled");
    expect(settled.sale.paymentEvidence?.receivedAmount).toBe(
      hirePurchase ? 0 : sale.sale.balancePayable,
    );
    expect(settled.sale.paymentEvidence?.financingAmount).toBe(
      hirePurchase ? sale.sale.balancePayable : 0,
    );
    const documentsBefore = (await (await read(context, `/api/sales/${id}/documents`)).json())
      .data as SaleDocument[];
    expect(documentsBefore).toHaveLength(2);
    await expect(selected.getByRole("link", { name: /Purchase order.*PDF/ })).toBeVisible();
    await selected.getByRole("button", { name: "Prepare handover", exact: true }).click();
    await selected
      .getByLabel("Final odometer (km)", { exact: true })
      .fill(String(settled.context.mileageKilometres + 1));
    await selected.getByLabel("Number of keys handed over", { exact: true }).fill("2");
    await selected.getByLabel("Customer acknowledges receipt of the vehicle").check();
    await selected.getByLabel("Ownership transfer is complete").check();
    await selected.getByLabel("Insurance is confirmed").check();
    if (info.project.name === "mobile") {
      expect(
        await selected
          .getByLabel("Insurance is confirmed")
          .evaluate((input) => input.closest("label")!.getBoundingClientRect().height),
      ).toBeGreaterThanOrEqual(44);
    }
    await page.screenshot({ path: path.join(evidence, "handover-form.png"), fullPage: true });
    await selected.getByRole("button", { name: "Complete handover", exact: true }).click();
    await confirm(page, "Complete handover?", "Complete handover");
    await expect(selected.getByText("Delivered", { exact: true }).first()).toBeVisible();
    const delivered = await readSale(context, id);
    expect(delivered.sale.status).toBe("Delivered");
    expect(delivered.sale.handoverEvidence?.keysDelivered).toBe(2);
    await expect(
      selected.getByRole("button", { name: "Review settlement", exact: true }),
    ).toHaveCount(0);
    await page.screenshot({ path: path.join(evidence, "delivered-sale.png"), fullPage: true });

    const managerPage = await manager.newPage();
    await managerPage.goto(`/dashboard/sales/${id}/delivery?view=delivered`);
    const drawer = managerPage.getByRole("dialog").filter({
      has: managerPage.getByRole("heading", { name: delivered.sale.orderNumber!, exact: true }),
    });
    await expect(drawer).toBeVisible();
    await expect(
      drawer.getByRole("button", { name: "Review settlement", exact: true }),
    ).toHaveCount(0);
    await drawer
      .locator("article")
      .filter({ hasText: "Payment receipt" })
      .locator("summary")
      .click();
    await drawer
      .getByRole("button", { name: "Correct buyer details", exact: true })
      .first()
      .click();
    await drawer
      .getByLabel("Buyer address", { exact: true })
      .fill("24 Jalan Anggerik, Seksyen 2, Shah Alam");
    await drawer
      .getByLabel("Correction reason", { exact: true })
      .fill("Correct buyer address spelling");
    await managerPage.screenshot({
      path: path.join(evidence, "document-correction.png"),
      fullPage: true,
    });
    const correctionRequests: string[] = [];
    await managerPage.route(`**/api/staff/sales/${id}/document-corrections`, async (route) => {
      correctionRequests.push(route.request().postData()!);
      const response = await route.fetch();
      expect(response.status()).toBe(200);
      if (correctionRequests.length === 1) {
        await route.abort("failed");
      } else {
        await route.fulfill({ response });
      }
    });
    await drawer.getByRole("button", { name: "Void and reissue", exact: true }).click();
    await confirm(managerPage, "Void and reissue this document?", "Void and reissue");
    await drawer.getByRole("button", { name: "Reconcile correction", exact: true }).click();
    expect(correctionRequests).toHaveLength(2);
    expect(correctionRequests[0]).toBe(correctionRequests[1]);
    await expect(drawer.getByText("Corrected", { exact: true })).toBeVisible();
    await drawer
      .locator("article")
      .filter({ hasText: "Payment receipt" })
      .locator("summary")
      .click();
    await expect(
      drawer.getByRole("button", { name: "View previous document", exact: true }),
    ).toBeVisible();
    await managerPage.unroute(`**/api/staff/sales/${id}/document-corrections`);
    expect((await readSale(context, id)).sale.version).toBe(delivered.sale.version);
    await drawer
      .getByRole("navigation", { name: "Sale sections" })
      .getByRole("button", { name: "History", exact: true })
      .click();
    const history = drawer.getByRole("region", { name: "Sale history", exact: true });
    for (const document of documentsBefore) {
      await expect(
        history.getByRole("link", { name: new RegExp(document.number) }),
      ).toHaveAttribute("href", `/api/sales/${id}/documents/${document.id}/pdf`);
    }
    await managerPage.screenshot({
      path: path.join(evidence, "history-documents.png"),
      fullPage: true,
    });
    await managerPage.keyboard.press("Escape");
    await expect(drawer).not.toBeVisible();
    await managerPage.screenshot({
      path: path.join(evidence, "manager-stages.png"),
      fullPage: true,
    });

    await apiLogin(manager, "farah.aziz@example.invalid");
    const awaiting = await read(
      manager,
      "/api/staff/sales?view=awaitingHandover&registration=PQA%206312",
    );
    expect(awaiting.status()).toBe(200);
    const tradeIn = (await awaiting.json()).data.items.find(
      (item: { registration: string }) => item.registration === "PQA 6312",
    );
    expect(tradeIn).toBeTruthy();
    const covered = await readSale(manager, tradeIn.id);
    expect(covered.sale.negotiatedDiscount).toBe(0);
    expect(covered.sale.balancePayable).toBe(0);
    expect(covered.sale.tradeInAllowance).toBeGreaterThan(covered.sale.depositCredit);
    await managerPage.goto(`/dashboard/sales/${tradeIn.id}/delivery?view=awaitingHandover`);
    const coveredDrawer = managerPage.getByRole("dialog").filter({
      has: managerPage.getByRole("heading", { name: covered.sale.orderNumber!, exact: true }),
    });
    await expect(
      coveredDrawer.getByText("No additional payment required", { exact: true }),
    ).toBeVisible();
    await expect(coveredDrawer.getByText("Discount over limit", { exact: true })).toHaveCount(0);
    await managerPage.screenshot({
      path: path.join(evidence, "trade-in-settlement.png"),
      fullPage: true,
    });

    const customerPage = await customer.newPage();
    const counts = () =>
      JSON.parse(readFileSync(path.join(evidence, "counts.json"), "utf8")) as Record<
        string,
        number
      >;
    const before = counts()[`/api/sales/${id}/documents`] ?? 0;
    const documentResponse = await customerPage.goto(`/account/sales/${id}/documents`);
    expect(documentResponse!.status()).toBe(200);
    const html = await documentResponse!.text();
    expect(html).toContain("Your sale documents");
    expect(html).toContain("VHN-");
    expect((counts()[`/api/sales/${id}/documents`] ?? 0) - before).toBe(1);
    await expect(
      customerPage.getByRole("link", { name: "Open signed inspection report (PDF)", exact: true }),
    ).toBeVisible();
    const issued = (await (await read(customer, `/api/sales/${id}/documents`)).json())
      .data as SaleDocument[];
    expect(issued).toHaveLength(3);
    for (const document of issued) {
      const response = await read(customer, `/api/sales/${id}/documents/${document.id}/pdf`);
      expect(response.status()).toBe(200);
      expect(response.headers()["content-type"]).toBe("application/pdf");
      expect(response.headers()["cache-control"]).toContain("no-store");
      const bytes = await response.body();
      expect(bytes.subarray(0, 5).toString()).toBe("%PDF-");
      writeFileSync(path.join(evidence, `${document.kind}.pdf`), bytes);
    }
    await customerPage.getByRole("button", { name: "View previous document", exact: true }).click();
    await expect(customerPage.getByText(/Superseded/)).toBeVisible();
    await customerPage
      .getByRole("button", { name: "View replacement document", exact: true })
      .click();
    await expect(
      customerPage.getByRole("button", { name: "View previous document", exact: true }),
    ).toBeEnabled();
    await expect(customerPage.getByText(/Superseded/)).toHaveCount(0);
    await customerPage.screenshot({
      path: path.join(evidence, "customer-documents.png"),
      fullPage: true,
    });
    expect(
      await customerPage.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    const reportHref = await customerPage
      .getByRole("link", { name: "Open signed inspection report (PDF)", exact: true })
      .getAttribute("href");
    expect((await read(customer, reportHref!)).status()).toBe(200);
    expect((await read(outsider, `/api/sales/${id}/documents`)).status()).toBe(404);
    expect((await read(outsider, `/api/sales/${id}/documents/${issued[0].id}/pdf`)).status()).toBe(
      404,
    );
    await customerPage.goto(
      `/account?search=${encodeURIComponent(sale.context.reservationNumber)}`,
      {
        waitUntil: "domcontentloaded",
        timeout: 30_000,
      },
    );
    const purchased = customerPage.locator("details").filter({ hasText: "Collected" }).first();
    await expect(purchased).toBeVisible();
    await purchased.locator("summary").click({ timeout: 15_000 });
    await expect(
      customerPage.getByRole("link", { name: "View sale documents", exact: true }).first(),
    ).toBeVisible();
    writeFileSync(
      path.join(evidence, "sales-completion.json"),
      JSON.stringify({
        saleId: id,
        settledVersion: settled.sale.version,
        deliveredVersion: delivered.sale.version,
        exactSettlementReplay: settlementRequests[0] === settlementRequests[1],
        exactCorrectionReplay: correctionRequests[0] === correctionRequests[1],
        documentIds: issued.map((document) => document.id),
        counts: counts(),
      }),
    );
  } finally {
    await Promise.allSettled([customer.close(), manager.close(), outsider.close()]);
  }
});
