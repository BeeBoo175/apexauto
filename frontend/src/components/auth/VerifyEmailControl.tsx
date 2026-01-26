"use client";

import { Button } from "@/components/ui/Button";
import { useEmailVerificationConfirm } from "@/hooks/use-email-verification-confirm";
import styles from "./AuthForm.module.css";

export function VerifyEmailControl() {
  const { token, busy, message, confirm } = useEmailVerificationConfirm();

  return (
    <div className={styles.form}>
      <p className={styles.help}>
        Choose Verify email to confirm this address. The link expires in 24 hours.
      </p>
      {!token && (
        <p role="status" className={styles.status}>
          This verification link is missing its token.
        </p>
      )}
      {message && (
        <p role="status" className={styles.status}>
          {message}
        </p>
      )}
      <Button disabled={!token || busy} onClick={() => void confirm()}>
        {busy ? "Verifying" : "Verify email"}
      </Button>
    </div>
  );
}
