"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useAppointmentHistory } from "@/hooks/use-appointment-history";
import type { ApiResult, Page } from "@/lib/api";
import type { Appointment } from "@/lib/intake/contracts";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { CancelAppointment } from "./CancelAppointment";
import styles from "./Account.module.css";

export function AppointmentHistory({
  initial,
  filters,
  page,
  ready,
}: {
  initial: ApiResult<Page<Appointment>>;
  filters: HistoryFilters;
  page: number;
  ready: boolean;
}) {
  const history = useAppointmentHistory(initial, filters, page);
  const [cancelling, setCancelling] = useState<Appointment | null>(null);
  const { snapshot, active, draft } = history;
  const disabled = !ready || history.busy || Boolean(cancelling);
  const empty = snapshot?.total === 0 && !active.filters.from && !active.filters.to;
  return (
    <section className={styles.history} aria-labelledby="appointment-history-title">
      <div className={styles.heading}>
        <h2 id="appointment-history-title">Your appointments</h2>
        <Button variant="secondary" disabled={disabled} onClick={() => void history.load()}>
          Refresh appointments
        </Button>
      </div>
      {!empty && (
        <form
          method="post"
          className={styles.filters}
          onSubmit={(event) => {
            event.preventDefault();
            void history.load(draft, 1);
          }}
        >
          <Input
            label="From (Malaysia)"
            type="date"
            value={draft.from}
            disabled={disabled}
            onChange={(event) => history.setDraft({ ...draft, from: event.target.value })}
          />
          <Input
            label="To (Malaysia)"
            type="date"
            value={draft.to}
            disabled={disabled}
            onChange={(event) => history.setDraft({ ...draft, to: event.target.value })}
          />
          <Button variant="secondary" type="submit" disabled={disabled}>
            Apply dates
          </Button>
        </form>
      )}
      {!empty && (
        <p className={styles.help}>
          Filter appointment dates in Malaysia. Leave both dates empty for all history.
        </p>
      )}
      {empty && (
        <div className={styles.emptyAppointments}>
          <p>No inspection appointments yet.</p>
          <p className={styles.help}>
            Planning to sell your car? Start with a valuation, then choose an inspection
            appointment.
          </p>
          <Link href="/sell" prefetch={false}>
            Value your car
          </Link>
        </div>
      )}
      {snapshot && !empty && (
        <>
          <p className={styles.help}>
            {snapshot.total} appointments · Page {active.page}
            {active.filters.from
              ? ` · ${active.filters.from} to ${active.filters.to}`
              : " · All dates"}
          </p>
          {snapshot.items.length === 0 && (
            <div>
              <p>No appointments match these filters.</p>
            </div>
          )}
          <div className={styles.records}>
            {snapshot.items.map((item) => (
              <article key={item.id} className={styles.appointment}>
                <div className={styles.heading}>
                  <h3>{item.branchName}</h3>
                  <span className={styles.state}>{historyStatus(item)}</span>
                </div>
                <p className={styles.plate}>{item.registrationPlate}</p>
                <dl className={styles.details}>
                  <div>
                    <dt>Inspection time</dt>
                    <dd>{formatMalaysianDateTime(item.startsAt)}</dd>
                  </div>
                  <div>
                    <dt>Booked</dt>
                    <dd>{formatMalaysianDateTime(item.createdAt)}</dd>
                  </div>
                  <div>
                    <dt>Reference</dt>
                    <dd className={styles.reference}>{item.id}</dd>
                  </div>
                  {item.cancelledAt && (
                    <div>
                      <dt>Cancelled</dt>
                      <dd>{formatMalaysianDateTime(item.cancelledAt)}</dd>
                    </div>
                  )}
                </dl>
                {!item.cancelledAt && !item.noShowAt && !item.checkedInAt && (
                  <Button
                    variant="secondary"
                    disabled={disabled}
                    onClick={() => setCancelling(item)}
                  >
                    Request cancellation
                  </Button>
                )}
              </article>
            ))}
          </div>
          <nav className={styles.paging} aria-label="Appointment history pages">
            <Button
              variant="secondary"
              disabled={disabled || active.page <= 1}
              onClick={() => void history.load(active.filters, active.page - 1)}
            >
              Previous appointments
            </Button>
            <Button
              variant="secondary"
              disabled={disabled || active.page * snapshot.pageSize >= snapshot.total}
              onClick={() => void history.load(active.filters, active.page + 1)}
            >
              Next appointments
            </Button>
          </nav>
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

function historyStatus(item: Appointment) {
  if (item.checkedInAt) {
    return "Checked in";
  }
  if (item.noShowAt) {
    return "No show";
  }
  if (item.cancelledAt) {
    return "Cancelled";
  }
  return "Appointment";
}
