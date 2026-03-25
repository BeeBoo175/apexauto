import type { RepairHistory, RevisionHistory } from "./report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import type { RepairSelection } from "./query";

export type InspectionEvent = {
  id: string;
  kind: "inspection" | "reopening" | "request" | "approval" | "completion";
  at: string;
  title: string;
  actor: string;
  detail: string;
  revision: number;
  request: RepairSelection | null;
};

export function inspectionTimeline(
  revisions: RevisionHistory | null,
  repairs: RepairHistory | null,
) {
  const events: InspectionEvent[] = [];
  for (const revision of revisions?.items ?? []) {
    events.push({
      id: revision.operationId,
      kind: "inspection",
      at: revision.signedAt,
      title: "Inspection signed off",
      actor: revision.technicianIdentity,
      detail: `${revision.assessment.isRejected ? "Rejected" : `Grade ${revision.assessment.grade}`} · Repair estimate ${formatCurrency(revision.assessment.estimatedRepairCost)}`,
      revision: revision.revision,
      request: null,
    });
    const reopening = revision.reopening;
    if (reopening) {
      events.push({
        id: reopening.operationId,
        kind: "reopening",
        at: reopening.recordedAt,
        title: "Reinspection requested",
        actor: reopening.actorId,
        detail: reopening.reason,
        revision: revision.revision,
        request: null,
      });
    }
  }
  for (const { request, approval, completion } of repairs?.items ?? []) {
    const selection = { requestId: request.operationId, revision: request.revision };
    events.push({
      id: request.operationId,
      kind: "request",
      at: request.requestedAt,
      title: "Repair request submitted",
      actor: request.actorId,
      detail: `Requested budget ${formatCurrency(request.budget)}`,
      revision: request.revision,
      request: selection,
    });
    if (approval) {
      events.push({
        id: approval.operationId,
        kind: "approval",
        at: approval.approvedAt,
        title: "Repair budget approved",
        actor: approval.actorId,
        detail: `Approved budget ${formatCurrency(request.budget)}`,
        revision: request.revision,
        request: selection,
      });
    }
    if (completion) {
      events.push({
        id: completion.operationId,
        kind: "completion",
        at: completion.completedAt,
        title: "Repair work completed",
        actor: completion.actorId,
        detail: completion.notes,
        revision: request.revision,
        request: selection,
      });
    }
  }
  return events.sort(
    (left, right) => Date.parse(right.at) - Date.parse(left.at) || right.id.localeCompare(left.id),
  );
}
