import type { RepairHistory, RevisionHistory } from "./report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import type { RepairSelection } from "./query";

export type InspectionEvent = {
  id: string;
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
      at: revision.signedAt,
      title: `Inspection revision ${revision.revision} signed`,
      actor: revision.technicianIdentity,
      detail: `${revision.assessment.isRejected ? "Rejected" : `Grade ${revision.assessment.grade}`} · Original estimate ${formatCurrency(revision.assessment.estimatedRepairCost)}`,
      revision: revision.revision,
      request: null,
    });
    const reopening = revision.reopening;
    if (reopening) {
      events.push({
        id: reopening.operationId,
        at: reopening.recordedAt,
        title: "Inspection reopened",
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
      at: request.requestedAt,
      title: "Repair budget requested",
      actor: request.actorId,
      detail: `Work version ${request.version} · ${formatCurrency(request.budget)}`,
      revision: request.revision,
      request: selection,
    });
    if (approval) {
      events.push({
        id: approval.operationId,
        at: approval.approvedAt,
        title: "Repair budget approved",
        actor: approval.actorId,
        detail: `Exact approval for work version ${request.version} · ${formatCurrency(request.budget)}`,
        revision: request.revision,
        request: selection,
      });
    }
    if (completion) {
      events.push({
        id: completion.operationId,
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
