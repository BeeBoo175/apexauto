"use client";

import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import type { useSaleDocuments } from "@/hooks/use-sale-documents";
import type { SaleDocument } from "@/lib/sales/documents";
import { SaleDocumentLinks } from "./SaleDocumentLinks";
import { SaleDocumentsLoadingShell } from "./SaleDocumentsLoadingShell";
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
      {read.busy && !read.documents && <SaleDocumentsLoadingShell />}
      {read.documents?.map((document) => (
        <article className={styles.document} key={document.id}>
          <SaleDocumentLinks
            saleId={saleId}
            initial={document}
            className={styles.details}
            compact
            onCorrect={onCorrect}
            disabled={disabled || read.busy || read.failed}
          />
        </article>
      ))}
      {read.failed && <ReloadNotice subject="Documents" />}
    </section>
  );
}
