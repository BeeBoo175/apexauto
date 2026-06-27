"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, authHref, isRecord } from "@/lib/auth/contracts";
import { forgetSnapshots } from "@/lib/api-browser";
import { sessionChanges } from "@/lib/auth/browser-session";

export function useAuthMutation(kind: "login" | "register" | "logout", destination: string) {
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const attempt = useRef<string | null>(null);
  const inFlight = useRef(false);
  const controller = useRef<AbortController | null>(null);
  const { success, error, warning } = useToast();

  useEffect(() => {
    return () => controller.current?.abort();
  }, []);

  useEffect(() => {
    if (!busy && !uncertain) {
      return;
    }

    const preserveAttempt = (event: BeforeUnloadEvent) => {
      if (attempt.current) {
        event.preventDefault();
      }
    };
    window.addEventListener("beforeunload", preserveAttempt);
    return () => window.removeEventListener("beforeunload", preserveAttempt);
  }, [busy, uncertain]);

  async function submit(fields: Record<string, string> = {}) {
    if (inFlight.current) {
      return;
    }

    inFlight.current = true;
    setBusy(true);
    controller.current = new AbortController();

    if (!attempt.current) {
      const key = kind === "register" ? "registrationKey" : "loginKey";
      attempt.current = JSON.stringify({
        ...fields,
        ...(kind === "logout" ? {} : { [key]: crypto.randomUUID() }),
      });
    }

    try {
      // Signing in, out or changing the session must not leave the previous account's reads behind.
      forgetSnapshots();
      const response = await fetch(`/api/auth/${kind}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: attempt.current,
        signal: AbortSignal.any([
          controller.current.signal,
          AbortSignal.timeout(AUTH_DEADLINE_MS + 2000),
        ]),
      });
      const receipt: unknown = await response.json();

      if (response.ok && isRecord(receipt) && receipt.ok === true) {
        attempt.current = null;
        setUncertain(false);
        success(kind === "register" ? "Account created. Sign in to continue." : "Done.");
        if (kind !== "register") {
          sessionChanges?.postMessage("session-changed");
        }

        // A new document discards the previous actor's router snapshots after cookie changes.
        const next = kind === "login" ? authHref("/auth/continue", destination) : destination;
        window.location.assign(new URL(next, window.location.origin).href);
        return;
      }

      if (
        !isRecord(receipt) ||
        typeof receipt.message !== "string" ||
        response.ok ||
        receipt.uncertain === true ||
        response.status >= 500
      ) {
        throw new Error("Unconfirmed authentication response");
      }

      attempt.current = null;
      setUncertain(false);
      error(receipt.message);
    } catch {
      setUncertain(true);
      warning("The result is unconfirmed. Keep this page open and retry the same request.");
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  return { busy, uncertain, submit };
}
