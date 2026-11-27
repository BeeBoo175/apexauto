"use client";

import Link from "next/link";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { EmptyNotice } from "@/components/ui/EmptyNotice";
import type { CurrentActor } from "@/lib/auth/actor";
import { documentLabels, type SaleDocument } from "@/lib/sales/documents";
import { SaleDocumentLinks } from "@/components/sales/SaleDocumentLinks";
import { reportDocumentHref } from "@/lib/inspection/contracts";
import links from "@/components/sales/SaleDocumentLinks.module.css";
import styles from "./Account.module.css";
import historyStyles from "./ReservationHistory.module.css";

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
  const reservation = documents?.[0]?.contents.reservationNumber;
  return (
    <section className={styles.page}>
      <header className={styles.heroBand}>
        <div className={styles.heroInner}>
          <div>
            <h1>Your sale documents</h1>
            <p className={styles.welcome}>
              {reservation ? `Reservation ${reservation}` : "Issued purchase and delivery records"}
            </p>
          </div>
          <Link href="/account" className={styles.heroLink}>
            Back to your account
          </Link>
        </div>
      </header>
      <div className={styles.content}>
        <div className={styles.panel}>
          <div className={styles.records}>
            {documents?.length === 0 && (
              <EmptyNotice icon="file-text" title="No documents yet">
                Your sales agent will confirm the next steps.
              </EmptyNotice>
            )}
            <ul className={historyStyles.historyList}>
              {documents?.map((document) => (
                <li key={document.id} className={historyStyles.documentCard}>
                  <h2>{documentLabels[document.kind]}</h2>
                  <SaleDocumentLinks
                    saleId={saleId}
                    initial={document}
                    className={historyStyles.documentBody}
                  />
                </li>
              ))}
              {report && (
                <li className={historyStyles.documentCard}>
                  <h2>Vehicle inspection report</h2>
                  <div className={historyStyles.documentBody}>
                    <a
                      className={links.pdf}
                      href={reportDocumentHref(report.inspectionId, report.revision)}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Open signed inspection report (PDF)
                    </a>
                  </div>
                </li>
              )}
            </ul>
            {documents === null && <ReloadNotice subject="Your documents" />}
            {reportUnavailable && <ReloadNotice subject="The inspection report" />}
          </div>
        </div>
      </div>
    </section>
  );
}
