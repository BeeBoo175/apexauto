"use client";

import { Button } from "@/components/ui/Button";
import { useSaleDocumentVersion } from "@/hooks/use-sale-document-version";
import { documentLabels, saleDocumentPdf, type SaleDocument } from "@/lib/sales/documents";
import { formatMalaysianDateTime } from "@/lib/date-format";

export function SaleDocumentLinks({
  saleId,
  initial,
  className,
}: {
  saleId: string;
  initial: SaleDocument;
  className: string;
}) {
  const version = useSaleDocumentVersion(saleId, initial);
  const { document } = version;
  if (!document) {
    return (
      <Button
        size="compact"
        variant="secondary"
        disabled={version.busy}
        onClick={() => void version.open(initial.id)}
      >
        Retry document
      </Button>
    );
  }
  return (
    <div className={className} aria-busy={version.busy}>
      <a href={saleDocumentPdf(saleId, document.id)} target="_blank" rel="noopener noreferrer">
        {documentLabels[document.kind]} · {document.number} (PDF)
      </a>
      <p>
        Issued {formatMalaysianDateTime(document.issuedAt)}
        {document.replacementDocumentId ? " · Superseded" : ""}
      </p>
      {document.correction && <p>Correction: {document.correction.reason}</p>}
      {document.replacesDocumentId && (
        <Button
          size="compact"
          variant="secondary"
          disabled={version.busy}
          onClick={() => void version.open(document.replacesDocumentId!)}
        >
          View previous document
        </Button>
      )}
      {document.replacementDocumentId && (
        <Button
          size="compact"
          variant="secondary"
          disabled={version.busy}
          onClick={() => void version.open(document.replacementDocumentId!)}
        >
          View replacement document
        </Button>
      )}
    </div>
  );
}
