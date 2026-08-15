"use client";

import { Button } from "@/components/ui/Button";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";
import { useReservationDepositConfirm } from "@/hooks/use-reservation-deposit-confirm";
import styles from "./AuthForm.module.css";

export function ConfirmDepositControl() {
  const { token, busy, confirm } = useReservationDepositConfirm();
  useLoadingProgress("Confirming your deposit", busy);

  return (
    <div className={styles.form}>
      <p className={styles.help}>
        Choose Confirm deposit to reserve the car. The RM500 deposit is simulated and refundable,
        and no payment is taken. The link expires 24 hours after it was sent.
      </p>
      <Button disabled={!token || busy} onClick={() => void confirm()}>
        {busy ? "Confirming" : "Confirm deposit"}
      </Button>
    </div>
  );
}
