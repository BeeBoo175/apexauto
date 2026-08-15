"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, isRecord } from "@/lib/auth/contracts";
import { writeAddress } from "@/lib/browser-url";

// The link in the deposit request mail carries its token in the address fragment, as the email verification link
// does, so it never reaches a server log. Choosing Confirm deposit posts it once; a repeat replays the same result.
export function useReservationDepositConfirm() {
  const [token, setToken] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const { success, error, warning } = useToast();
  const router = useRouter();
  const controller = useRef<AbortController | null>(null);
  const inFlight = useRef(false);

  useEffect(() => {
    const readFragment = () => {
      const nextToken = new URLSearchParams(window.location.hash.slice(1)).get("token");
      setToken(nextToken);
      if (!nextToken) {
        error("This deposit link is missing its token.");
      }
    };

    queueMicrotask(readFragment);
    window.addEventListener("hashchange", readFragment);
    return () => {
      controller.current?.abort();
      window.removeEventListener("hashchange", readFragment);
    };
  }, [error]);

  async function confirm() {
    if (!token || inFlight.current) {
      return;
    }

    inFlight.current = true;
    setBusy(true);
    const attempt = new AbortController();
    controller.current = attempt;

    try {
      const response = await fetch("/api/reservation-deposits/confirmations", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token }),
        signal: AbortSignal.any([attempt.signal, AbortSignal.timeout(AUTH_DEADLINE_MS + 2000)]),
      });
      const receipt: unknown = await response.json();

      if (response.ok && isRecord(receipt) && receipt.ok === true) {
        writeAddress("/confirm-deposit");
        success("Deposit confirmed. Your reservation receipt is on its way.");
        router.push("/account");
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

      throw new Error("Unconfirmed deposit response");
    } catch {
      if (!attempt.signal.aborted) {
        warning("The result is unconfirmed. Keep this page open and retry the same link.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        inFlight.current = false;
        setBusy(false);
      }
    }
  }

  return { token, busy, confirm };
}
