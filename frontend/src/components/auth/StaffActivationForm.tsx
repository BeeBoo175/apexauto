"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useStaffActivation } from "@/hooks/use-staff-activation";
import { roleNames, type Role } from "@/lib/auth/actor";
import styles from "./AuthForm.module.css";

export function StaffActivationForm() {
  const [hydrated, setHydrated] = useState(false);
  const activation = useStaffActivation();
  const { preview } = activation;

  useEffect(() => {
    queueMicrotask(() => setHydrated(true));
  }, []);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void activation.submit();
  }

  if (preview.state === "loading") {
    return <p className={styles.help}>Opening your invitation.</p>;
  }

  if (preview.state === "unavailable") {
    return (
      <p className={styles.help} role="alert">
        {preview.message}
      </p>
    );
  }

  const { invitation } = preview;
  const role = roleNames[invitation.role as Role] ?? invitation.role;

  return (
    <form method="post" onSubmit={handleSubmit} className={styles.form}>
      <noscript>
        <p className={styles.notice}>
          JavaScript is required to submit this form. Enable JavaScript and reload this page.
        </p>
      </noscript>
      <p className={styles.help}>
        {invitation.name}, you are invited as {role}
        {invitation.branchName ? ` at ${invitation.branchName}` : ""}. Opening this page changes
        nothing. You sign in with your email and the password you choose here.
      </p>
      <Input
        id="activation-username"
        label="Username"
        name="username"
        autoComplete="username"
        maxLength={30}
        value={activation.username}
        onChange={(event) => activation.setUsername(event.target.value)}
        disabled={!hydrated || activation.busy}
        required
      />
      <div className={styles.password}>
        <Input
          id="activation-password"
          label="Password"
          name="password"
          type="password"
          autoComplete="new-password"
          maxLength={128}
          value={activation.password}
          onChange={(event) => activation.setPassword(event.target.value)}
          disabled={!hydrated || activation.busy}
          aria-describedby="activation-help"
          required
        />
        <p id="activation-help" className={styles.notice}>
          Use at least 8 characters with uppercase, lowercase, a digit and a symbol. Usernames use 3
          to 30 letters, numbers, dots or underscores.
        </p>
      </div>
      {activation.problem && (
        <p className={styles.notice} role="alert">
          {activation.problem}
        </p>
      )}
      <Button type="submit" disabled={!hydrated || activation.busy} className={styles.submit}>
        {activation.busy ? "Please wait" : "Activate account"}
      </Button>
    </form>
  );
}
