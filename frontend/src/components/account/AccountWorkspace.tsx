"use client";

import { AccountPanel } from "@/components/auth/AccountPanel";
import { Card } from "@/components/ui/Card";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import type { CustomerAccount } from "@/lib/auth/contracts";
import type { ApiResult, Page } from "@/lib/api";
import type { Appointment } from "@/lib/intake/contracts";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { AppointmentHistory } from "./AppointmentHistory";
import styles from "./Account.module.css";

export function AccountWorkspace({
  account,
  history,
  filters,
  page,
}: {
  account: CustomerAccount;
  history: ApiResult<Page<Appointment>>;
  filters: HistoryFilters;
  page: number;
}) {
  const actor = useIntakeActor({ userId: account.userId, role: "Customer", branchId: null });
  if (!actor.active) {
    return null;
  }
  return (
    <section className={styles.page}>
      <h1 tabIndex={-1}>Your account</h1>
      <div className={styles.workspace}>
        <aside aria-label="Account details">
          <Card>
            <AccountPanel initialAccount={account} />
            <a className={styles.recovery} href="/forgot-password">
              Password recovery
            </a>
          </Card>
        </aside>
        <AppointmentHistory initial={history} filters={filters} page={page} ready={actor.ready} />
      </div>
    </section>
  );
}
