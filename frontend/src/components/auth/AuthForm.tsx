"use client";

import type { FormEvent } from "react";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { useToast } from "@/components/ui/Toast";
import { DemoAccounts } from "@/components/auth/DemoAccounts";
import type { DemoAccount } from "@/components/auth/DemoAccounts";
import styles from "./AuthForm.module.css";

export function AuthForm({
  kind,
  destination,
  demoAccounts,
}: {
  kind: "login" | "register";
  destination: string;
  demoAccounts?: readonly DemoAccount[];
}) {
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    queueMicrotask(() => setHydrated(true));
  }, []);

  const { busy, uncertain, submit } = useAuthMutation(kind, destination);
  const emailInput = useRef<HTMLInputElement>(null);
  const passwordInput = useRef<HTMLInputElement>(null);
  const submitButton = useRef<HTMLButtonElement>(null);
  const { info } = useToast();
  let submitLabel = kind === "register" ? "Create account" : "Sign in";

  if (uncertain) {
    submitLabel = "Retry same request";
  }

  if (busy) {
    submitLabel = "Please wait";
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const fields = Object.fromEntries(new FormData(event.currentTarget).entries());
    const strings: Record<string, string> = {};

    for (const [name, value] of Object.entries(fields)) {
      if (typeof value === "string") {
        strings[name] = value;
      }
    }

    void submit(strings);
  }

  function fillDemoAccount(account: DemoAccount) {
    if (busy || uncertain || !emailInput.current || !passwordInput.current) {
      return;
    }

    emailInput.current.value = account.email;
    passwordInput.current.value = account.password;
    submitButton.current?.focus();
    info("Demo details filled. Sign in to continue.");
  }

  return (
    <form method="post" onSubmit={handleSubmit} className={styles.form}>
      <noscript>
        <p className={styles.help}>Enable JavaScript and reload this page to continue.</p>
      </noscript>
      <fieldset disabled={!hydrated || busy || uncertain} className={styles.fields}>
        {kind === "register" && (
          <Input label="Legal name" name="legalName" autoComplete="name" required />
        )}
        <Input
          ref={emailInput}
          label="Email"
          name="email"
          type="email"
          autoComplete="username"
          required
        />
        <div className={styles.password}>
          <Input
            ref={passwordInput}
            label="Password"
            name="password"
            type="password"
            autoComplete={kind === "register" ? "new-password" : "current-password"}
            aria-describedby={kind === "register" ? "password-help" : undefined}
            required
          />
          {kind === "register" && (
            <p id="password-help" className={styles.help}>
              8+ characters with uppercase, lowercase, a number and a symbol.
            </p>
          )}
        </div>
        {kind === "register" && (
          <>
            <div className={styles.pairedFields}>
              <Input
                label="NRIC"
                name="nric"
                inputMode="numeric"
                pattern="[0-9]{12}"
                maxLength={12}
                required
              />
              <Input label="Mobile" name="mobile" type="tel" autoComplete="tel" required />
            </div>
            <Input
              label="Residential address"
              name="residentialAddress"
              autoComplete="street-address"
              required
            />
          </>
        )}
      </fieldset>
      <div className={styles.actions}>
        <Button
          ref={submitButton}
          type="submit"
          disabled={!hydrated || busy}
          className={styles.submit}
        >
          {submitLabel}
        </Button>
      </div>
      {kind === "login" && (
        <div className={styles.secondaryLinks}>
          <a href="/register" className={styles.accountLink}>
            Create an account
          </a>
          <a className={styles.recoveryLink} href="/forgot-password">
            Forgot password?
          </a>
        </div>
      )}
      {kind === "login" && demoAccounts && (
        <fieldset
          disabled={!hydrated || busy || uncertain}
          className={styles.demo}
          aria-label="Demo accounts"
        >
          <DemoAccounts accounts={demoAccounts} onSelect={fillDemoAccount} />
        </fieldset>
      )}
    </form>
  );
}
