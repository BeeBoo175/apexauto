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

  const initials = account.legalName
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return (
    <div className={styles.panel}>
      <header className={styles.header}>
        <h2 id="account-details-title">Account details</h2>
        <p className={styles.help}>Your sign-in identity and security.</p>
      </header>
      <div className={styles.profile}>
        <span className={styles.avatar} aria-hidden="true">
          {initials}
        </span>
        <div className={styles.identity}>
          <p className={styles.name}>{account.legalName}</p>
          <p className={styles.email}>{account.email}</p>
        </div>
        <span className={styles.verificationState} data-verified={account.emailVerified}>
          {account.emailVerified ? "Verified" : "Not verified"}
        </span>
      </div>
      <dl className={styles.rows}>
        <div className={styles.row}>
          <dt className={styles.label}>Email verification</dt>
          <dd className={styles.value}>
            <span className={styles.help}>
              {account.emailVerified
                ? "Your email is verified for reservations and inspection bookings."
                : "Verify your email to reserve a car or book an inspection."}
            </span>
          </dd>
          {!account.emailVerified && (
            <dd className={styles.control}>
              <Button
                variant="secondary"
                disabled={resend.busy || logout.busy || logout.uncertain}
                onClick={() => void resend.resend()}
              >
                {resend.busy ? "Requesting email" : "Resend verification email"}
              </Button>
            </dd>
          )}
        </div>
        <div className={styles.row}>
          <dt className={styles.label}>Password</dt>
          <dd className={styles.value}>
            <span className={styles.help}>Reset your password by email.</span>
          </dd>
          <dd className={styles.control}>
            <a
              href="/forgot-password"
              className={`${buttonStyles.button} ${buttonStyles.secondary} ${buttonStyles.md}`}
            >
              Password recovery
            </a>
          </dd>
        </div>
        <div className={styles.row}>
          <dt className={styles.label}>Session</dt>
          <dd className={styles.value}>
            <span className={styles.help}>Sign out of ApexAuto on this device.</span>
          </dd>
          <dd className={styles.control}>
            <Button variant="secondary" disabled={logout.busy} onClick={() => void logout.submit()}>
              {logoutLabel}
            </Button>
          </dd>
        </div>
      </dl>
    </div>
  );
}
