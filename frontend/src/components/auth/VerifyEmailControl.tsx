"use client";

import { Button } from "@/components/ui/Button";
import { useEmailVerificationConfirm } from "@/hooks/use-email-verification-confirm";

export function VerifyEmailControl() {
  const { token, busy, message, confirm } = useEmailVerificationConfirm();

  return (
    <div className="flex flex-col gap-4">
      <p>Choose Verify email to confirm this address. The link expires in 24 hours.</p>
      {!token && <p role="status">This verification link is missing its token.</p>}
      {message && (
        <p role="status" className="text-sm text-muted">
          {message}
        </p>
      )}
      <Button disabled={!token || busy} onClick={() => void confirm()}>
        {busy ? "Verifying" : "Verify email"}
      </Button>
    </div>
  );
}
