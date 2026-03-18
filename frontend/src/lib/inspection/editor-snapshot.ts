import { isRecord } from "@/lib/auth/contracts";
import { parseItems } from "@/lib/api";
import { parseDraft, parseRepairPage, parsePhotoReference } from "./parse";
import { parseReport, parseRevisions, parseRepairs, parseRepairRequest } from "./parse-report";
import type { Draft, RepairRow, PhotoReference } from "./contracts";
import type { Report, RevisionHistory, RepairHistory, RepairRequest } from "./report-contracts";

export type EditorSnapshot = {
  draft: Draft;
  report: Report | null;
  revisions: RevisionHistory | null;
  repairs: RepairHistory | null;
  historyRequested: boolean;
  currentRequest: RepairRequest | null;
  repairState: RepairRow | null;
  photos: PhotoReference[];
};

export function parseEditor(v: unknown): EditorSnapshot | null {
  if (!isRecord(v)) {
    return null;
  }
  const draft = parseDraft(v.draft);
  const report = v.report === null ? null : parseReport(v.report);
  const revisions = v.revisions === null ? null : parseRevisions(v.revisions);
  const repairs = v.repairs === null ? null : parseRepairs(v.repairs);
  const currentRequest = v.currentRequest === null ? null : parseRepairRequest(v.currentRequest);
  const state =
    v.repairState === null
      ? null
      : parseRepairPage({ items: [v.repairState], total: 1, page: 1, pageSize: 25 })?.items[0];
  const photos = parseItems(v.photos, parsePhotoReference);
  if (
    !draft ||
    !photos ||
    typeof v.historyRequested !== "boolean" ||
    (v.revisions !== null && !revisions) ||
    (v.report !== null && !report) ||
    (v.repairs !== null && !repairs) ||
    (v.currentRequest !== null && !currentRequest) ||
    (v.repairState !== null && !state)
  ) {
    return null;
  }
  if (
    (revisions && revisions.inspectionId !== draft.id) ||
    (report && (report.inspectionId !== draft.id || report.revision !== draft.revision)) ||
    (repairs && (repairs.inspectionId !== draft.id || repairs.revision !== draft.revision)) ||
    (state &&
      (state.inspectionId !== draft.id ||
        state.revision !== draft.revision ||
        state.repairVersion !== draft.repairVersion)) ||
    (currentRequest &&
      (currentRequest.inspectionId !== draft.id ||
        currentRequest.revision !== draft.revision ||
        currentRequest.version !== draft.repairVersion ||
        currentRequest.signedRevisionId !== report?.operationId ||
        currentRequest.operationId !== state?.requestId)) ||
    (draft.isSigned && !report) ||
    Boolean(currentRequest) !== Boolean(state) ||
    (!draft.isSigned && Boolean(currentRequest))
  ) {
    return null;
  }
  return {
    draft,
    report,
    revisions,
    repairs,
    historyRequested: v.historyRequested,
    currentRequest,
    repairState: state ?? null,
    photos,
  };
}

export function parseWorkspace(v: unknown): EditorSnapshot | null {
  if (!isRecord(v) || !isRecord(v.draft)) {
    return null;
  }
  return parseEditor({
    ...v,
    revisions: null,
    repairs: null,
    historyRequested: false,
    photos: v.draft.photoReferences,
  });
}
