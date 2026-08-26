"use client";

import { useEffect, useState } from "react";

// Asked once per page load and shared: every deposit wording on the page reads the same answer. Only a successful
// answer is kept, so one failed read is asked again instead of wording every deposit as simulated until a reload.
let answer: Promise<boolean> | null = null;

function readMode() {
  answer ??= fetch("/api/payments/mode", { cache: "no-store" })
    .then((response) =>
      response.ok ? response.json() : Promise.reject(new Error("Payment mode unavailable")),
    )
    .then(
      (body: unknown) =>
        typeof body === "object" && body !== null && "live" in body && body.live === true,
    )
    .catch(() => {
      answer = null;
      return false;
    });
  return answer;
}

// True when deposits are paid through the payment provider, false while that is unknown or when they are simulated.
export function usePaymentMode() {
  const [live, setLive] = useState(false);
  useEffect(() => {
    let current = true;
    void readMode().then((value) => {
      if (current) {
        setLive(value);
      }
    });
    return () => {
      current = false;
    };
  }, []);
  return live;
}
