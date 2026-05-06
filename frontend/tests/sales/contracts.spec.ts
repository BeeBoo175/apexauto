import { expect, test } from "@playwright/test";
import { parseSale, parseSaleReceipt } from "@/lib/sales/sale";
import { historyChange, parseSaleHistory, versionSection } from "@/lib/sales/sale-history";
import {
  parseSalesQueue,
  parseSalesTable,
  saleReference,
  tableParams,
  tableQuery,
} from "@/lib/sales/sales";

const id = "8a3b0c1e-2f44-4f3b-9c55-2a7d1e0f9b11";
const agent = "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e";

const snapshot = {
  status: "Handling",
  agentId: agent,
  claimedAt: "2026-10-31T02:00:00+00:00",
  dueAt: null,
  pausedSeconds: 64800,
  orderNumber: null,
  promoCode: null,
  basePrice: 60000,
  roadTax: 90,
  processingFee: 500,
  sstRate: 0.08,
  serviceTax: 40,
  promoDiscount: 0,
  negotiatedDiscount: 300,
  warrantyPackageId: null,
  warrantyName: null,
  warrantyPrice: 0,
  depositCredit: 500,
  tradeInAppointmentId: null,
  tradeInLabel: null,
  tradeInAllowance: 0,
  balancePayable: 59830,
  submittedAt: null,
  discountLimitAtSubmit: null,
  decidedById: null,
  decidedAt: null,
  closedAt: null,
  version: 3,
};

const summary = {
  id,
  reservationNumber: "VRS-2026-000001",
  orderNumber: null,
  status: "Handling",
  registration: "WYY 8801",
  manufactureYear: 2020,
  make: "Perodua",
  model: "Myvi",
  trim: "1.5 AV",
  customer: "Nur Aina Abdullah",
  agent: "agent@example.invalid",
  branch: "Petaling Jaya",
  dueAt: null,
  pausedSeconds: 64800,
  claimedAt: "2026-10-31T02:00:00+00:00",
  negotiatedDiscount: 300,
  discountLimitAtSubmit: null,
  balancePayable: 59830,
  submittedAt: null,
  decidedAt: null,
  closedAt: null,
  outcome: null,
  version: 3,
};

test("a sale carries its snapshot, context and server-decided actions or is refused", () => {
  const sale = {
    id,
    sale: snapshot,
    agent: "agent@example.invalid",
    decidedBy: null,
    createdAt: "2026-10-30T02:00:00+00:00",
    updatedAt: "2026-10-31T03:00:00+00:00",
    context: {
      reservationNumber: "VRS-2026-000001",
      registration: "WYY 8801",
      vehicle: "2020 Perodua Myvi 1.5 AV",
      vin: "PL1BT000000000001",
      mileageKilometres: 90000,
      customer: "Nur Aina Abdullah",
      customerMobile: "0123456789",
      customerEmail: "aina@example.invalid",
      branch: "Petaling Jaya",
      reservedAt: "2026-10-30T02:00:00+00:00",
      warrantyMonths: null,
      warrantyMileageCapKm: null,
      tradeInMinimum: null,
      tradeInMaximum: null,
    },
    actions: {
      claim: false,
      assign: false,
      release: true,
      saveTerms: true,
      submit: true,
      decide: false,
      cancel: true,
    },
  };
  expect(parseSale(sale)?.sale.version).toBe(3);
  expect(parseSale({ ...sale, sale: { ...snapshot, status: "Draft" } })).toBeNull();
  expect(parseSale({ ...sale, actions: { ...sale.actions, decide: "no" } })).toBeNull();
  expect(
    parseSaleReceipt({
      operationId: agent,
      saleId: id,
      kind: "TermsSaved",
      version: 3,
      status: "Handling",
      recordedAt: "2026-10-31T03:00:00+00:00",
    }),
  ).not.toBeNull();
});

test("queues and tables parse their counts and the table names the view it chose", () => {
  const queue = {
    items: [summary],
    total: 1,
    page: 1,
    pageSize: 25,
    counts: { toClaim: 2, handling: 1, waiting: 0, approved: 0 },
  };
  expect(parseSalesQueue(queue)?.counts.toClaim).toBe(2);
  expect(parseSalesQueue({ ...queue, items: [summary, summary], total: 2 })).toBeNull();
  const table = {
    ...queue,
    counts: { needsApproval: 0, open: 2, beingHandled: 1, approved: 0, history: 4 },
    view: "Open",
  };
  expect(parseSalesTable(table)?.view).toBe("Open");
  expect(parseSalesTable({ ...table, view: "Deals" })).toBeNull();
  expect(saleReference(summary)).toBe("VRS-2026-000001");
  expect(saleReference({ ...summary, orderNumber: "VPO-2026-000001" })).toBe("VPO-2026-000001");
});

test("table filters round-trip through the address and leave an unset view to the server", () => {
  const { filters, page } = tableParams({
    view: "History",
    outcome: "Expired",
    page: "2",
    sort: "x",
  });
  expect(filters.sort).toBe("");
  const query = new URLSearchParams(tableQuery(filters, page));
  expect([query.get("view"), query.get("outcome"), query.get("page")]).toEqual([
    "History",
    "Expired",
    "2",
  ]);
  expect(new URLSearchParams(tableQuery(tableParams({}).filters)).has("view")).toBe(false);
});

test("history entries open the section their action belongs to and show readable changes", () => {
  expect(["Claimed", "Assigned", "Reassigned", "Released", "Expired"].map(versionSection)).toEqual(
    Array(5).fill("customer"),
  );
  expect(
    ["Created", "TermsSaved", "Submitted", "Approved", "Rejected", "Cancelled"].map(versionSection),
  ).toEqual(Array(6).fill("deal"));
  expect(historyChange({ field: "Warranty", before: null, after: "ApexShield Gold" })).toBe(
    "Warranty None → ApexShield Gold",
  );
  const history = {
    items: [
      {
        id: agent,
        version: 2,
        kind: "Claimed",
        recordedAt: "2026-10-31T02:00:00+00:00",
        actor: "agent@example.invalid",
        agent: "agent@example.invalid",
        previousAgent: null,
        fromStatus: "Open",
        toStatus: "Handling",
        reason: null,
        changes: [],
        refund: null,
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  };
  expect(parseSaleHistory(history)?.totalCount).toBe(1);
  expect(
    parseSaleHistory({ ...history, items: [{ ...history.items[0], toStatus: "Draft" }] }),
  ).toBeNull();
});
