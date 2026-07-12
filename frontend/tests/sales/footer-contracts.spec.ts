import { expect, test } from "@playwright/test";
import type { SaleActions, SaleStatus } from "@/lib/sales/contracts";
import { saleFooterPresentation } from "@/lib/sales/sale-footer";
import { saleSections } from "@/lib/sales/sale-sections";

const actions: SaleActions = {
  claim: false,
  assign: false,
  release: true,
  saveTerms: true,
  submit: true,
  decide: false,
  cancel: true,
};
const state = {
  status: "Handling" as const,
  actions,
  section: "deal" as const,
  version: null,
  editing: false,
  dirty: false,
  notesDirty: false,
  cancelling: false,
};

const views = saleSections.flatMap((section) => [null, 3].map((version) => ({ section, version })));

test("reason drafts override term editing and retained records while preserving their destination", () => {
  for (const { section, version } of views) {
    for (const cancelling of [false, true]) {
      const context = saleFooterPresentation({
        ...state,
        section,
        version,
        editing: true,
        dirty: true,
        notesDirty: true,
        cancelling,
      });
      expect(context.kind).toBe("reason-draft");
      expect(context.title).toBe(cancelling ? "Cancel sale" : "Return to agent");
      expect(context.description.startsWith("Complete")).toBe(
        section === "deal" && version === null,
      );
    }
  }
});

test("term editing owns the footer across reference sections and historical versions", () => {
  for (const { section, version } of views) {
    for (const dirty of [false, true]) {
      const context = saleFooterPresentation({ ...state, section, version, editing: true, dirty });
      expect(context.kind).toBe("term-edit");
      expect(context.title).toBe(dirty ? "Unsaved deal terms" : "Edit deal terms");
      expect(context.description.startsWith("Save")).toBe(section === "deal" && version === null);
    }
  }
});

test("read-only history names retained sections without treating Car as a versioned view", () => {
  for (const section of ["customer", "deal"] as const) {
    const context = saleFooterPresentation({ ...state, section, version: 3 });
    expect(context.kind).toBe("past-version");
    expect(context.title).toBe(`Historical ${section} · version 3`);
  }
  expect(saleFooterPresentation({ ...state, section: "history", version: 3 }).kind).toBe(
    "record-history",
  );
  expect(saleFooterPresentation({ ...state, section: "car", version: 3 }).kind).toBe("next-step");
});

test("normal guidance preserves status, permission and dirty-term precedence", () => {
  const stepOf = (overrides: Partial<Parameters<typeof saleFooterPresentation>[0]>) => {
    const context = saleFooterPresentation({ ...state, ...overrides });
    return "step" in context ? context.step : undefined;
  };
  const statuses: SaleStatus[] = [
    "Open",
    "Handling",
    "PendingApproval",
    "Approved",
    "Settled",
    "Delivered",
    "Expired",
    "Cancelled",
  ];
  for (const status of statuses) {
    expect(stepOf({ status })).toBe(status);
  }
  expect(stepOf({ actions: { ...actions, saveTerms: false } })).toBe("review-terms");
  expect(stepOf({ actions: { ...actions, decide: true } })).toBe("decide");
  expect(stepOf({ dirty: true, actions: { ...actions, decide: true } })).toBe("dirty-terms");
  expect(saleFooterPresentation({ ...state, section: "customer", status: "Open" }).step).toBe(
    "Open",
  );
  // One copy check keeps the wording that readers see.
  expect(saleFooterPresentation({ ...state, status: "PendingApproval" })).toEqual({
    kind: "next-step",
    step: "PendingApproval",
    title: "Next step",
    description: "Waiting for manager approval.",
  });
});
