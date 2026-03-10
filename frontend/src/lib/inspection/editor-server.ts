import "server-only";
import type { ApiResult } from "@/lib/api";
import { inspectionRead } from "./server";
import { parseDraft, parseRepairPage } from "./parse";
import { parseReport, parseRevisions, parseRepairs, parseRepairRequest } from "./parse-report";
import { parseEditor, type EditorSnapshot } from "./editor-snapshot";
import { readRepairReview, type RepairReview } from "./repair-review";
import type { RepairSelection } from "./query";

export async function readSelectedRepair(
  id: string,
  selection: RepairSelection,
): Promise<ApiResult<RepairReview>> {
  try {
    const data = await readRepairReview(
      id,
      selection,
      (path, parse, options) => inspectionRead(path, parse, options?.signal ?? undefined),
      AbortSignal.timeout(20_000),
    );
    return { status: 200, data, message: "", uncertain: false };
  } catch {
    return {
      status: 502,
      data: null,
      message: "The selected repair request could not be loaded. Use refresh to retry.",
      uncertain: false,
    };
  }
}

export async function readEditor(
  id: string,
  cancellation?: AbortSignal,
  includeHistory = false,
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
  const requestRead =
    draft.isSigned && draft.repairVersion > 0
      ? inspectionRead(`${revision}/repairs`, parseRepairRequest, signal)
      : null;
  const queueRead = requestRead?.then((request) => {
    if (!request.data) {
      return null;
    }
    return inspectionRead(
      `repair-requests?status=All&search=${request.data.operationId}&page=1&pageSize=25`,
      parseRepairPage,
      signal,
    );
  });
  const [revisions, report, repairs, request, queue] = await Promise.all([
    includeHistory
      ? inspectionRead(`${base}/revisions?page=1&pageSize=25`, parseRevisions, signal)
      : null,
    draft.isSigned ? inspectionRead(revision, parseReport, signal) : null,
    includeHistory && draft.isSigned
      ? inspectionRead(`${revision}/repair-history?page=1&pageSize=25`, parseRepairs, signal)
      : null,
    requestRead,
    queueRead,
  ]);
  const failed = [report, queue, request].find((read) => read && !read.data);
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
    revisions: revisions?.data?.inspectionId === id ? revisions.data : null,
    report: report?.data ?? null,
    repairs:
      repairs?.data?.inspectionId === id && repairs.data.revision === draft.revision
        ? repairs.data
        : null,
    historyRequested: includeHistory,
    currentRequest: request?.data ?? null,
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
