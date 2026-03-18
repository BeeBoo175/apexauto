import "server-only";
import type { ApiResult } from "@/lib/api";
import { inspectionRead } from "./server";
import { parseRevisions, parseRepairs } from "./parse-report";
import { parseEditor, parseWorkspace, type EditorSnapshot } from "./editor-snapshot";
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
  const result = await inspectionRead(`drafts/${id}/workspace`, parseWorkspace, signal);
  if (!result.data || result.data.draft.id !== id) {
    return { ...result, data: null };
  }
  const workspace = result.data;
  const draft = workspace.draft;
  const base = `reports/${id}`;
  const revision = `${base}/revisions/${draft.revision}`;
  const [revisions, repairs] = await Promise.all([
    includeHistory
      ? inspectionRead(`${base}/revisions?page=1&pageSize=25`, parseRevisions, signal)
      : null,
    includeHistory && draft.isSigned
      ? inspectionRead(`${revision}/repair-history?page=1&pageSize=25`, parseRepairs, signal)
      : null,
  ]);
  const data = parseEditor({
    ...workspace,
    revisions: revisions?.data?.inspectionId === id ? revisions.data : null,
    repairs:
      repairs?.data?.inspectionId === id && repairs.data.revision === draft.revision
        ? repairs.data
        : null,
    historyRequested: includeHistory,
    photos: draft.photoReferences,
  });
  return {
    status: data ? 200 : 502,
    data,
    uncertain: !data,
    message: data ? "" : "The inspection snapshot could not be confirmed. Retry the read.",
  };
}
