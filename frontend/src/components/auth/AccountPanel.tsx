"use client";

import { Button } from "@/components/ui/Button";
import buttonStyles from "@/components/ui/Button.module.css";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { useEmailVerificationResend } from "@/hooks/use-email-verification-resend";
import type { CustomerAccount } from "@/lib/auth/contracts";
import styles from "./AccountPanel.module.css";

export function AccountPanel({ initialAccount }: { initialAccount: CustomerAccount }) {
  const account = initialAccount;
  const logout = useAuthMutation("logout", "/login");
  const resend = useEmailVerificationResend(account);

  let logoutLabel = "Sign out";

  if (logout.uncertain) {
    logoutLabel = "Retry sign out";
  }

  if (logout.busy) {
    logoutLabel = "Signing out";
  }

  return (
    <div className={styles.panel}>
      <header className={styles.header}>
        <h2 id="account-details-title">Account details</h2>
        <div className={styles.actions}>
          <a
            href="/forgot-password"
            className={`${buttonStyles.button} ${buttonStyles.secondary} ${buttonStyles.compact}`}
          >
            Password recovery
          </a>
          <Button
            variant="secondary"
            size="compact"
            disabled={logout.busy}
            onClick={() => void logout.submit()}
          >
            {logoutLabel}
          </Button>
        </div>
      </header>
      <div className={styles.details}>
        <dl className={styles.facts}>
          <div>
            <dt className={styles.label}>Legal name</dt>
            <dd className={styles.name}>{account.legalName}</dd>
          </div>
        </dl>
        <div className={styles.verification}>
          <dl>
            <div>
              <dt className={styles.label}>Email</dt>
              <dd className={styles.email}>{account.email}</dd>
              <dd className={styles.verificationState} data-verified={account.emailVerified}>
                {account.emailVerified ? "Verified" : "Not verified"}
              </dd>
            </div>
          </dl>
          <p className={styles.help}>
            {account.emailVerified
              ? "Your email is verified for reservations and inspection bookings."
              : "Verify your email to reserve a car or book an inspection."}
          </p>
          {!account.emailVerified && (
            <Button
              variant="secondary"
              disabled={resend.busy || logout.busy || logout.uncertain}
              onClick={() => void resend.resend()}
            >
              {resend.busy ? "Requesting email" : "Resend verification email"}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
