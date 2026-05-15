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
    return "Approve the submitted terms, or return them to the agent with a reason.";
  }
  if (status === "Handling" && !actions.saveTerms) {
    return "Review the agreed terms or manage this sale’s assignment.";
  }
  const notes: Record<SaleStatus, string> = {
    Open: actions.claim
      ? "Claim this sale to start agreeing terms."
      : "Assign an agent to start agreeing terms.",
    Handling: "Key the terms agreed with the customer, then submit.",
    PendingApproval: "Waiting for a manager to approve the discount.",
    Approved: "Settlement and handover follow.",
    Expired: "This hold expired and its deposit was refunded.",
    Cancelled: "This sale is cancelled and its deposit refunded.",
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
        ? "Complete this action, or discard changes to continue reviewing the sale."
        : "Your reason is kept. Return to Deal to complete the action, or discard changes.",
    };
  }
  if (editing) {
    return {
      title: dirty ? "Unsaved deal terms" : "Edit deal terms",
      description: editingHere
        ? "Save the agreed terms, or discard changes to restore the confirmed deal."
        : "Your terms are kept. Return to Deal to save or discard changes.",
    };
  }
  if (viewingPast) {
    return {
      title: `Historical ${saleSectionLabels[section].toLowerCase()} · version ${version}`,
      description: "Retained sale versions are read only.",
    };
  }
  if (section === "history") {
    return { title: "Record history", description: "Retained sale versions are read only." };
  }
  if (section !== "deal" && status !== "Open") {
    return { title: "Next step", description: "Review the current deal and agreed terms." };
  }
  return { title: "Next step", description: nextStepNote(status, actions, dirty) };
}
