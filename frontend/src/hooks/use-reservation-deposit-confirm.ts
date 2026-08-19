"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { AUTH_DEADLINE_MS, isRecord, isUuid } from "@/lib/auth/contracts";
import { writeAddress } from "@/lib/browser-url";

// The provider's own fields in the return address, whatever it names them, without our request id, which is not
// part of what it signs. The API's gateway for the configured provider decides what they mean.
function providerFields(search: URLSearchParams) {
  const fields: Record<string, string> = {};
  for (const [name, value] of search) {
    if (name !== "request") {
      fields[name] = value;
    }
  }

  return fields;
}

// The link in the deposit request mail carries its token in the address fragment, as the email verification link
// does, so it never reaches a server log. Choosing Confirm deposit posts it once; a repeat replays the same result.
// With the payment gateway on, that choice opens the hosted payment page and the customer returns here with the
// provider's signed fields, which the API verifies to confirm the deposit at once.
export function useReservationDepositConfirm() {
  const [token, setToken] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const { success, error, warning } = useToast();
  const controller = useRef<AbortController | null>(null);
  const inFlight = useRef(false);
  // The signed return is sent once per page. Development runs every effect twice, and the first run's cleanup must not
  // cancel the one request that this guard lets through.
  const sent = useRef<string | null>(null);

  const finish = useCallback(() => {
    writeAddress("/confirm-deposit");
    success("Deposit confirmed. The reservation receipt is on its way.");
    // Stay here: a signed-in staff member opening the link would otherwise land on their own account.
    setConfirmed(true);
  }, [success]);

  const settle = useCallback(
    (status: string) => {
      if (status === "confirmed") {
        finish();
      } else if (status === "lapsed") {
        error(
          "The deposit link expired and the hold was released. Ask our team to send a new one.",
        );
      } else if (status === "conflict") {
        error(
          "Your payment could not be matched to a reservation. Contact our team and quote this link.",
        );
      } else {
        warning("The payment was not completed. Open the link in your email to try again.");
      }
    },
    [error, finish, warning],
  );

  const returnFromPayment = useCallback(
    async (search: URLSearchParams) => {
      const fields = providerFields(search);
      const requestId = search.get("request");
      const hasFields = Object.keys(fields).length > 0;
      const target = hasFields
        ? {
            path: "/api/reservation-deposits/returns",
            init: {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ fields }),
            },
          }
        : { path: `/api/reservation-deposits/${requestId}/status`, init: { method: "GET" } };

      setBusy(true);
      try {
        const response = await fetch(target.path, {
          ...target.init,
          cache: "no-store",
          signal: AbortSignal.timeout(AUTH_DEADLINE_MS + 2000),
        });
        const body: unknown = await response.json();
        if (response.ok && isRecord(body) && typeof body.status === "string") {
          settle(body.status);
          return;
        }

        error(
          isRecord(body) && typeof body.message === "string"
            ? body.message
            : "The payment result could not be read.",
        );
      } catch {
        sent.current = null;
        warning("The payment result is unconfirmed. Reload this page to check again.");
      } finally {
        setBusy(false);
      }
    },
    [error, settle, warning],
  );

  useEffect(() => {
    const readFragment = () => {
      const nextToken = new URLSearchParams(window.location.hash.slice(1)).get("token");
      setToken(nextToken);
      const search = new URLSearchParams(window.location.search);
      const returned = search.get("request");
      if (returned && isUuid(returned)) {
        if (sent.current !== search.toString()) {
          sent.current = search.toString();
          void returnFromPayment(search);
        }
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
      window.removeEventListener("hashchange", readFragment);
    };
  }, [error, returnFromPayment]);

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

  return { token, busy, confirmed, confirm };
}
