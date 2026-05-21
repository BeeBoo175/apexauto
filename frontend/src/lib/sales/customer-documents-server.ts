import "server-only";

import { isPositiveInteger, isRecord, isUuid } from "@/lib/auth/contracts";
import { privateApiRead } from "@/lib/server/http/api";
import { parseSaleDocuments } from "./documents";

export function readCustomerSaleDocuments(saleId: string, customerId: string) {
  return privateApiRead(`/api/sales/${saleId}/documents`, (value) => {
    const documents = parseSaleDocuments(value);
    return documents?.every((document) => document.contents.customerId === customerId)
      ? documents
      : null;
  });
}

export function readCustomerSaleReport(saleId: string) {
  return privateApiRead(`/api/sales/${saleId}/inspection-report`, (value) =>
    isRecord(value) && isUuid(value.inspectionId) && isPositiveInteger(value.revision)
      ? { inspectionId: value.inspectionId, revision: value.revision }
      : null,
  );
}
