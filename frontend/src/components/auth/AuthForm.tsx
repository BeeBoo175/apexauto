"use client";

import type { FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useAuthMutation } from "@/hooks/use-auth-mutation";

export function AuthForm({
  kind,
  destination,
}: {
  kind: "login" | "register";
  destination: string;
}) {
  const { busy, uncertain, submit } = useAuthMutation(kind, destination);
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

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-5">
      <fieldset disabled={busy || uncertain} className="flex flex-col gap-5">
        {kind === "register" && (
          <Input label="Legal name" name="legalName" autoComplete="name" required />
        )}
        <Input label="Email" name="email" type="email" autoComplete="username" required />
        <Input
          label="Password"
          name="password"
          type="password"
          autoComplete={kind === "register" ? "new-password" : "current-password"}
          required
        />
        {kind === "register" && (
          <>
            <p className="text-sm text-muted">
              Use at least 8 characters with uppercase, lowercase, a digit and a symbol.
            </p>
            <Input
              label="NRIC"
              name="nric"
              inputMode="numeric"
              pattern="[0-9]{12}"
              maxLength={12}
              required
            />
            <Input label="Mobile" name="mobile" type="tel" autoComplete="tel" required />
            <Input
              label="Residential address"
              name="residentialAddress"
              autoComplete="street-address"
              required
            />
          </>
        )}
      </fieldset>
      <Button type="submit" disabled={busy}>
        {submitLabel}
      </Button>
    </form>
  );
}
