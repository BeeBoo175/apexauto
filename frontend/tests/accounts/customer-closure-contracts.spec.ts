import { expect, test } from "@playwright/test";
import {
  closureConflict,
  parseClosurePreview,
  parseClosureReceipt,
  parseClosureRequest,
} from "@/lib/accounts/customer-closure";
import { accountEventTitles } from "@/lib/accounts/account-history";

const retained = [
  { name: "Reservation records", detail: "Prices and dates stay.", endsWhen: "Seven years" },
];

test("the closure preview needs both the blockers and the retained categories", () => {
  const blocker = {
    type: "Reservation",
    reference: "R-1",
    status: "Active",
    branchName: "Glenmarie",
    resolution: "Cancel the reservation.",
  };
  expect(parseClosurePreview({ blockers: [blocker], retained })?.blockers).toHaveLength(1);
  expect(parseClosurePreview({ blockers: [], retained: [] })?.retained).toEqual([]);
  expect(parseClosurePreview({ blockers: [] })).toBeNull();
  expect(parseClosurePreview({ blockers: [{ ...blocker, resolution: 1 }], retained })).toBeNull();
});

test("the closure receipt is strict about its identity, time and retained records", () => {
  const receipt = {
    receiptId: "5c0c5e34-6a8e-4b8e-a1b5-5d6f8f6f0a11",
    closedAt: "2026-11-16T04:00:00.000Z",
    fileRemovalPending: true,
    retained,
  };
  expect(parseClosureReceipt(receipt)?.fileRemovalPending).toBe(true);
  expect(parseClosureReceipt({ ...receipt, receiptId: "nope" })).toBeNull();
  expect(parseClosureReceipt({ ...receipt, retained: [{ name: "x" }] })).toBeNull();
});

test("only the five documented conflict codes map to a message and an unknown one stays generic", () => {
  for (const code of [
    "wrong-password",
    "blocked",
    "protected-sample",
    "session-changed",
    "retry-later",
  ]) {
    expect(closureConflict(code)?.code).toBe(code);
  }
  expect(closureConflict("other")).toBeUndefined();
  expect(closureConflict(undefined)).toBeUndefined();
});

test("the confirmation body is exactly an operation id and a bounded password", () => {
  const operationId = "5c0c5e34-6a8e-4b8e-a1b5-5d6f8f6f0a11";
  expect(parseClosureRequest({ operationId, password: "Password123!" })).toEqual({
    operationId,
    password: "Password123!",
  });
  expect(parseClosureRequest({ operationId, password: "" })).toBeNull();
  expect(parseClosureRequest({ operationId, password: "x".repeat(129) })).toBeNull();
  expect(parseClosureRequest({ operationId: "bad", password: "Password123!" })).toBeNull();
});

test("the account history accepts the erased event kind", () => {
  expect(accountEventTitles.Erased).toBeTruthy();
});
