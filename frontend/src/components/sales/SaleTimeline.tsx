"use client";

import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { StaffLink } from "@/components/staff/StaffLink";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { useStaffHistory } from "@/hooks/use-staff-history";
import { privateApiFetch } from "@/lib/api-browser";
import { formatCurrency } from "@/lib/currency-format";
import type { SaleSection } from "@/lib/sales/sale-sections";
import {
  appendSaleHistory,
  historyChange,
  historyTitle,
  parseSaleHistory,
  saleHistoryPath,
  versionSection,
} from "@/lib/sales/sale-history";
import timeline from "@/components/staff/StaffHistory.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

// The sale's retained history, newest first, as the workshop's record history. Each entry opens the sale as it
// stood after that action, in the section the action belongs to.
export function SaleTimeline({
  saleId,
  disabled,
  onOpenVersion,
}: {
  saleId: string;
  disabled: boolean;
  onOpenVersion: (version: number, section: SaleSection) => void;
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
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Sale history</h2>
      </div>
      <StaffTimeline>
        {history?.items.map((item) => {
          const section = versionSection(item.kind);
          return (
            <StaffTimelineEvent
              key={item.id}
              title={historyTitle(item)}
              at={item.recordedAt}
              context={item.actor}
            >
              {item.reason && <p>{item.reason}</p>}
              {item.changes.length > 0 && (
                <ul className={timeline.changes}>
                  {item.changes.map((change) => (
                    <li key={change.field}>{historyChange(change)}</li>
                  ))}
                </ul>
              )}
              {item.refund !== null && <p>Deposit refunded: {formatCurrency(item.refund)}</p>}
              <div className={timeline.eventActions}>
                <StaffLink
                  prefetch={false}
                  href={`?sale=${saleId}&section=${section}&version=${item.version}`}
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
                      onOpenVersion(item.version, section);
                    }
                  }}
                >
                  View this version
                </StaffLink>
              </div>
            </StaffTimelineEvent>
          );
        })}
      </StaffTimeline>
      <StaffHistoryProgress read={read} label="Sale history" disabled={disabled} />
    </section>
  );
}
