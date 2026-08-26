"use client";

import { formatCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

import { usePaymentMode } from "@/hooks/use-payment-mode";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { useAccountReservations } from "@/hooks/use-account-reservations";
import {
  type ReservationFilters,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import { EmptyState } from "./EmptyState";
import { HistoryFilterForm } from "./HistoryFilterForm";
import { ReservedCar } from "./ReservedCar";
import { ReservationHistoryRow } from "./ReservationHistoryRow";
import { CancelReservation } from "./CancelReservation";
import { DepositRefunds } from "./DepositRefunds";
import { AccountLoadingShell } from "./AccountLoadingShell";
import styles from "./Reservations.module.css";
import historyStyles from "./ReservationHistory.module.css";

export function AccountReservations({
  page,
  filters,
  actorId,
  ready,
}: {
  page: number;
  filters: ReservationFilters;
  actorId: string;
  ready: boolean;
}) {
  const live = usePaymentMode();
  const history = useAccountReservations(page, filters, actorId);
  const [cancelling, setCancelling] = useState<AccountReservation | null>(null);
  const { snapshot } = history;
  const featured = snapshot?.active;
  const disabled = !ready || history.busy || Boolean(cancelling);
  const filtered = Boolean(
    history.filters.from || history.filters.to || history.filters.search.trim(),
  );
  const records = snapshot?.items.filter((item) => !item.isActive) ?? [];
  const pages = snapshot ? Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize)) : 1;
  const first = snapshot?.items.length ? (history.page - 1) * snapshot.pageSize + 1 : 0;
  const last = first && snapshot ? first + snapshot.items.length - 1 : 0;

  return (
    <section id="reservations" aria-label="Your reservations" className={styles.section}>
      <div className={styles.heading}>
        <h2 id="reservations-title">Your reservation</h2>
        <p className={styles.help}>
          Reserve a car with a refundable {formatCurrency(RESERVATION_DEPOSIT, "RM")}{" "}
          {live ? "" : "simulated "}deposit.
        </p>
      </div>
      <div className={styles.body}>
        <DepositRefunds actorId={actorId} />
        {!snapshot && history.busy && <AccountLoadingShell label="Loading reservations" />}
        {!snapshot && !history.busy && (
          <Button variant="secondary" disabled={disabled} onClick={() => void history.load()}>
            Try loading reservations again
          </Button>
        )}
        {featured && (
          <div id="reserved-car" className={styles.currentReservation}>
            <ReservedCar
              key={featured.receipt.id}
              item={featured}
              disabled={disabled}
              onCancel={() => setCancelling(featured)}
            />
          </div>
        )}
        {snapshot && !featured && (
          <EmptyState
            title="No active reservation"
            description="When you reserve a car, it and its booking details appear here."
            href="/vehicles"
            action="Browse cars"
          />
        )}
        {snapshot && (snapshot.total > 0 || filtered) && (
          <section className={historyStyles.history} aria-labelledby="reservation-history-title">
            <div className={historyStyles.historyHead}>
              <h3 id="reservation-history-title">Reservation history</h3>
              <p className={styles.help}>
                {snapshot.total} {snapshot.total === 1 ? "reservation" : "reservations"} · Page{" "}
                {history.page} · Newest first
              </p>
            </div>
            <HistoryFilterForm
              draft={history.draft}
              disabled={disabled}
              busy={history.busy}
              searchLabel="Search reservations"
              searchPlaceholder="Search reservation number, vehicle or branch"
              onDraft={history.setDraft}
              onApply={() => void history.load(1, history.draft)}
              onClearSearch={() => {
                const next = { ...history.draft, search: "" };
                history.setDraft(next);
                void history.load(1, next);
              }}
            />
            {records.length === 0 && (
              <div className={historyStyles.noMatches}>
                <p className={styles.help}>
                  {filtered
                    ? "No reservations match your search."
                    : "No past reservations on this page."}
                </p>
                {filtered && (
                  <Button
                    variant="ghost"
                    size="compact"
                    disabled={disabled}
                    onClick={() => {
                      const cleared = { from: "", to: "", search: "" };
                      history.setDraft(cleared);
                      void history.load(1, cleared);
                    }}
                  >
                    Clear filters
                  </Button>
                )}
              </div>
            )}
            <ul className={historyStyles.historyList}>
              {records.map((item) => (
                <ReservationHistoryRow key={item.receipt.id} item={item} />
              ))}
            </ul>
            {snapshot.total > 0 && (
              <nav aria-label="Reservation history pages" className={historyStyles.pager}>
                <span className={historyStyles.pagerMeta} aria-live="polite">
                  {first}–{last} of {snapshot.total}
                </span>
                <div className={historyStyles.pagerControls}>
                  <Button
                    variant="ghost"
                    size="icon"
                    disabled={disabled || history.page <= 1}
                    onClick={() => void history.load(history.page - 1)}
                    aria-label="Previous reservations"
                    title="Previous page"
                  >
                    <Icon name="previous" />
                  </Button>
                  <span>
                    Page {history.page} of {pages}
                  </span>
                  <Button
                    variant="ghost"
                    size="icon"
                    disabled={disabled || history.page >= pages}
                    onClick={() => void history.load(history.page + 1)}
                    aria-label="Next reservations"
                    title="Next page"
                  >
                    <Icon name="next" />
                  </Button>
                </div>
              </nav>
            )}
          </section>
        )}
      </div>
      {cancelling && (
        <CancelReservation
          item={cancelling}
          actorId={actorId}
          onClose={() => setCancelling(null)}
          onConfirmed={history.cancelled}
          onChanged={() => void history.load()}
        />
      )}
    </section>
  );
}
