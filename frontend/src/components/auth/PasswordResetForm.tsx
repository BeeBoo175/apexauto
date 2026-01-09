"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { usePasswordReset } from "@/hooks/use-password-reset";

export function PasswordResetForm() {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    queueMicrotask(() => setHydrated(true));
  }, []);

  const { token, password, setPassword, busy, uncertain, message, passwordError, submit } =
    usePasswordReset();
  let label = "Reset password";

  if (uncertain) {
    label = "Retry same request";
  }

  if (busy) {
    label = "Please wait";
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submit();
  }

  return (
    <form method="post" onSubmit={handleSubmit} className="flex flex-col gap-5">
      <noscript>
        <p className="text-sm text-muted">
          JavaScript is required to submit this form. Enable JavaScript and reload this page.
        </p>
      </noscript>
      <p>
        Choose a new password. Reset links expire after one hour. Opening this page changes nothing.
      </p>
      <Input
        id="new-password"
        label="New password"
        name="password"
        type="password"
        autoComplete="new-password"
        maxLength={128}
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        disabled={!hydrated || !token || busy || uncertain}
        aria-describedby="password-help password-error"
        aria-invalid={Boolean(passwordError)}
        required
      />
      <p id="password-help" className="text-sm text-muted">
        Use at least 8 characters with uppercase, lowercase, a digit and a symbol.
      </p>
      <p id="password-error" role="status" className="text-sm text-signal">
        {passwordError}
      </p>
      <p role="status" aria-live="polite" className="min-h-12 text-sm text-muted">
        {message}
      </p>
      <Button type="submit" disabled={!hydrated || !token || busy}>
        {label}
      </Button>
    </form>
  );
}
