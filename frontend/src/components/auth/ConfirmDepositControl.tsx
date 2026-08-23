"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { useReservationDepositConfirm } from "@/hooks/use-reservation-deposit-confirm";
import styles from "./AuthForm.module.css";

export function ConfirmDepositControl() {
  const { token, busy, confirmed, confirm } = useReservationDepositConfirm();
  useLoadingProgress("Confirming your deposit", busy);

  if (confirmed) {
    return (
      <div className={styles.form}>
        <p className={styles.help}>
          Deposit confirmed. The car is reserved and the reservation receipt is on its way by email.
        </p>
        <Link href="/account" className={styles.accountLink}>
          View reservations
        </Link>
      </div>
    );
  }

  return (
    <div className={styles.form}>
      <p className={styles.help}>
        Choose Confirm deposit to reserve the car. The RM500 deposit is refundable. You may be taken
        to our payment provider to pay it securely. The link expires 24 hours after it was sent.
      </p>
      <Button disabled={!token || busy} onClick={() => void confirm()}>
        {busy ? "Confirming" : "Confirm deposit"}
      </Button>
    </div>
  );
}
