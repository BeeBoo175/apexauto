"use client";

import { Button } from "@/components/ui/Button";
import { useEmailVerificationConfirm } from "@/hooks/use-email-verification-confirm";
import styles from "./AuthForm.module.css";

export function VerifyEmailControl() {
  const { token, busy, confirm } = useEmailVerificationConfirm();

  return (
    <div className={styles.form}>
      <p className={styles.help}>
        Choose Verify email to confirm this address. The link expires in 24 hours.
      </p>
      <Button disabled={!token || busy} onClick={() => void confirm()}>
        {busy ? "Verifying" : "Verify email"}
      </Button>
    </div>
  );
}
