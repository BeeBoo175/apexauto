"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { usePasswordReset } from "@/hooks/use-password-reset";
import styles from "./AuthForm.module.css";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";

export function PasswordResetForm() {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    queueMicrotask(() => setHydrated(true));
  }, []);

  const { token, password, setPassword, busy, uncertain, passwordError, submit } =
    usePasswordReset();
  useLoadingProgress("Resetting your password", busy);
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
    <form method="post" onSubmit={handleSubmit} className={styles.form}>
      <noscript>
        <p className={styles.notice}>
          JavaScript is required to submit this form. Enable JavaScript and reload this page.
        </p>
      </noscript>
      <p className={styles.help}>
        Reset links expire after one hour. Opening this page changes nothing.
      </p>
      <div className={styles.password}>
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
          aria-describedby="password-help"
          aria-invalid={Boolean(passwordError)}
          required
        />
        <p id="password-help" className={styles.notice}>
          Use at least 8 characters with uppercase, lowercase, a digit and a symbol.
        </p>
      </div>
      <Button type="submit" disabled={!hydrated || !token || busy} className={styles.submit}>
        {label}
      </Button>
    </form>
  );
}
