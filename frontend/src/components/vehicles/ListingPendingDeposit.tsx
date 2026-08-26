"use client";

import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { useMinuteClock } from "@/hooks/use-minute-clock";
import { DEPOSIT_REQUEST_HOURS } from "@/lib/vehicles/deposit-terms";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { DepositRequestCancel } from "./DepositRequestCancel";
import styles from "./ListingPublication.module.css";

// A car held by a deposit request until its customer confirms: the customer, the time left and a way to release it.
export function ListingPendingDeposit({
  pending,
  onChanged,
}: {
  pending: NonNullable<ListingWorkspace["pendingDeposit"]>;
  onChanged: () => void;
}) {
  const [now] = useMinuteClock();

  return (
    <div className={styles.pendingDeposit}>
      <span>
        <CustomerAvatar id={pending.customerId} name={pending.customer} />
        {" · awaiting deposit confirmation"}
      </span>
      <HoldTimer
        dueAt={pending.expiresAtUtc}
        now={now}
        totalSeconds={DEPOSIT_REQUEST_HOURS * 3600}
        compact
      />
      <DepositRequestCancel requestId={pending.requestId} onChanged={onChanged} />
    </div>
  );
}
