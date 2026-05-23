"use client";

import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import type { useSaleDocuments } from "@/hooks/use-sale-documents";
import type { SaleDocument } from "@/lib/sales/documents";
import { SaleDocumentLinks } from "./SaleDocumentLinks";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./SaleDocuments.module.css";

export function SaleDocuments({
  saleId,
  read,
  onCorrect,
  disabled = false,
}: {
  saleId: string;
  read: ReturnType<typeof useSaleDocuments>;
  onCorrect?: (document: SaleDocument) => void;
  disabled?: boolean;
}) {
  return (
    <section className={styles.documents} aria-label="Issued documents" aria-busy={read.busy}>
      <div className={workbench.sectionHeader}>
        <h3 className={workbench.sectionTitle}>Issued documents</h3>
      </div>
      <LoadingProgress label="Loading sales documents" active={read.busy} />
      {read.documents?.map((document) => (
        <div className={styles.document} key={document.id}>
          <SaleDocumentLinks saleId={saleId} initial={document} className={styles.details} />
          {onCorrect && (
            <Button
              size="compact"
              variant="secondary"
              disabled={disabled || read.busy || read.failed}
              onClick={() => onCorrect(document)}
            >
              Correct buyer details
            </Button>
          )}
        </div>
      ))}
      {read.failed && (
        <Button size="compact" variant="secondary" disabled={read.busy} onClick={read.retry}>
          Retry documents
        </Button>
      )}
    </section>
  );
}
