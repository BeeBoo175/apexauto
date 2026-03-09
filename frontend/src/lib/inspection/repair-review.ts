import type { ApiRequest, ApiResult } from "@/lib/api";
import { parseReport, parseRepairs } from "./parse-report";
import type { RepairHistoryEntry, Report } from "./report-contracts";
import type { RepairSelection } from "./query";

export type RepairReview = { report: Report; entry: RepairHistoryEntry };
export type SelectedRepairReview = { selection: RepairSelection; result: ApiResult<RepairReview> };

export async function readRepairReview(
  inspectionId: string,
  selection: RepairSelection,
  read: ApiRequest,
  signal: AbortSignal,
): Promise<RepairReview> {
  const { requestId, revision } = selection;
  const base = `reports/${inspectionId}/revisions/${revision}`;
  const reportRead = await read(base, parseReport, { signal });
  const report = reportRead.data;
  if (
    reportRead.status !== 200 ||
    report?.inspectionId !== inspectionId ||
    report.revision !== revision
  ) {
    throw new Error("Selected report unavailable");
  }
  let page = 1;
  while (true) {
    const history = await read(`${base}/repair-history?page=${page}&pageSize=50`, parseRepairs, {
      signal,
    });
    if (
      history.status !== 200 ||
      !history.data ||
      history.data.inspectionId !== inspectionId ||
      history.data.revision !== revision ||
      history.data.page !== page
    ) {
      throw new Error("Selected repair history unavailable");
    }
    const entry = history.data.items.find((item) => item.request.operationId === requestId);
    if (entry) {
      if (
        entry.request.inspectionId !== inspectionId ||
        entry.request.revision !== revision ||
        entry.request.signedRevisionId !== report.operationId ||
        (entry.approval && entry.approval.requestId !== requestId) ||
        (entry.completion && entry.completion.requestId !== requestId)
      ) {
        throw new Error("Selected request identity mismatch");
      }
      signal.throwIfAborted();
      return { report, entry };
    }
    if (page * history.data.pageSize >= history.data.totalCount || !history.data.items.length) {
      throw new Error("Selected request unavailable");
    }
    page += 1;
  }
}
