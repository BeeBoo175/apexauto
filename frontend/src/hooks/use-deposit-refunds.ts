"use client";

import { useEffect, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import {
  parseDepositRefundList,
  parseDepositRefundReceipt,
  type BankAccountDetails,
  type DepositRefundList,
} from "@/lib/vehicles/account-refunds";

// The customer's deposits that are owed back, and the bank account they choose to receive each one. A refund still
// being paid out is read again on the next look, which also lets the API settle it from the provider's record.
export function useDepositRefunds(actorId: string) {
  const [list, setList] = useState<DepositRefundList | null>(null);
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [reads, setReads] = useState(0);
  const { success, error } = useToast();

  useEffect(() => {
    // Read in the browser when the section opens, after each request, and for another signed-in customer.
    const controller = new AbortController();
    async function load() {
      const result = await privateApiFetch(
        "/api/account/reservation-refunds",
        parseDepositRefundList,
        {
          signal: controller.signal,
        },
      );
      if (!controller.signal.aborted && result.status === 200 && result.data) {
        setList(result.data);
      }
    }

    void load();
    return () => controller.abort();
  }, [actorId, reads]);

  async function submit(paymentRequestId: string, details: BankAccountDetails) {
    if (submitting) {
      return;
    }

    setSubmitting(paymentRequestId);
    try {
      const result = await privateApiFetch(
        `/api/account/reservation-refunds/${paymentRequestId}/bank-account`,
        parseDepositRefundReceipt,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(details),
        },
      );
      if (result.status === 200 && result.data) {
        success("Your refund is on its way to your bank account.");
      } else {
        error(result.message || "The refund could not be requested. Try again.");
      }
    } finally {
      setSubmitting(null);
      setReads((count) => count + 1);
    }
  }

  return { list, submitting, submit };
}
