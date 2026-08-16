"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import statusTone from "@/components/ui/StatusTone.module.css";
import { useAppointmentHistory } from "@/hooks/use-appointment-history";
import type { Appointment } from "@/lib/intake/contracts";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { CancelAppointment } from "./CancelAppointment";
import { EmptyState } from "./EmptyState";
import { HistoryFilterForm } from "./HistoryFilterForm";
import { AccountLoadingShell } from "./AccountLoadingShell";
import styles from "./Account.module.css";
import historyStyles from "./ReservationHistory.module.css";

export function AppointmentHistory({
  filters,
  page,
  ready,
}: {
  filters: HistoryFilters;
  page: number;
  ready: boolean;
}) {
  const history = useAppointmentHistory(filters, page);
  const [cancelling, setCancelling] = useState<Appointment | null>(null);
  const { snapshot, active, draft } = history;
  const disabled = !ready || history.busy || Boolean(cancelling);
  const filtered = Boolean(active.filters.from || active.filters.to);
  const empty = snapshot?.total === 0 && !filtered;
  const pages = snapshot ? Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize)) : 1;
  const first = snapshot?.items.length ? (active.page - 1) * snapshot.pageSize + 1 : 0;
  const last = first && snapshot ? first + snapshot.items.length - 1 : 0;

  return (
    <section className={styles.history} aria-labelledby="appointment-history-title">
      <div className={styles.heading}>
        <div>
          <h2 id="appointment-history-title">Your appointments</h2>
          <p className={styles.help}>
            Inspection appointments for the cars you are selling, with their dates and status.
          </p>
        </div>
      </div>
      {!snapshot && history.busy && <AccountLoadingShell label="Loading appointments" />}
      {!snapshot && !history.busy && (
        <Button variant="secondary" disabled={disabled} onClick={() => void history.load()}>
          Try loading appointments again
        </Button>
      )}
      {!empty && (
        <div className={styles.filterGroup}>
          <HistoryFilterForm
            draft={draft}
            disabled={disabled}
            busy={history.busy}
            onDraft={history.setDraft}
            onApply={() => void history.load(draft, 1)}
          />
          <p className={styles.help}>
            Filter appointment dates in Malaysia. Leave both dates empty for all history.
          </p>
        </div>
      )}
      {empty && (
        <EmptyState
          title="No inspection appointments yet"
          description="Planning to sell your car? Start with a valuation, then choose an inspection appointment."
          href="/sell"
          action="Value your car"
          prefetch={false}
        />
      )}
      {snapshot && !empty && (
        <>
          <p className={styles.help}>
            {snapshot.total} {snapshot.total === 1 ? "appointment" : "appointments"} · Page{" "}
            {active.page}
            {active.filters.from
              ? ` · ${active.filters.from} to ${active.filters.to}`
              : " · All dates"}
          </p>
          {snapshot.items.length === 0 && (
            <div className={historyStyles.noMatches}>
              <p className={styles.help}>No appointments match these filters.</p>
              {filtered && (
                <Button
                  variant="ghost"
                  size="compact"
                  disabled={disabled}
                  onClick={() => {
                    const cleared = { from: "", to: "" };
                    history.setDraft(cleared);
                    void history.load(cleared, 1);
                  }}
                >
                  Clear filters
                </Button>
              )}
            </div>
          )}
          <ul className={historyStyles.historyList}>
            {snapshot.items.map((item) => {
              const status = historyStatus(item);
              const cancellable = !item.cancelledAt && !item.noShowAt && !item.checkedInAt;
              return (
                <li key={item.id}>
                  <article className={styles.appointmentCard}>
                    <div className={styles.appointmentHeader}>
                      <div className={styles.appointmentIdentity}>
                        <span className={styles.appointmentPlate} translate="no">
                          {item.registrationPlate}
                        </span>
                        <h3>{item.branchName}</h3>
                      </div>
                      <span className={`${statusTone.badge} ${statusTone[status.tone]}`}>
                        {status.label}
                      </span>
                    </div>
                    <dl className={styles.appointmentFacts}>
                      <div>
                        <dt>Inspection time</dt>
                        <dd>{formatMalaysianDateTime(item.startsAt)}</dd>
                      </div>
                      <div>
                        <dt>Booked</dt>
                        <dd>{formatMalaysianDateTime(item.createdAt)}</dd>
                      </div>
                      {item.cancelledAt && (
                        <div>
                          <dt>Cancelled</dt>
                          <dd>{formatMalaysianDateTime(item.cancelledAt)}</dd>
                        </div>
                      )}
                      <div>
                        <dt>Reference</dt>
                        <dd className={styles.reference}>{item.id}</dd>
                      </div>
                    </dl>
                    {cancellable && (
                      <div className={styles.appointmentFooter}>
                        <Button
                          variant="secondary"
                          disabled={disabled}
                          onClick={() => setCancelling(item)}
                        >
                          Request cancellation
                        </Button>
                      </div>
                    )}
                  </article>
                </li>
              );
            })}
          </ul>
          {snapshot.total > 0 && (
            <nav className={historyStyles.pager} aria-label="Appointment history pages">
              <span className={historyStyles.pagerMeta} aria-live="polite">
                {first}–{last} of {snapshot.total}
              </span>
              <div className={historyStyles.pagerControls}>
                <Button
                  variant="ghost"
                  size="icon"
                  disabled={disabled || active.page <= 1}
                  onClick={() => void history.load(active.filters, active.page - 1)}
                  aria-label="Previous appointments"
                  title="Previous page"
                >
                  <Icon name="previous" />
                </Button>
                <span>
                  Page {active.page} of {pages}
                </span>
                <Button
                  variant="ghost"
                  size="icon"
                  disabled={disabled || active.page >= pages}
                  onClick={() => void history.load(active.filters, active.page + 1)}
                  aria-label="Next appointments"
                  title="Next page"
                >
                  <Icon name="next" />
                </Button>
              </div>
            </nav>
          )}
        </>
      )}
      {cancelling && (
        <CancelAppointment
          key={cancelling.id}
          appointment={cancelling}
          onClose={() => setCancelling(null)}
          onConfirmed={history.cancelled}
        />
      )}
    </section>
  );
}

function historyStatus(item: Appointment): {
  label: string;
  tone: "info" | "success" | "warning" | "neutral";
} {
  if (item.checkedInAt) {
    return { label: "Checked in", tone: "success" };
  }
  if (item.noShowAt) {
    return { label: "No show", tone: "warning" };
  }
  if (item.cancelledAt) {
    return { label: "Cancelled", tone: "neutral" };
  }
  return { label: "Booked", tone: "info" };
}
