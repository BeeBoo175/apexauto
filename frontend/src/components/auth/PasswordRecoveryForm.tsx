"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { usePasswordRecovery } from "@/hooks/use-password-recovery";
import styles from "./AuthForm.module.css";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";

export function PasswordRecoveryForm() {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    queueMicrotask(() => setHydrated(true));
  }, []);

  const { email, setEmail, busy, uncertain, submit } = usePasswordRecovery();
  useLoadingProgress("Requesting a reset link", busy);
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
    <form method="post" onSubmit={handleSubmit} className={styles.form}>
      <noscript>
        <p className={styles.notice}>
          JavaScript is required to submit this form. Enable JavaScript and reload this page.
        </p>
      </noscript>
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
      <Button type="submit" disabled={!hydrated || busy} className={styles.submit}>
        {label}
      </Button>
    </form>
  );
}
