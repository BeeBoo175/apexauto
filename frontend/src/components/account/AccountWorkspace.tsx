"use client";

import { useEffect, useState } from "react";
import { AccountPanel } from "@/components/auth/AccountPanel";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import type { CustomerAccount } from "@/lib/auth/contracts";
import type { ApiResult, Page } from "@/lib/api";
import type { Appointment } from "@/lib/intake/contracts";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { AppointmentHistory } from "./AppointmentHistory";
import { AccountReservations } from "./AccountReservations";
import { AccountTestDrives } from "./AccountTestDrives";
import type { TestDriveHistory, TestDriveHistoryFilters } from "@/lib/test-drives/account";
import type {
  AccountReservations as ReservationSnapshot,
  ReservationFilters,
} from "@/lib/vehicles/account-reservations";
import styles from "./Account.module.css";

const sections = [
  { id: "reservations", label: "Reservations" },
  { id: "appointments", label: "Selling appointments" },
  { id: "test-drives", label: "Test drives" },
  { id: "account-details", label: "Account details" },
] as const;

type AccountSection = (typeof sections)[number]["id"];

function sectionFromHash(hash: string) {
  return sections.find(({ id }) => `#${id}` === hash)?.id;
}

export function AccountWorkspace({
  account,
  history,
  filters,
  page,
  reservations,
  reservationPage,
  reservationFilters,
  testDrives,
  testDriveQuery,
}: {
  account: CustomerAccount;
  history: ApiResult<Page<Appointment>> | null;
  filters: HistoryFilters;
  page: number;
  reservations: ApiResult<ReservationSnapshot>;
  reservationPage: number;
  reservationFilters: ReservationFilters;
  testDrives: ApiResult<TestDriveHistory>;
  testDriveQuery: { page: number; filters: TestDriveHistoryFilters };
}) {
  const actor = useIntakeActor({ userId: account.userId, role: "Customer", branchId: null });
  const [section, setSection] = useState<AccountSection>("reservations");
  useEffect(() => {
    function sync() {
      const next = sectionFromHash(window.location.hash);
      if (next) {
        setSection(next);
      }
    }
    queueMicrotask(sync);
    window.addEventListener("hashchange", sync);
    return () => window.removeEventListener("hashchange", sync);
  }, []);
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
      </header>
      <nav className={styles.tabs} aria-label="Account sections">
        {sections.map(({ id, label }) => (
          <a
            key={id}
            href={`#${id}`}
            aria-current={section === id ? "location" : undefined}
            onClick={(event) => {
              if (event.button === 0 && !(event.metaKey || event.ctrlKey || event.shiftKey)) {
                event.preventDefault();
                setSection(id);
                window.history.replaceState(null, "", `#${id}`);
              }
            }}
          >
            {label}
          </a>
        ))}
      </nav>
      <div className={styles.content}>
        <div hidden={section !== "reservations"}>
          <AccountReservations
            initial={reservations}
            page={reservationPage}
            filters={reservationFilters}
            actorId={account.userId}
            ready={actor.ready}
          />
        </div>
        <div id="appointments" className={styles.panel} hidden={section !== "appointments"}>
          {history ? (
            <AppointmentHistory
              initial={history}
              filters={filters}
              page={page}
              ready={actor.ready}
            />
          ) : (
            <section aria-labelledby="appointments-title">
              <h2 id="appointments-title">Your appointments</h2>
              <p className={styles.help}>Verify your email to view your appointment history.</p>
            </section>
          )}
        </div>
        <div id="test-drives" className={styles.panel} hidden={section !== "test-drives"}>
          <AccountTestDrives
            initial={testDrives}
            page={testDriveQuery.page}
            filters={testDriveQuery.filters}
            actorId={account.userId}
            ready={actor.ready}
          />
        </div>
        <section
          id="account-details"
          className={styles.panel}
          aria-labelledby="account-details-title"
          hidden={section !== "account-details"}
        >
          <AccountPanel initialAccount={account} />
        </section>
      </div>
    </section>
  );
}
