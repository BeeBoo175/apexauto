"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { sessionChanges } from "@/lib/auth/browser-session";
import { AUTH_DEADLINE_MS, isRecord } from "@/lib/auth/contracts";

export function usePasswordReset() {
  const [token, setToken] = useState<string | null>(null);
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [message, setMessage] = useState("Open the reset link from your email to continue.");
  const [passwordError, setPasswordError] = useState("");
  const activeToken = useRef<string | null>(null);
  const attempt = useRef<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const { success, error, warning } = useToast();

  useEffect(() => {
    let active = true;

    function readFragment() {
      if (!active) {
        return;
      }

      const values = new URLSearchParams(window.location.hash.slice(1)).getAll("token");
      const nextToken =
        values.length === 1 && values[0].length > 0 && values[0].length <= 160 ? values[0] : null;

      if (activeToken.current === nextToken) {
        return;
      }

      controller.current?.abort();
      controller.current = null;
      activeToken.current = nextToken;
      attempt.current = null;
      setToken(nextToken);
      setPassword("");
      setBusy(false);
      setUncertain(false);
      setPasswordError("");
      setMessage(nextToken ? "" : "The reset link is missing or invalid. Request a new link.");
    }

    queueMicrotask(readFragment);
    window.addEventListener("hashchange", readFragment);
    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
      activeToken.current = null;
      attempt.current = null;
      window.removeEventListener("hashchange", readFragment);
    };
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
    if (!token || controller.current) {
      return;
    }

    attempt.current ??= JSON.stringify({ token, password });
    const requestToken = token;
    const request = new AbortController();
    controller.current = request;
    setBusy(true);
    setPasswordError("");
    setMessage("");
    const isCurrent = () =>
      controller.current === request &&
      activeToken.current === requestToken &&
      !request.signal.aborted;

    try {
      const response = await fetch("/api/auth/password-reset", {
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
        activeToken.current = null;
        setToken(null);
        setPassword("");
        setUncertain(false);
        window.history.replaceState(null, "", "/reset-password");
        sessionChanges?.postMessage("session-changed");
        success("Password reset. Sign in with your new password.");
        window.location.assign(new URL("/login", window.location.origin).href);
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
        setPasswordError(typeof receipt.passwordError === "string" ? receipt.passwordError : "");
        error(receipt.message);
        return;
      }

      throw new Error("Unconfirmed password reset response");
    } catch {
      if (isCurrent()) {
        setUncertain(true);
        setMessage(
          "The result is unconfirmed. Keep this page open and retry the same link and password before the link expires.",
        );
        warning("The password reset could not be confirmed.");
      }
    } finally {
      if (controller.current === request) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  return { token, password, setPassword, busy, uncertain, message, passwordError, submit };
}
