"use client";

import Link from "next/link";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import type { CurrentActor } from "@/lib/auth/actor";
import { documentLabels, type SaleDocument } from "@/lib/sales/documents";
import { SaleDocumentLinks } from "@/components/sales/SaleDocumentLinks";
import { reportDocumentHref } from "@/lib/inspection/contracts";
import styles from "./Account.module.css";

export function AccountSaleDocuments({
  actor,
  saleId,
  documents,
  report,
  reportUnavailable,
}: {
  actor: CurrentActor;
  saleId: string;
  documents: SaleDocument[] | null;
  report: { inspectionId: string; revision: number } | null;
  reportUnavailable: boolean;
}) {
  const session = useIntakeActor(actor);
  if (!session.active) {
    return null;
  }
  return (
    <section className={styles.page}>
      <header className={styles.pageHeader}>
        <div>
          <h1>Your sale documents</h1>
          <p className={styles.welcome}>
            {documents?.[0]?.contents.reservationNumber ?? "Issued purchase and delivery records"}
          </p>
        </div>
        <Link href="/account">Back to your account</Link>
      </header>
      <div className={styles.records}>
        {documents?.length === 0 && (
          <p>No documents have been issued yet. Your sales agent will confirm the next steps.</p>
        )}
        {documents?.map((document) => (
          <article key={document.id} className={styles.appointment}>
            <h2>{documentLabels[document.kind]}</h2>
            <SaleDocumentLinks saleId={saleId} initial={document} className={styles.records} />
          </article>
        ))}
        {report && (
          <section className={styles.appointment}>
            <h2>Vehicle inspection report</h2>
            <a
              href={reportDocumentHref(report.inspectionId, report.revision)}
              target="_blank"
              rel="noopener noreferrer"
            >
              Open signed inspection report (PDF)
            </a>
          </section>
        )}
        {documents === null && <ReloadNotice subject="Your documents" />}
        {reportUnavailable && <ReloadNotice subject="The inspection report" />}
      </div>
    </section>
  );
}
