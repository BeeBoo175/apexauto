"use client";

import { AccountPanel } from "@/components/auth/AccountPanel";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import type { AccountProfile } from "@/lib/account/contracts";
import styles from "./Account.module.css";

// Staff share the customer's account presentation, without the customer record sections.
export function StaffAccount({ profile }: { profile: AccountProfile }) {
  const actor = useIntakeActor({
    userId: profile.userId,
    role: profile.role,
    branchId: profile.branchId,
  });

  if (!actor.active) {
    return null;
  }

  return (
    <section className={styles.page}>
      <header className={styles.pageHeader}>
        <div>
          <h1 tabIndex={-1}>Your account</h1>
          <p className={styles.welcome}>Signed in as {profile.name}.</p>
        </div>
      </header>
      <div className={styles.content}>
        <section className={styles.panel} aria-labelledby="account-details-title">
          <AccountPanel profile={profile} />
        </section>
      </div>
    </section>
  );
}
