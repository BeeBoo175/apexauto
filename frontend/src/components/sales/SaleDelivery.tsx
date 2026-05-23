"use client";

import type { useSaleEditor } from "@/hooks/use-sale-editor";
import type { useSaleCompletion } from "@/hooks/use-sale-completion";
import type { useSaleDocuments } from "@/hooks/use-sale-documents";
import type { useSaleDocumentCorrection } from "@/hooks/use-sale-document-correction";
import { SaleCompletionEvidence } from "./SaleCompletionEvidence";
import { SaleCompletionForm } from "./SaleCompletionForm";
import { SaleDocumentCorrection } from "./SaleDocumentCorrection";
import { SaleDocuments } from "./SaleDocuments";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Sales.module.css";

export function SaleDelivery({
  editor,
  completion,
  documents,
  correction,
  actionsContainer,
}: {
  editor: ReturnType<typeof useSaleEditor>;
  completion: ReturnType<typeof useSaleCompletion>;
  documents: ReturnType<typeof useSaleDocuments>;
  correction: ReturnType<typeof useSaleDocumentCorrection>;
  actionsContainer: HTMLElement | null;
}) {
  const { snapshot } = editor;
  let title = "Settlement and handover";
  if (completion.stage === "settlement") {
    title = "Review settlement";
  }
  if (completion.stage === "handover") {
    title = "Prepare handover";
  }
  if (correction.draft) {
    title = "Correct buyer details";
  }
  return (
    <section className={styles.section} aria-label="Sale delivery">
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>{title}</h2>
      </div>
      <SaleCompletionEvidence sale={snapshot.sale} />
      {!completion.stage && !snapshot.sale.paymentEvidence && (
        <p className={workbench.help}>
          {completion.eligible
            ? "Verify payment against the approved balance to settle this sale."
            : "Awaiting payment verification by the handling agent."}
        </p>
      )}
      {completion.stage && (
        <SaleCompletionForm
          workflow={completion}
          balance={snapshot.sale.balancePayable}
          actionsContainer={actionsContainer}
          active
        />
      )}
      {correction.draft ? (
        <SaleDocumentCorrection
          correction={correction}
          actionsContainer={actionsContainer}
          blocked={editor.operating || editor.refreshing}
        />
      ) : (
        snapshot.sale.paymentEvidence && (
          <SaleDocuments
            saleId={snapshot.id}
            read={documents}
            onCorrect={correction.allowed ? correction.start : undefined}
            disabled={editor.operating || editor.refreshing || completion.dirty}
          />
        )
      )}
    </section>
  );
}
