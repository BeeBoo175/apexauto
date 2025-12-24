"use client";

import { Button } from "@/components/ui/Button";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { useAccountRefresh } from "@/hooks/use-account-refresh";
import { useEmailVerificationResend } from "@/hooks/use-email-verification-resend";
import type { CustomerAccount } from "@/lib/auth/contracts";

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
    <div className="flex flex-col gap-5">
      <dl className="flex flex-col gap-3 wrap-break-word">
        <div>
          <dt className="text-sm text-muted">Legal name</dt>
          <dd>{account.legalName}</dd>
        </div>
        <div>
          <dt className="text-sm text-muted">Email</dt>
          <dd>{account.email}</dd>
        </div>
        <div>
          <dt className="text-sm text-muted">Email verification</dt>
          <dd>{account.emailVerified ? "Verified" : "Not verified"}</dd>
        </div>
      </dl>
      {refreshFailed && (
        <p id="account-refresh-failure" role="status" className="text-sm text-muted">
          Refresh failed. These details may be out of date. Retry when the service is available.
        </p>
      )}
      {!account.emailVerified && (
        <div className="flex flex-col gap-2">
          <Button
            variant="secondary"
            disabled={resend.busy || refreshing || logout.busy || logout.uncertain}
            onClick={() => void resend.resend()}
          >
            {resend.busy ? "Requesting email" : "Resend verification email"}
          </Button>
          {resend.message && (
            <p role="status" className="text-sm text-muted">
              {resend.message}
            </p>
          )}
        </div>
      )}
      <div className="flex flex-wrap gap-3">
        <Button
          variant="secondary"
          disabled={refreshing || logout.busy || logout.uncertain}
          aria-describedby={refreshFailed ? "account-refresh-failure" : undefined}
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
