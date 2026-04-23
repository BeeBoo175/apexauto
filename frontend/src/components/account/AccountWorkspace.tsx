"use client";

import { AccountPanel } from "@/components/auth/AccountPanel";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import type { CustomerAccount } from "@/lib/auth/contracts";
import type { ApiResult, Page } from "@/lib/api";
import type { Appointment } from "@/lib/intake/contracts";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { AppointmentHistory } from "./AppointmentHistory";
import { AccountReservations } from "./AccountReservations";
import type { AccountReservations as ReservationSnapshot } from "@/lib/vehicles/account-reservations";
import styles from "./Account.module.css";

export function AccountWorkspace({
  account,
  history,
  filters,
  page,
  reservations,
  reservationPage,
}: {
  account: CustomerAccount;
  history: ApiResult<Page<Appointment>> | null;
  filters: HistoryFilters;
  page: number;
  reservations: ApiResult<ReservationSnapshot>;
  reservationPage: number;
}) {
  const actor = useIntakeActor({ userId: account.userId, role: "Customer", branchId: null });
  if (!actor.active) {
    return null;
  }
  return (
    <section className={styles.page}>
      <header className={styles.pageHeader}>
        <div>
          <h1 tabIndex={-1}>Your account</h1>
          <p className={styles.welcome}>Welcome back, {account.legalName}.</p>
        </div>
        <nav className={styles.sectionLinks} aria-label="Account sections">
          <a href="#reservations">Reservations</a>
          <a href="#account-details">Account details</a>
          <a href="#appointments">Selling appointments</a>
        </nav>
      </header>
      <AccountReservations
        initial={reservations}
        page={reservationPage}
        actorId={account.userId}
        ready={actor.ready}
      />
      <section
        id="account-details"
        className={styles.accountDetails}
        aria-labelledby="account-details-title"
      >
        <AccountPanel initialAccount={account} />
      </section>
      <div id="appointments" className={styles.appointments}>
        {history ? (
          <AppointmentHistory initial={history} filters={filters} page={page} ready={actor.ready} />
        ) : (
          <section aria-labelledby="appointments-title">
            <h2 id="appointments-title">Your appointments</h2>
            <p className={styles.help}>Verify your email to view your appointment history.</p>
          </section>
        )}
      </div>
    </section>
  );
}
