import { expect, test } from "@playwright/test";
import {
  historyChange,
  historyLink,
  historySummary,
  historyVersionTitle,
  parseSaleHistory,
} from "@/lib/sales/sale-history";

const agent = "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e";

test("history validates entries and shows readable changes", () => {
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
        documents: [],
      },
    ],
    totalCount: 1,
    page: 1,
    pageSize: 25,
  };
  expect(parseSaleHistory(history)?.totalCount).toBe(1);
  const issued = { id: agent, kind: "PaymentReceipt", number: "VRC-2026-000001" };
  const withDocuments = (documents: unknown) => ({
    ...history,
    items: [{ ...history.items[0], documents }],
  });
  expect(parseSaleHistory(withDocuments([issued]))?.items[0].documents).toEqual([issued]);
  for (const documents of [
    undefined,
    [issued, issued],
    [{ ...issued, kind: "Unknown" }],
    [{ ...issued, id: "invalid" }],
  ]) {
    expect(parseSaleHistory(withDocuments(documents))).toBeNull();
  }
  expect(
    parseSaleHistory({ ...history, items: [{ ...history.items[0], toStatus: "Draft" }] }),
  ).toBeNull();
});

test("history links only financial milestones and names their retained contents", () => {
  const entry = { kind: "Created", toStatus: "Open" as const, version: 1, changes: [] };
  expect(historyLink(entry)).toBe("View original pricing · v1");
  expect(historyLink({ ...entry, kind: "TermsSaved", version: 3 })).toBe("View saved terms · v3");
  expect(
    historyLink({ ...entry, kind: "Submitted", toStatus: "PendingApproval", version: 4 }),
  ).toBe("View submitted deal · v4");
  expect(historyLink({ ...entry, kind: "Submitted", toStatus: "Approved", version: 4 })).toBe(
    "View approved deal · v4",
  );
  for (const kind of [
    "Claimed",
    "Assigned",
    "Reassigned",
    "Released",
    "Approved",
    "Rejected",
    "Cancelled",
    "CustomerCancelled",
    "Expired",
    "Withdrawn",
    "Unknown",
  ]) {
    expect(historyLink({ ...entry, kind }), kind).toBeNull();
  }
  expect(
    historyLink({
      ...entry,
      kind: "Released",
      version: 5,
      changes: [{ field: "Warranty", before: "Gold", after: null }],
    }),
  ).toBe("View reset terms · v5");
  expect(historyVersionTitle({ kind: "Approved", toStatus: "Approved" })).toBe("Approved deal");
});

test("history explains approval withdrawal and release without requiring another snapshot", () => {
  const entry = {
    kind: "TermsSaved",
    fromStatus: "Approved" as const,
    agent: null,
    previousAgent: null,
  };
  expect(historySummary(entry)).toContain("Previous approval withdrawn");
  expect(historySummary({ ...entry, fromStatus: "Handling" })).toBeNull();
  expect(
    historySummary({ ...entry, kind: "Released", previousAgent: "agent@example.invalid" }),
  ).toContain("agent@example.invalid released");
  expect(historySummary({ ...entry, kind: "Released" })).toContain("hold timer resumed");
  expect(historySummary({ ...entry, kind: "Approved" })).toContain("without changes");
  expect(historySummary({ ...entry, kind: "Rejected" })).toContain("kept");
});
