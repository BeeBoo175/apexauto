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

function nextStepNote(status: SaleStatus, actions: SaleActions, dirty: boolean) {
  if (dirty) {
    return "Save your term changes before submitting.";
  }
  if (actions.decide) {
    return "Approve the terms or return them.";
  }
  if (status === "Handling" && !actions.saveTerms) {
    return "Review terms or manage assignment.";
  }
  const notes: Record<SaleStatus, string> = {
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
  return notes[status];
}

// Draft reasons take precedence over terms, which remain owned by the live Deal section.
export function saleFooterPresentation(state: SaleFooterState) {
  const { section, version, editing, dirty, notesDirty, cancelling, status, actions } = state;
  const viewingPast = version !== null && isSaleVersionSection(section);
  const editingHere = section === "deal" && !viewingPast;

  if (notesDirty) {
    return {
      title: cancelling ? "Cancel sale" : "Return to agent",
      description: editingHere
        ? "Complete this, or discard changes."
        : "Reason kept. Return to Deal to finish.",
    };
  }
  if (editing) {
    return {
      title: dirty ? "Unsaved deal terms" : "Edit deal terms",
      description: editingHere
        ? "Save the terms, or discard changes."
        : "Terms kept. Return to Deal to save.",
    };
  }
  if (viewingPast) {
    return {
      title: `Historical ${saleSectionLabels[section].toLowerCase()} · version ${version}`,
      description: "Past versions are read only.",
    };
  }
  if (section === "history") {
    return { title: "Record history", description: "Past versions are read only." };
  }
  if (section === "delivery") {
    return {
      title: status === "Delivered" ? "Delivery complete" : "Next step",
      description: nextStepNote(status, actions, dirty),
    };
  }
  if (section !== "deal" && status !== "Open") {
    return { title: "Next step", description: "Review the current deal and agreed terms." };
  }
  return { title: "Next step", description: nextStepNote(status, actions, dirty) };
}
