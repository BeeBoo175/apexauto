"use client";

import { Button } from "@/components/ui/Button";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { useAccountRefresh } from "@/hooks/use-account-refresh";
import { useEmailVerificationResend } from "@/hooks/use-email-verification-resend";
import type { CustomerAccount } from "@/lib/auth/contracts";
import styles from "./AccountPanel.module.css";

export function AccountPanel({ initialAccount }: { initialAccount: CustomerAccount }) {
  const { account, refreshing, refreshFailed, refresh } = useAccountRefresh(initialAccount);
  const logout = useAuthMutation("logout", "/login");
  const resend = useEmailVerificationResend(account);

  let logoutLabel = "Sign out";

  if (logout.uncertain) {
    logoutLabel = "Retry sign out";
  }

  if (logout.busy) {
    logoutLabel = "Signing out";
  }

  if (!account) {
    return null;
  }

  return (
    <div className={styles.panel}>
      <dl className={styles.facts}>
        <div>
          <dt className={styles.label}>Legal name</dt>
          <dd>{account.legalName}</dd>
        </div>
        <div>
          <dt className={styles.label}>Email</dt>
          <dd>{account.email}</dd>
        </div>
        <div>
          <dt className={styles.label}>Email verification</dt>
          <dd>{account.emailVerified ? "Verified" : "Not verified"}</dd>
        </div>
      </dl>
      {!account.emailVerified && (
        <div className={styles.verification}>
          <Button
            variant="secondary"
            disabled={resend.busy || refreshing || logout.busy || logout.uncertain}
            onClick={() => void resend.resend()}
          >
            {resend.busy ? "Requesting email" : "Resend verification email"}
          </Button>
        </div>
      )}
      <div className={styles.actions}>
        <Button
          variant="secondary"
          disabled={refreshing || logout.busy || logout.uncertain}
          onClick={refresh}
        >
          {refreshFailed ? "Retry account refresh" : "Refresh account"}
        </Button>
        <Button disabled={refreshing || logout.busy} onClick={() => void logout.submit()}>
          {logoutLabel}
        </Button>
      </div>
    </div>
  );
}
