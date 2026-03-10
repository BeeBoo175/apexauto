import type { RepairHistory, RevisionHistory } from "./report-contracts";

export type InspectionHistoryPage = RepairHistory | RevisionHistory;

export function appendInspectionHistory<T extends InspectionHistoryPage>(previous: T, next: T): T {
  const items = new Map(
    [...previous.items, ...next.items].map((item) => [
      "request" in item ? item.request.operationId : item.operationId,
      item,
    ]),
  );
  return { ...next, items: [...items.values()] };
}
