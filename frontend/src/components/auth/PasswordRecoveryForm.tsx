"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { usePasswordRecovery } from "@/hooks/use-password-recovery";

export function PasswordRecoveryForm() {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    queueMicrotask(() => setHydrated(true));
  }, []);

  const { email, setEmail, busy, uncertain, message, submit } = usePasswordRecovery();
  let label = "Send reset link";

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
      <p>Enter your account email to request a password reset link.</p>
      <Input
        label="Email"
        name="email"
        type="email"
        autoComplete="username"
        maxLength={254}
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        disabled={!hydrated || busy || uncertain}
        required
      />
      <p role="status" aria-live="polite" className="min-h-12 text-sm text-muted">
        {message}
      </p>
      <Button type="submit" disabled={!hydrated || busy}>
        {label}
      </Button>
    </form>
  );
}
