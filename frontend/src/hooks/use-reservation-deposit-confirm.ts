"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, isRecord, isUuid } from "@/lib/auth/contracts";
import { writeAddress } from "@/lib/browser-url";

const STATUS_POLL_MS = 2500;
const STATUS_POLL_LIMIT = 24;

// The token is a request id in hex followed by its signature; the id alone names the request for status reads.
function requestIdFromToken(token: string) {
  const hex = token.slice(0, 32);
  if (!/^[0-9a-f]{32}$/.test(hex)) {
    return null;
  }

  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

// The link in the deposit request mail carries its token in the address fragment, as the email verification link
// does, so it never reaches a server log. Choosing Confirm deposit posts it once; a repeat replays the same result.
// With the payment gateway on, that choice opens the hosted payment page, and the customer returns here with only
// the request id, whose stored state is read until the signed payment callback has made the reservation.
export function useReservationDepositConfirm() {
  const [token, setToken] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [waiting, setWaiting] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const { success, error, warning } = useToast();
  const controller = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  const polling = useRef(false);

  const finish = useCallback(() => {
    writeAddress("/confirm-deposit");
    success("Deposit confirmed. The reservation receipt is on its way.");
    // Stay here: a signed-in staff member opening the link would otherwise land on their own account.
    setConfirmed(true);
  }, [success]);

  const watchPayment = useCallback(
    async (requestId: string, signal: AbortSignal) => {
      if (polling.current) {
        return;
      }

      polling.current = true;
      setWaiting(true);
      try {
        for (let attempt = 0; attempt < STATUS_POLL_LIMIT && !signal.aborted; attempt += 1) {
          const response = await fetch(`/api/reservation-deposits/${requestId}/status`, {
            cache: "no-store",
            signal: AbortSignal.any([signal, AbortSignal.timeout(AUTH_DEADLINE_MS)]),
          }).catch(() => null);
          const body: unknown = response?.ok ? await response.json().catch(() => null) : null;
          const status = isRecord(body) && typeof body.status === "string" ? body.status : null;

          if (status === "confirmed") {
            finish();
            return;
          }

          if (status === "lapsed") {
            error(
              "The deposit link expired and the hold was released. Ask our team to send a new one.",
            );
            return;
          }

          if (status === "conflict") {
            error(
              "Your payment could not be matched to a reservation. Contact our team and quote this link.",
            );
            return;
          }

          await new Promise((resolve) => setTimeout(resolve, STATUS_POLL_MS));
        }

        if (!signal.aborted) {
          warning(
            "We are still waiting for the payment confirmation. Keep this page open or check your email.",
          );
        }
      } finally {
        polling.current = false;
        setWaiting(false);
      }
    },
    [error, finish, warning],
  );

  useEffect(() => {
    const watcher = new AbortController();
    const readFragment = () => {
      const nextToken = new URLSearchParams(window.location.hash.slice(1)).get("token");
      setToken(nextToken);
      const returned = new URLSearchParams(window.location.search).get("request");
      if (returned && isUuid(returned)) {
        void watchPayment(returned, watcher.signal);
        return;
      }

      if (!nextToken) {
        error("This deposit link is missing its token.");
      }
    };

    queueMicrotask(readFragment);
    window.addEventListener("hashchange", readFragment);
    return () => {
      controller.current?.abort();
      watcher.abort();
      window.removeEventListener("hashchange", readFragment);
    };
  }, [error, watchPayment]);

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
        finish();
        return;
      }

      if (response.ok && isRecord(receipt) && typeof receipt.paymentUrl === "string") {
        // The payment page is the provider's; the customer returns to this page when they are done.
        window.location.assign(receipt.paymentUrl);
        return;
      }

      if (response.status === 202 && isRecord(receipt) && receipt.waiting === true) {
        const requestId = requestIdFromToken(token);
        if (requestId) {
          void watchPayment(requestId, attempt.signal);
        }
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

  return { token, busy, waiting, confirmed, confirm };
}
