import "server-only";
import type { ApiResult } from "@/lib/intake/contracts";
import { inspectionRead } from "./server";
import { parseDraft, parseRepairPage } from "./parse";
import { parseReport, parseRevisions, parseRepairs } from "./parse-report";
import { parseEditor, type EditorSnapshot } from "./editor-snapshot";

export async function readEditor(
  id: string,
  cancellation?: AbortSignal,
): Promise<ApiResult<EditorSnapshot>> {
  const signal = AbortSignal.any([
    AbortSignal.timeout(20_000),
    ...(cancellation ? [cancellation] : []),
  ]);
  const result = await inspectionRead(`drafts/${id}`, parseDraft, signal);
  if (!result.data) {
    return { ...result, data: null };
  }
  const draft = result.data;
  const base = `reports/${id}`;
  const revision = `${base}/revisions/${draft.revision}`;
  const [revisions, report, repairs, queue] = await Promise.all([
    inspectionRead(`${base}/revisions?page=1&pageSize=25`, parseRevisions, signal),
    draft.isSigned ? inspectionRead(revision, parseReport, signal) : null,
    draft.isSigned
      ? inspectionRead(`${revision}/repair-history?page=1&pageSize=25`, parseRepairs, signal)
      : null,
    draft.isSigned && draft.repairVersion > 0
      ? inspectionRead(
          `repair-requests?status=All&search=${id}&page=1&pageSize=25`,
          parseRepairPage,
          signal,
        )
      : null,
  ]);
  const failed = [revisions, report, repairs, queue].find((read) => read && !read.data);
  if (failed) {
    return {
      ...failed,
      data: null,
      message:
        "The inspection could not be fully refreshed. Keep the last confirmed snapshot and retry.",
    };
  }
  const repairState =
    queue?.data?.items.find(
      (row) =>
        row.inspectionId === id &&
        row.revision === draft.revision &&
        row.repairVersion === draft.repairVersion,
    ) ?? null;
  const data = parseEditor({
    draft,
    revisions: revisions.data,
    report: report?.data ?? null,
    repairs: repairs?.data ?? null,
    repairState,
    photos: draft.photoReferences,
  });
  return {
    status: data ? 200 : 502,
    data,
    uncertain: !data,
    message: data ? "" : "The inspection snapshot could not be confirmed. Retry the read.",
  };
}
