import type { SaleActions, SaleStatus } from "./contracts";
import { isSaleVersionSection, saleSectionLabels, type SaleSection } from "./sale-sections";

interface SaleFooterState {
  status: SaleStatus;
  actions: SaleActions;
  section: SaleSection;
  version: number | null;
  editing: boolean;
  dirty: boolean;
  notesDirty: boolean;
  cancelling: boolean;
}

export type SaleFooterKind =
  "reason-draft" | "term-edit" | "past-version" | "record-history" | "delivery" | "next-step";

// Which next-step guidance applies; the order of these rules is the precedence the footer promises.
export type SaleNextStep = "dirty-terms" | "decide" | "review-terms" | SaleStatus;

function nextStep(status: SaleStatus, actions: SaleActions, dirty: boolean): SaleNextStep {
  if (dirty) {
    return "dirty-terms";
  }
  if (actions.decide) {
    return "decide";
  }
  if (status === "Handling" && !actions.saveTerms) {
    return "review-terms";
  }
  return status;
}

function nextStepNote(status: SaleStatus, actions: SaleActions, dirty: boolean) {
  const step = nextStep(status, actions, dirty);
  const notes: Record<SaleNextStep, string> = {
    "dirty-terms": "Save your term changes before submitting.",
    decide: "Approve the terms or return them.",
    "review-terms": "Review terms or manage assignment.",
    Open: actions.claim
      ? "Claim this sale to start agreeing terms."
      : "Assign an agent to start agreeing terms.",
    Handling: "Enter the agreed terms, then submit.",
    PendingApproval: "Waiting for manager approval.",
    Approved: "Settlement and handover follow.",
    Settled: "Settled. Hand over when the vehicle is ready.",
    Delivered: "Delivered, records are read only.",
    Expired: "Hold expired, deposit refunded.",
    Cancelled: "Cancelled, deposit refunded.",
  };
  return { step, description: notes[step] };
}

// Draft reasons take precedence over terms, which remain owned by the live Deal section.
export function saleFooterPresentation(state: SaleFooterState) {
  const { section, version, editing, dirty, notesDirty, cancelling, status, actions } = state;
  const viewingPast = version !== null && isSaleVersionSection(section);
  const editingHere = section === "deal" && !viewingPast;

  if (notesDirty) {
    return {
      kind: "reason-draft" as SaleFooterKind,
      title: cancelling ? "Cancel sale" : "Return to agent",
      description: editingHere
        ? "Complete this, or discard changes."
        : "Reason kept. Return to Deal to finish.",
    };
  }
  if (editing) {
    return {
      kind: "term-edit" as SaleFooterKind,
      title: dirty ? "Unsaved deal terms" : "Edit deal terms",
      description: editingHere
        ? "Save the terms, or discard changes."
        : "Terms kept. Return to Deal to save.",
    };
  }
  if (viewingPast) {
    return {
      kind: "past-version" as SaleFooterKind,
      title: `Historical ${saleSectionLabels[section].toLowerCase()} · version ${version}`,
      description: "Past versions are read only.",
    };
  }
  if (section === "history") {
    return {
      kind: "record-history" as SaleFooterKind,
      title: "Record history",
      description: "Past versions are read only.",
    };
  }
  if (section === "delivery") {
    const note = nextStepNote(status, actions, dirty);
    return {
      kind: "delivery" as SaleFooterKind,
      step: note.step,
      title: status === "Delivered" ? "Delivery complete" : "Next step",
      description: note.description,
    };
  }
  if (section !== "deal" && status !== "Open") {
    return {
      kind: "next-step" as SaleFooterKind,
      step: undefined,
      title: "Next step",
      description: "Review the current deal and agreed terms.",
    };
  }
  const note = nextStepNote(status, actions, dirty);
  return {
    kind: "next-step" as SaleFooterKind,
    step: note.step as SaleNextStep | undefined,
    title: "Next step",
    description: note.description,
  };
}
