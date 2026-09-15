"use client";

import { Button } from "@/components/ui/Button";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { useSaleDocumentVersion } from "@/hooks/use-sale-document-version";
import {
  buyerDetailsRemoved,
  documentLabels,
  saleDocumentPdf,
  type SaleDocument,
} from "@/lib/sales/documents";
import { formatMalaysianDateTime } from "@/lib/date-format";
import links from "./SaleDocumentLinks.module.css";

export function SaleDocumentLinks({
  saleId,
  initial,
  className,
  compact = false,
  onCorrect,
  disabled = false,
}: {
  saleId: string;
  initial: SaleDocument;
  className: string;
  compact?: boolean;
  onCorrect?: (document: SaleDocument) => void;
  disabled?: boolean;
}) {
  const version = useSaleDocumentVersion(saleId, initial);
  const { document } = version;
  if (!document) {
    return <ReloadNotice subject="Document" />;
  }
  const removed = buyerDetailsRemoved(document.contents.review);
  const details = (
    <>
      <p>
        Issued {formatMalaysianDateTime(document.issuedAt)}
        {document.replacementDocumentId ? " · Superseded" : ""}
      </p>
      {document.correction && (
        <p>Correction{document.correction.reason ? `: ${document.correction.reason}` : "."}</p>
      )}
      {removed && <p>Buyer details were removed after the retention period.</p>}
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
      {onCorrect && !removed && !document.replacementDocumentId && (
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled || version.busy}
          onClick={() => onCorrect(document)}
        >
          Correct buyer details
        </Button>
      )}
    </>
  );
  return (
    <div className={className} aria-busy={version.busy}>
      <a
        className={links.pdf}
        href={saleDocumentPdf(saleId, document.id)}
        target="_blank"
        rel="noopener noreferrer"
      >
        {documentLabels[document.kind]} · {document.number} (PDF)
      </a>
      {compact ? (
        <details className={links.disclosure}>
          <summary>
            Document details
            {document.correction && <span>Corrected</span>}
            {document.replacementDocumentId && <span>Superseded</span>}
          </summary>
          <div className={links.disclosureContent}>{details}</div>
        </details>
      ) : (
        details
      )}
    </div>
  );
}
