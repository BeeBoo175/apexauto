"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, isRecord } from "@/lib/auth/contracts";

export function usePasswordRecovery() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [message, setMessage] = useState("");
  const attempt = useRef<string | null>(null);
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

  async function submit() {
    if (controller.current) {
      return;
    }

    attempt.current ??= JSON.stringify({ email });
    const request = new AbortController();
    controller.current = request;
    setBusy(true);
    setMessage("");
    const isCurrent = () => controller.current === request && !request.signal.aborted;

    try {
      const response = await fetch("/api/auth/password-recovery", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: attempt.current,
        signal: AbortSignal.any([request.signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
      });
      const receipt: unknown = await response.json();

      if (!isCurrent()) {
        return;
      }

      if (response.ok && isRecord(receipt) && receipt.ok === true) {
        attempt.current = null;
        setUncertain(false);
        setMessage(
          "If this account can receive a reset email, a link will be sent. Check your inbox; requests may be limited.",
        );
        success("Password reset request accepted.");
        return;
      }

      if (
        !response.ok &&
        isRecord(receipt) &&
        typeof receipt.message === "string" &&
        receipt.uncertain !== true &&
        response.status < 500
      ) {
        attempt.current = null;
        setUncertain(false);
        setMessage(receipt.message);
        error(receipt.message);
        return;
      }

      throw new Error("Unconfirmed recovery response");
    } catch {
      if (isCurrent()) {
        setUncertain(true);
        setMessage(
          "The result is unconfirmed. Keep this page open and check your inbox, or retry the same email. A retry may request another link; use the latest email.",
        );
        warning("The password reset request could not be confirmed.");
      }
    } finally {
      if (controller.current === request) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  return { email, setEmail, busy, uncertain, message, submit };
}
