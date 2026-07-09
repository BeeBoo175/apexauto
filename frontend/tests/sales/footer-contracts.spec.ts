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
      expect(context).toEqual({
        title: cancelling ? "Cancel sale" : "Return to agent",
        description:
          section === "deal" && version === null
            ? "Complete this, or discard changes."
            : "Reason kept. Return to Deal to finish.",
      });
    }
  }
});

test("term editing owns the footer across reference sections and historical versions", () => {
  for (const { section, version } of views) {
    for (const dirty of [false, true]) {
      expect(saleFooterPresentation({ ...state, section, version, editing: true, dirty })).toEqual({
        title: dirty ? "Unsaved deal terms" : "Edit deal terms",
        description:
          section === "deal" && version === null
            ? "Save the terms, or discard changes."
            : "Terms kept. Return to Deal to save.",
      });
    }
  }
});

test("read-only history names retained sections without treating Car as a versioned view", () => {
  for (const section of ["customer", "deal"] as const) {
    expect(saleFooterPresentation({ ...state, section, version: 3 })).toEqual({
      title: `Historical ${section} · version 3`,
      description: "Past versions are read only.",
    });
  }
  expect(saleFooterPresentation({ ...state, section: "history", version: 3 })).toEqual({
    title: "Record history",
    description: "Past versions are read only.",
  });
  expect(saleFooterPresentation({ ...state, section: "car", version: 3 })).toEqual({
    title: "Next step",
    description: "Review the current deal and agreed terms.",
  });
});

test("normal guidance preserves status, permission and dirty-term precedence", () => {
  const notes: Record<SaleStatus, string> = {
    Open: "Assign an agent to start agreeing terms.",
    Handling: "Enter the agreed terms, then submit.",
    PendingApproval: "Waiting for manager approval.",
    Approved: "Settlement and handover follow.",
    Settled: "Settled. Hand over when the vehicle is ready.",
    Delivered: "Delivered, records are read only.",
    Expired: "Hold expired, deposit refunded.",
    Cancelled: "Cancelled, deposit refunded.",
  };
  for (const [status, description] of Object.entries(notes)) {
    expect(saleFooterPresentation({ ...state, status: status as SaleStatus })).toEqual({
      title: "Next step",
      description,
    });
  }
  expect(
    saleFooterPresentation({ ...state, status: "Open", actions: { ...actions, claim: true } })
      .description,
  ).toBe("Claim this sale to start agreeing terms.");
  expect(
    saleFooterPresentation({ ...state, actions: { ...actions, saveTerms: false } }).description,
  ).toBe("Review terms or manage assignment.");
  expect(
    saleFooterPresentation({ ...state, actions: { ...actions, decide: true } }).description,
  ).toBe("Approve the terms or return them.");
  expect(
    saleFooterPresentation({ ...state, dirty: true, actions: { ...actions, decide: true } })
      .description,
  ).toBe("Save your term changes before submitting.");
  expect(
    saleFooterPresentation({ ...state, section: "customer", status: "Open" }).description,
  ).toBe(notes.Open);
});
