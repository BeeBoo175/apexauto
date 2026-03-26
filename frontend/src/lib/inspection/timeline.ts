import type { RepairHistory, RevisionHistory } from "./report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import type { RepairSelection } from "./query";

export type InspectionEvent = {
  id: string;
  kind: "inspection" | "reopening" | "request" | "approval" | "completion";
  at: string;
  title: string;
  context: string;
  detail: string;
  revision: number;
  request: RepairSelection | null;
};

export function inspectionTimeline(
  revisions: RevisionHistory | null,
  repairs: RepairHistory | null,
  previous: RepairHistory | null = null,
) {
  const events: InspectionEvent[] = [];
  for (const revision of revisions?.items ?? []) {
    const outcome = revision.assessment.isRejected
      ? "Rejected"
      : `Grade ${revision.assessment.grade}`;
    events.push({
      id: revision.operationId,
      kind: "inspection",
      at: revision.signedAt,
      title: "Inspection signed off",
      context: `Inspection ${revision.revision} · ${outcome}`,
      detail: "",
      revision: revision.revision,
      request: null,
    });
    if (revision.reopening) {
      events.push({
        id: revision.reopening.operationId,
        kind: "reopening",
        at: revision.reopening.recordedAt,
        title: "Reinspection requested",
        context: `Inspection ${revision.reopening.revision}`,
        detail: revision.reopening.reason,
        revision: revision.revision,
        request: null,
      });
    }
  }
  const entries = new Map(
    [...(repairs?.items ?? []), ...(previous?.items ?? [])].map((entry) => [
      entry.request.operationId,
      entry,
    ]),
  );
  for (const { request, approval, completion } of entries.values()) {
    const context = `Request ${request.version} · Inspection ${request.revision}`;
    events.push({
      id: request.operationId,
      kind: "request",
      at: request.requestedAt,
      title: "Repair request submitted",
      context: `${context} · ${formatCurrency(request.budget)}`,
      detail: request.items.map((item) => item.work).join(" · "),
      revision: request.revision,
      request: { requestId: request.operationId, revision: request.revision },
    });
    if (approval) {
      events.push({
        id: approval.operationId,
        kind: "approval",
        at: approval.approvedAt,
        title: "Repair budget approved",
        context,
        detail: "",
        revision: request.revision,
        request: null,
      });
    }
    if (completion) {
      events.push({
        id: completion.operationId,
        kind: "completion",
        at: completion.completedAt,
        title: "Repair work completed",
        context,
        detail: completion.notes,
        revision: request.revision,
        request: null,
      });
    }
  }
  return events.sort(
    (left, right) => Date.parse(right.at) - Date.parse(left.at) || right.id.localeCompare(left.id),
  );
}
