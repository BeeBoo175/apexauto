import type { CurrentActor } from "@/lib/auth/actor";
import type { Deal } from "./deals";

// What this person may do to this deal now; the server enforces the same rules.
export function dealAbilities(deal: Deal, actor: CurrentActor) {
  const own = actor.role === "SalesAgent" && deal.salesAgentId === actor.userId;
  const manager =
    (actor.role === "BranchManager" && actor.branchId === deal.branchId) ||
    actor.role === "GeneralManager";
  const pending = deal.status === "PendingManagerApproval";
  return {
    edit: own && (deal.status === "Draft" || deal.status === "Approved"),
    submit: own && deal.status === "Draft",
    // An agent never decides their own excess discount.
    decide: manager && pending && deal.salesAgentId !== actor.userId,
    cancel: deal.status !== "Cancelled" && (own || manager),
  };
}

export const validDealReason = (reason: string) =>
  reason === reason.trim() && reason.length >= 1 && reason.length <= 1000;

export const dealEventLabels: Record<string, string> = {
  Created: "Draft created",
  Revised: "Terms changed",
  Submitted: "Submitted",
  Approved: "Approved",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
};
