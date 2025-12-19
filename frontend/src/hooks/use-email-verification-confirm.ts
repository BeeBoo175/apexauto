"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AUTH_DEADLINE_MS, isRecord } from "@/lib/auth/contracts";

export function useEmailVerificationConfirm() {
  const [token, setToken] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const router = useRouter();
  const controller = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  const activeToken = useRef<string | null>(null);

  useEffect(() => {
    let active = true;
    const readFragment = () => {
      if (!active) {
        return;
      }

      const fragment = new URLSearchParams(window.location.hash.slice(1));
      const nextToken = fragment.get("token");

      if (activeToken.current === nextToken) {
        return;
      }

      controller.current?.abort();
      controller.current = null;
      inFlight.current = false;
      activeToken.current = nextToken;
      setToken(nextToken);
      setBusy(false);
      setMessage(null);
    };

    queueMicrotask(readFragment);
    window.addEventListener("hashchange", readFragment);

    return () => {
      active = false;
      controller.current?.abort();
      controller.current = null;
      activeToken.current = null;
      window.removeEventListener("hashchange", readFragment);
    };
  }, []);

  async function confirm() {
    if (!token || inFlight.current) {
      return;
    }

    inFlight.current = true;
    setBusy(true);
    setMessage(null);
    const attempt = new AbortController();
    controller.current = attempt;
    const requestToken = token;
    const isCurrent = () =>
      controller.current === attempt &&
      activeToken.current === requestToken &&
      !attempt.signal.aborted;

    try {
      const response = await fetch("/api/auth/email-verification", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token: requestToken }),
        signal: AbortSignal.any([attempt.signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
      });
      const receipt: unknown = await response.json();

      if (!isCurrent()) {
        return;
      }

      if (response.ok && isRecord(receipt) && receipt.ok === true) {
        window.history.replaceState(null, "", "/verify-email");
        router.push("/login");
        return;
      }

      if (
        !response.ok &&
        isRecord(receipt) &&
        typeof receipt.message === "string" &&
        receipt.uncertain !== true &&
        response.status < 500
      ) {
        setMessage(receipt.message);
        return;
      }

      throw new Error("Unconfirmed verification response");
    } catch {
      if (isCurrent()) {
        setMessage("The result is unconfirmed. Keep this page open and retry the same link.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        inFlight.current = false;
        setBusy(false);
      }
    }
  }

  return { token, busy, message, confirm };
}
