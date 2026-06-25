import { expect, test } from "@playwright/test";
import {
  isSaleReason,
  normalizeSaleReason,
  parseSale,
  parseSaleReceipt,
  parseSalesQueue,
  parseSalesTable,
} from "@/lib/sales/parse";
import { createInteractionGuard } from "@/lib/interaction-guard";
import { parseMyr } from "@/lib/currency-input";
import { queueCountOf, queueViewOf, saleReference } from "@/lib/sales/contracts";
import { queueParams, queueQuery, saleSelection } from "@/lib/sales/query";
import { isSaleVersionSection, saleSections, saleSectionLabels } from "@/lib/sales/sale-sections";
import { parseSaleVehicle } from "@/lib/sales/vehicle-reference";

const id = "8a3b0c1e-2f44-4f3b-9c55-2a7d1e0f9b11";
const agent = "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e";

test("decision drafts normalize outer whitespace while commands enforce the backend reason contract", () => {
  for (const draft of ["sdsd", "sdsd ", " sdsd", "\nsdsd\n", "\u00a0sdsd\u00a0"]) {
    expect(normalizeSaleReason(draft)).toBe("sdsd");
    expect(isSaleReason(normalizeSaleReason(draft))).toBe(true);
  }
  expect(normalizeSaleReason("a".repeat(1000))).toHaveLength(1000);
  for (const draft of ["", "   ", "\n", "a".repeat(1001), "needs\nrevision", "reason\u0085text"]) {
    expect(normalizeSaleReason(draft)).toBeNull();
  }
  expect(isSaleReason("sdsd ")).toBe(false);
});

test("money drafts accept surrounding spaces without changing precision or accepting embedded spaces", () => {
  expect(parseMyr(" 1250.50 ")).toBe(1250.5);
  expect(parseMyr("\u00a01250.50\u00a0")).toBe(1250.5);
  for (const value of [" ", "12 50", "1.001", "-1", "1,000", "1e3"]) {
    expect(parseMyr(value)).toBeNull();
  }
});

test("navigation confirms all draft owners once and never discards an unconfirmed command", () => {
  const guard = createInteractionGuard();
  const first = Symbol("terms");
  const second = Symbol("notes");
  const operation = Symbol("save");
  const discarded: string[] = [];
  let navigations = 0;
  guard.setLocked(first, {
    kind: "draft",
    label: "Terms",
    discard: () => {
      discarded.push("terms");
      guard.setLocked(first, false);
    },
  });
  guard.setLocked(second, {
    kind: "draft",
    label: "Notes",
    discard: () => {
      discarded.push("notes");
      guard.setLocked(second, false);
    },
  });
  const navigate = () => {
    navigations += 1;
  };
  guard.request(navigate);
  expect(guard.getSnapshot().intent).not.toBeNull();
  guard.cancel();
  expect(discarded).toEqual([]);
  expect(navigations).toBe(0);
  guard.request(navigate);
  guard.setLocked(operation, { kind: "uncertain", label: "Sale submission" });
  guard.confirm();
  expect(discarded).toEqual([]);
  expect(navigations).toBe(0);
  expect(guard.getSnapshot().notice?.message).toContain("reconcile");
  guard.setLocked(operation, false);
  guard.request(navigate);
  guard.confirm();
  expect(discarded).toEqual(["terms", "notes"]);
  expect(navigations).toBe(1);
  expect(guard.isLocked()).toBe(false);
  guard.confirm();
  expect(navigations).toBe(1);
});

test("a new editor requires a fresh confirmation and releasing it preserves other owners", () => {
  const guard = createInteractionGuard();
  const first = Symbol("inspection");
  const second = Symbol("photo");
  let discarded = false;
  let navigated = false;
  guard.setLocked(first, {
    kind: "draft",
    label: "Inspection",
    discard: () => {
      discarded = true;
      guard.setLocked(first, false);
    },
  });
  guard.request(() => {
    navigated = true;
  });
  guard.setLocked(second, {
    kind: "draft",
    label: "Photo",
    discard: () => guard.setLocked(second, false),
  });
  guard.confirm();
  expect(discarded).toBe(false);
  expect(navigated).toBe(false);
  guard.setLocked(second, false);
  expect(guard.isLocked()).toBe(true);
  guard.confirm();
  expect(discarded).toBe(true);
  expect(navigated).toBe(true);
});

test("the same editor becoming unconfirmed invalidates an already open discard confirmation", () => {
  const guard = createInteractionGuard();
  const owner = Symbol("sale editor");
  let discarded = false;
  let navigated = false;
  guard.setLocked(owner, {
    kind: "draft",
    label: "Sale terms",
    discard: () => {
      discarded = true;
    },
  });
  guard.request(() => {
    navigated = true;
  });
  guard.setLocked(owner, { kind: "uncertain", label: "Saving sale terms" });
  guard.confirm();
  expect(discarded).toBe(false);
  expect(navigated).toBe(false);
  expect(guard.getSnapshot().intent).toBeNull();
  expect(guard.getSnapshot().notice?.message).toContain("reconcile");
});

test("Customer and Car are separate sections and only Customer and Deal open sale versions", () => {
  expect(saleSections.map((section) => saleSectionLabels[section])).toEqual([
    "Customer",
    "Car",
    "Deal",
    "Delivery",
    "History",
  ]);
  expect(saleSelection({ sale: id, section: "car", version: "3" })).toEqual({
    saleId: id,
    section: "car",
    version: null,
  });
  expect(saleSelection({ sale: id, section: "customer", version: "3" }).version).toBe(3);
  expect(saleSelection({ sale: id, section: "deal", version: "4" }).version).toBe(4);
  expect(saleSelection({ sale: id, section: "history", version: "3" }).version).toBeNull();
  expect(saleSelection({ sale: id, section: "delivery", version: "3" })).toEqual({
    saleId: id,
    section: "delivery",
    version: null,
  });
  expect(saleSections.filter(isSaleVersionSection)).toEqual(["customer", "deal"]);
});

test("vehicle references validate retained listing content without manager workspace metadata", () => {
  const reference = {
    saleId: id,
    make: "Perodua",
    model: "Myvi",
    trim: "1.5 AV",
    manufactureYear: 2020,
    mileageKilometres: 90000,
    branchName: "Petaling Jaya",
    publishedAt: "2026-10-30T02:00:00+00:00",
    listing: {
      vehicleId: agent,
      vehiclePrice: 60000,
      roadTax: 90,
      transmission: "Automatic",
      fuel: "Petrol",
      description: "Inspected Malaysian vehicle",
      photoIds: [agent],
    },
  };
  expect(parseSaleVehicle(reference)?.listing.transmission).toBe("Automatic");
  expect(parseSaleVehicle({ ...reference, listing: { ...reference.listing, fuel: 0 } })).toBeNull();
  expect(parseSaleVehicle({ ...reference, saleId: "invalid" })).toBeNull();
  expect(parseSaleVehicle({ ...reference, mileageKilometres: -1 })).toBeNull();
  expect(parseSaleVehicle({ ...reference, publishedAt: "yesterday" })).toBeNull();
  for (const change of [
    { photoIds: [agent, agent] },
    { photoIds: ["bad"] },
    { fuel: 99 },
    { vehiclePrice: "60000" },
  ]) {
    expect(
      parseSaleVehicle({ ...reference, listing: { ...reference.listing, ...change } }),
    ).toBeNull();
  }
});

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
  paymentEvidence: null,
  settledAt: null,
  acquiredVehicleId: null,
  handoverEvidence: null,
  deliveredAt: null,
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
  agentId: id,
  agentName: "Aina Rahman",
  agentPictureId: null,
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
  updatedAt: "2026-10-31T03:00:00+00:00",
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
    counts: { toClaim: 2, handling: 1, waiting: 0, settling: 3, completed: 4 },
  };
  expect(parseSalesQueue(queue)?.counts.toClaim).toBe(2);
  expect(parseSalesQueue({ ...queue, items: [summary, summary], total: 2 })).toBeNull();
  const table = {
    ...queue,
    counts: {
      needsApproval: 0,
      open: 2,
      beingHandled: 1,
      approved: 0,
      awaitingHandover: 3,
      delivered: 4,
      history: 4,
    },
    view: "Open",
  };
  expect(parseSalesTable(table)?.view).toBe("Open");
  expect(parseSalesTable({ ...table, view: "Deals" })).toBeNull();
  expect(parseSalesTable({ ...table, items: [{ ...summary, updatedAt: "invalid" }] })).toBeNull();
  expect(saleReference(summary)).toBe("VRS-2026-000001");
  expect(saleReference({ ...summary, orderNumber: "VPO-2026-000001" })).toBe("VPO-2026-000001");
  for (const status of ["Approved", "Settled", "Delivered"] as const) {
    const view = queueViewOf[status]!;
    expect(view).toBe(status === "Delivered" ? "Completed" : "Settling");
    const parsed = parseSalesQueue({ ...queue, items: [{ ...summary, status }] });
    expect(parsed?.counts[queueCountOf[view]]).toBe(status === "Delivered" ? 4 : 3);
    expect(new URLSearchParams(queueQuery(view)).get("view")).toBe(view);
  }
  expect(queueParams({ view: "settling" })).toBe("Settling");
  expect(queueParams({ view: "completed" })).toBe("Completed");
  expect(queueParams({ view: "approved" })).toBeNull();
  expect(
    parseSalesQueue({ ...queue, counts: { toClaim: 2, handling: 1, waiting: 0, approved: 0 } }),
  ).toBeNull();
  for (const view of ["AwaitingHandover", "Delivered"] as const) {
    expect(parseSalesTable({ ...table, view })?.view).toBe(view);
    expect(
      parseSalesTable({ ...table, view, counts: { ...table.counts, delivered: -1 } }),
    ).toBeNull();
  }
});
