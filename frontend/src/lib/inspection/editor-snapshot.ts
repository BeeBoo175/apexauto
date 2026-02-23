import { isRecord } from "@/lib/auth/contracts";
import { parseItems } from "@/lib/api";
import { parseDraft, parseRepairPage, parsePhotoReference } from "./parse";
import { parseReport, parseRevisions, parseRepairs } from "./parse-report";
import type { Draft, RepairRow, PhotoReference } from "./contracts";
import type { Report, RevisionHistory, RepairHistory } from "./report-contracts";

export type EditorSnapshot = {
  draft: Draft;
  report: Report | null;
  revisions: RevisionHistory;
  repairs: RepairHistory | null;
  repairState: RepairRow | null;
  photos: PhotoReference[];
};

export function parseEditor(v: unknown): EditorSnapshot | null {
  if (!isRecord(v)) {
    return null;
  }
  const draft = parseDraft(v.draft);
  const report = v.report === null ? null : parseReport(v.report);
  const revisions = parseRevisions(v.revisions);
  const repairs = v.repairs === null ? null : parseRepairs(v.repairs);
  const state =
    v.repairState === null
      ? null
      : parseRepairPage({ items: [v.repairState], total: 1, page: 1, pageSize: 25 })?.items[0];
  const photos = parseItems(v.photos, parsePhotoReference);
  if (
    !draft ||
    !revisions ||
    !photos ||
    (v.report !== null && !report) ||
    (v.repairs !== null && !repairs) ||
    (v.repairState !== null && !state)
  ) {
    return null;
  }
  if (
    revisions.inspectionId !== draft.id ||
    (report && (report.inspectionId !== draft.id || report.revision !== draft.revision)) ||
    (repairs && (repairs.inspectionId !== draft.id || repairs.revision !== draft.revision)) ||
    (state && (state.inspectionId !== draft.id || state.repairVersion !== draft.repairVersion))
  ) {
    return null;
  }
  return { draft, report, revisions, repairs, repairState: state ?? null, photos };
}
