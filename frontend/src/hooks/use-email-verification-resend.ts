"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, isRecord, type CustomerAccount } from "@/lib/auth/contracts";
import { sessionChanges } from "@/lib/auth/browser-session";

export function useEmailVerificationResend(account: CustomerAccount | null) {
  const [status, setStatus] = useState({
    actorId: account?.userId ?? null,
    busy: false,
  });
  const { success, error, warning } = useToast();
  const body = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  const actorId = useRef<string | null>(account?.userId ?? null);

  useEffect(() => {
    const actorChanged = () => {
      controller.current?.abort();
      controller.current = null;
      body.current = null;
      inFlight.current = false;
      actorId.current = null;
      setStatus({ actorId: null, busy: false });
    };

    actorId.current = account?.userId ?? null;
    body.current = null;
    sessionChanges?.addEventListener("message", actorChanged);

    return () => {
      controller.current?.abort();
      controller.current = null;
      body.current = null;
      inFlight.current = false;
      actorId.current = null;
      sessionChanges?.removeEventListener("message", actorChanged);
    };
  }, [account?.userId]);

  async function resend() {
    if (inFlight.current || !account || account.emailVerified) {
      return;
    }

    inFlight.current = true;
    const attempt = new AbortController();
    const requestActor = account.userId;
    setStatus({ actorId: requestActor, busy: true });
    controller.current = attempt;
    body.current ??= JSON.stringify({ resendKey: crypto.randomUUID() });
    const isCurrent = () =>
      controller.current === attempt && actorId.current === requestActor && !attempt.signal.aborted;

    try {
      const response = await fetch("/api/auth/email-verification/resend", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: body.current,
        signal: AbortSignal.any([attempt.signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
      });
      const receipt: unknown = await response.json();

      if (!isCurrent()) {
        return;
      }

      if (response.ok && isRecord(receipt) && receipt.ok === true) {
        body.current = null;
        success("If verification is still needed, an email is queued. Check your inbox.");
        return;
      }

      if (
        !response.ok &&
        isRecord(receipt) &&
        typeof receipt.message === "string" &&
        receipt.uncertain !== true &&
        response.status < 500
      ) {
        error(receipt.message);
        return;
      }

      throw new Error("Unconfirmed resend response");
    } catch {
      if (isCurrent()) {
        warning("The result is unconfirmed. Retry the same request when the service is available.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        inFlight.current = false;
        setStatus((current) => ({ ...current, busy: false }));
      }
    }
  }

  const currentStatus = status.actorId === account?.userId ? status : null;
  return { busy: currentStatus?.busy ?? false, resend };
}
