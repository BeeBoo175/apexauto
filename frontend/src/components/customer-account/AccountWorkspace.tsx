"use client";

import { useState } from "react";
import { usePathname } from "next/navigation";
import { AccountPanel } from "@/components/auth/AccountPanel";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import type { AccountProfile } from "@/lib/profile/contracts";
import {
  accountSectionPath,
  accountSections as sections,
  sectionFromPathname,
  type AccountSection,
} from "@/lib/profile/sections";
import { writeAddress } from "@/lib/browser-url";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { AppointmentHistory } from "./AppointmentHistory";
import { AccountReservations } from "./AccountReservations";
import { AccountTestDrives } from "./AccountTestDrives";
import type { TestDriveHistoryFilters } from "@/lib/test-drives/account";
import type { ReservationFilters } from "@/lib/vehicles/account-reservations";
import styles from "./Account.module.css";

interface SectionEntry<Filters> {
  page: number;
  filters: Filters;
}

const emptyDates: HistoryFilters = { from: "", to: "" };
const emptySearch: ReservationFilters & TestDriveHistoryFilters = { from: "", to: "", search: "" };

export function AccountWorkspace({
  profile,
  section: entrySection,
  appointments,
  reservations,
  testDrives,
}: {
  profile: AccountProfile;
  section: AccountSection;
  appointments: SectionEntry<HistoryFilters> | null;
  reservations: SectionEntry<ReservationFilters> | null;
  testDrives: SectionEntry<TestDriveHistoryFilters> | null;
}) {
  const actor = useIntakeActor({ userId: profile.userId, role: "Customer", branchId: null });
  const section = sectionFromPathname(usePathname());
  // A section mounts the first time it is opened and then stays mounted, keeping its drafts.
  const [opened, setOpened] = useState<AccountSection[]>([entrySection]);
  if (!opened.includes(section)) {
    setOpened([...opened, section]);
  }
  if (!actor.active) {
    return null;
  }
  const mounted = (id: AccountSection) => opened.includes(id) || section === id;
  return (
    <section className={styles.page}>
      <header className={styles.heroBand}>
        <div className={styles.heroInner}>
          <div>
            <h1 tabIndex={-1}>Your account</h1>
            <p className={styles.welcome}>Welcome back, {profile.name}.</p>
          </div>
        </div>
      </header>
      <div className={styles.tabsBand}>
        <nav className={styles.tabs} aria-label="Account sections">
          {sections.map(({ id, label }) => (
            <a
              key={id}
              href={accountSectionPath(id)}
              aria-current={section === id ? "location" : undefined}
              onClick={(event) => {
                if (event.button === 0 && !(event.metaKey || event.ctrlKey || event.shiftKey)) {
                  event.preventDefault();
                  writeAddress(accountSectionPath(id), null, "push");
                }
              }}
            >
              {label}
            </a>
          ))}
        </nav>
      </div>
      <div className={styles.content}>
        <div hidden={section !== "reservations"}>
          {mounted("reservations") && (
            <AccountReservations
              page={reservations?.page ?? 1}
              filters={reservations?.filters ?? emptySearch}
              actorId={profile.userId}
              ready={actor.ready}
            />
          )}
        </div>
        <div className={styles.panel} hidden={section !== "appointments"}>
          {mounted("appointments") &&
            (profile.emailVerified ? (
              <AppointmentHistory
                filters={appointments?.filters ?? emptyDates}
                page={appointments?.page ?? 1}
                ready={actor.ready}
              />
            ) : (
              <section aria-labelledby="appointments-title">
                <h2 id="appointments-title">Your appointments</h2>
                <p className={styles.help}>Verify your email to view your appointment history.</p>
              </section>
            ))}
        </div>
        <div className={styles.panel} hidden={section !== "test-drives"}>
          {mounted("test-drives") && (
            <AccountTestDrives
              page={testDrives?.page ?? 1}
              filters={testDrives?.filters ?? emptySearch}
              actorId={profile.userId}
              ready={actor.ready}
            />
          )}
        </div>
        <section
          className={styles.panel}
          aria-labelledby="account-details-title"
          hidden={section !== "details"}
        >
          <AccountPanel profile={profile} />
        </section>
      </div>
    </section>
  );
}
