"use client";

import type { FormEvent } from "react";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Input } from "@/components/ui/Input";
import { demoDetails } from "@/lib/demo/demo-details";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { useToast } from "@/components/ui/Toast";
import { DemoAccounts } from "@/components/auth/DemoAccounts";
import type { DemoAccount } from "@/components/auth/DemoAccounts";
import styles from "./AuthForm.module.css";
import { useLoadingProgress } from "@/components/ui/LoadingProgress";

const demoFieldNames = ["legalName", "nric", "mobile", "residentialAddress"];

function isInput(element: unknown): element is HTMLInputElement {
  return element instanceof HTMLInputElement;
}

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
  useLoadingProgress(kind === "login" ? "Signing in" : "Creating your account", busy);
  const form = useRef<HTMLFormElement>(null);
  const [confirmingFill, setConfirmingFill] = useState(false);
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

  function demoFields() {
    return demoFieldNames.map((name) => form.current?.elements.namedItem(name)).filter(isInput);
  }

  function requestDemoDetails() {
    if (demoFields().some((field) => field.value.trim() !== "")) {
      setConfirmingFill(true);
      return;
    }

    fillDemoDetails();
  }

  function fillDemoDetails() {
    const details = demoDetails();

    for (const field of demoFields()) {
      field.value = details[field.name as keyof typeof details];
    }

    setConfirmingFill(false);
    info(
      "Fictional details filled. Review them, then enter your own email, username and password.",
    );
  }

  function fillDemoAccount(account: DemoAccount) {
    if (busy || uncertain || !emailInput.current || !passwordInput.current) {
      return;
    }

    emailInput.current.value = account.email;
    passwordInput.current.value = account.password;
    // The accounts sit below the form, so return to the filled fields and the Sign in button.
    submitButton.current?.focus({ preventScroll: true });
    window.scrollTo({ top: 0, behavior: "smooth" });
    info("Sample details filled. Sign in to continue.");
  }

  return (
    <form ref={form} method="post" onSubmit={handleSubmit} className={styles.form}>
      <noscript>
        <p className={styles.help}>Enable JavaScript and reload this page to continue.</p>
      </noscript>
      <fieldset disabled={busy || uncertain} className={styles.fields}>
        {kind === "register" && (
          <div className={styles.group} role="group" aria-labelledby="register-about">
            <p id="register-about" className={styles.groupTitle}>
              About you
            </p>
            <div className={styles.pairedFields}>
              <Input label="Legal name" name="legalName" autoComplete="name" required />
              <Input
                label="NRIC"
                name="nric"
                inputMode="numeric"
                pattern="[0-9]{12}"
                maxLength={12}
                required
              />
            </div>
            <Input
              label="Residential address"
              name="residentialAddress"
              autoComplete="street-address"
              required
            />
            <div className={styles.demoFill}>
              <Button type="button" variant="secondary" size="compact" onClick={requestDemoDetails}>
                Fill demo details
              </Button>
              <p className={styles.help}>Fills fictional name, NRIC, mobile and address.</p>
            </div>
          </div>
        )}
        <div
          className={styles.group}
          role="group"
          aria-labelledby={kind === "register" ? "register-sign-in" : undefined}
        >
          {kind === "register" && (
            <p id="register-sign-in" className={styles.groupTitle}>
              Sign in details
            </p>
          )}
          <div className={kind === "register" ? styles.pairedFields : styles.fields}>
            <Input
              ref={emailInput}
              label="Email"
              name="email"
              type="email"
              autoComplete="username"
              required
            />
            {kind === "register" && (
              <Input label="Mobile" name="mobile" type="tel" autoComplete="tel" required />
            )}
          </div>
          {kind === "register" && (
            <div className={styles.password}>
              <Input
                label="Username"
                name="username"
                autoComplete="nickname"
                pattern="[A-Za-z0-9._]{3,30}"
                maxLength={30}
                aria-describedby="username-help"
                required
              />
              <p id="username-help" className={styles.help}>
                3 to 30 letters, numbers, dots or underscores. Shown to staff; you still sign in
                with your email.
              </p>
            </div>
          )}
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
        </div>
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
        <fieldset disabled={busy || uncertain} className={styles.demo} aria-label="Sample accounts">
          <DemoAccounts accounts={demoAccounts} ready={hydrated} onSelect={fillDemoAccount} />
        </fieldset>
      )}
      <ConfirmationDialog
        open={confirmingFill}
        title="Replace your details?"
        confirmLabel="Replace details"
        cancelLabel="Keep my details"
        onClose={() => setConfirmingFill(false)}
        onConfirm={fillDemoDetails}
      >
        Name, NRIC, mobile and address already contain text. Filling demo details replaces it.
      </ConfirmationDialog>
    </form>
  );
}
