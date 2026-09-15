"use client";

import { useContext, useState } from "react";
import { StaffActorContext } from "@/components/staff/StaffActorBoundary";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord } from "@/lib/auth/contracts";
import { boundedText } from "@/lib/sales/completion-evidence";
import { isSalesManager, type Sale } from "@/lib/sales/contracts";
import { parseDocumentCorrection, type SaleDocument } from "@/lib/sales/documents";
import { salePath } from "@/lib/sales/query";
import { useApiCommand } from "./use-api-command";
import { useEditGuard } from "./use-edit-guard";

export function useSaleDocumentCorrection(sale: Sale, onChanged: () => void) {
  const actor = useContext(StaffActorContext);
  const [draft, setDraft] = useState<{
    document: SaleDocument;
    version: number;
    buyerName: string;
    buyerAddress: string;
    reason: string;
  } | null>(null);
  const command = useApiCommand(
    `${salePath(sale.id)}/document-corrections`,
    parseDocumentCorrection,
    (receipt, request) =>
      isRecord(request) &&
      receipt.saleId === sale.id &&
      receipt.operationId === request.operationId &&
      receipt.originalDocumentId === request.documentId,
    privateApiFetch,
  );
  const operating = command.busy || command.uncertain;
  const allowed = Boolean(actor && isSalesManager(actor));
  const stale = Boolean(draft && draft.version !== sale.sale.version);
  const valid = Boolean(
    draft &&
    boundedText(draft.buyerName, 200) &&
    boundedText(draft.buyerAddress, 1000) &&
    boundedText(draft.reason, 1000) &&
    (draft.buyerName.trim() !== draft.document.contents.review.buyerName ||
      draft.buyerAddress.trim() !== draft.document.contents.review.buyerAddress),
  );
  useEditGuard({
    dirty: draft !== null,
    pending: command.busy,
    uncertain: command.uncertain,
    label: "Document correction",
    discard: () => setDraft(null),
  });

  async function submit() {
    if (!draft || command.busy || (!command.uncertain && (!valid || stale || !allowed))) {
      return;
    }
    const response = await command.submit({
      operationId: crypto.randomUUID(),
      expectedVersion: draft.version,
      documentId: draft.document.id,
      buyerName: draft.buyerName.trim(),
      buyerAddress: draft.buyerAddress.trim(),
      reason: draft.reason.trim(),
    });
    if (response?.status === 200 && response.data) {
      setDraft(null);
      onChanged();
    }
  }

  return {
    draft,
    allowed,
    stale,
    valid,
    busy: command.busy,
    uncertain: command.uncertain,
    operating,
    submit,
    start: (document: SaleDocument) => {
      const { buyerName, buyerAddress } = document.contents.review;
      // Retention removed this document's buyer details, so there is nothing left to correct.
      if (buyerName === null || buyerAddress === null) {
        return;
      }
      if (allowed && !operating && !document.replacementDocumentId) {
        setDraft({
          document,
          version: sale.sale.version,
          buyerName,
          buyerAddress,
          reason: "",
        });
      }
    },
    change: (
      patch: Partial<Pick<NonNullable<typeof draft>, "buyerName" | "buyerAddress" | "reason">>,
    ) => setDraft((current) => (current ? { ...current, ...patch } : null)),
    discard: () => {
      if (!operating) {
        setDraft(null);
      }
    },
  };
}
