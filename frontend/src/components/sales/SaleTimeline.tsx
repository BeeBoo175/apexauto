"use client";

import type { ComponentProps } from "react";
import { createPortal } from "react-dom";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { StaffLink } from "@/components/staff/StaffLink";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { useStaffHistory } from "@/hooks/use-staff-history";
import { privateApiFetch } from "@/lib/api-browser";
import { formatCurrency } from "@/lib/currency-format";
import type { SaleSection } from "@/lib/sales/sale-sections";
import { documentLabels, saleDocumentPdf } from "@/lib/sales/documents";
import {
  appendSaleHistory,
  historyChange,
  historyLink,
  historySummary,
  historyTitle,
  parseSaleHistory,
  saleHistoryPath,
} from "@/lib/sales/sale-history";
import timeline from "@/components/staff/StaffHistory.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import links from "./SaleDocumentLinks.module.css";

// Every action remains in history; financial milestones open their retained Deal snapshot.
export function SaleTimeline({
  saleId,
  disabled,
  onOpenVersion,
  active,
  actionsContainer,
  refreshControl,
}: {
  saleId: string;
  disabled: boolean;
  onOpenVersion: (version: number, section: SaleSection) => void;
  active: boolean;
  actionsContainer?: HTMLElement | null;
  refreshControl?: ComponentProps<typeof RefreshButton>;
}) {
  const read = useStaffHistory(
    null,
    {
      path: saleHistoryPath(saleId),
      parse: parseSaleHistory,
      matches: () => true,
      append: appendSaleHistory,
      read: privateApiFetch,
    },
    "The sale history could not be loaded. Confirmed entries remain shown.",
    { progressive: true },
  );
  const { history, busy } = read;
  return (
    <section className={timeline.history} aria-label="Sale history" aria-busy={busy}>
      {active &&
        actionsContainer &&
        createPortal(
          <RefreshButton
            label="Refresh sale history"
            busy={busy || Boolean(refreshControl?.busy)}
            disabled={disabled || Boolean(refreshControl?.disabled)}
            onClick={() => {
              void read.refresh();
              refreshControl?.onClick();
            }}
          />,
          actionsContainer,
        )}
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Record history</h2>
      </div>
      <StaffTimeline>
        {history?.items.map((item) => {
          const link = historyLink(item);
          const summary = historySummary(item);
          return (
            <StaffTimelineEvent
              key={item.id}
              title={historyTitle(item)}
              at={item.recordedAt}
              context={item.actor}
            >
              {summary && <p>{summary}</p>}
              {item.reason && <p>{item.reason}</p>}
              {item.changes.map((change) => (
                <p key={change.field}>{historyChange(change)}</p>
              ))}
              {item.refund !== null && <p>Deposit refunded: {formatCurrency(item.refund)}</p>}
              {item.documents.length > 0 && (
                <div className={timeline.eventActions}>
                  {item.documents.map((document) => (
                    <a
                      key={document.id}
                      className={links.pdf}
                      href={saleDocumentPdf(saleId, document.id)}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      {documentLabels[document.kind]} · {document.number} (PDF)
                    </a>
                  ))}
                </div>
              )}
              {link && (
                <div className={timeline.eventActions}>
                  <StaffLink
                    prefetch={false}
                    href={`?sale=${saleId}&section=deal&version=${item.version}`}
                    aria-disabled={disabled}
                    onClick={(event) => {
                      if (
                        event.button !== 0 ||
                        event.ctrlKey ||
                        event.metaKey ||
                        event.shiftKey ||
                        event.altKey
                      ) {
                        return;
                      }
                      // Local selection keeps the loaded history and any unsaved terms mounted.
                      event.preventDefault();
                      if (!disabled) {
                        onOpenVersion(item.version, "deal");
                      }
                    }}
                  >
                    {link}
                  </StaffLink>
                </div>
              )}
            </StaffTimelineEvent>
          );
        })}
      </StaffTimeline>
      <StaffHistoryProgress read={read} label="Sale history" disabled={disabled} />
    </section>
  );
}
