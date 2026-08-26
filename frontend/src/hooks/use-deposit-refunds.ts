"use client";

import { useEffect, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import { parseDepositRefundList, type DepositRefundList } from "@/lib/vehicles/account-refunds";

// The customer's deposits that are owed back. A refund still being paid out is read again on the next look, which
// also lets the API settle it from the provider's record. A failed read is reported, never shown as nothing owed.
export function useDepositRefunds(actorId: string) {
  const [list, setList] = useState<DepositRefundList | null>(null);
  const [failed, setFailed] = useState(false);
  const [reads, setReads] = useState(0);

  useEffect(() => {
    // Read in the browser when the section opens, after each request, and for another signed-in customer.
    const controller = new AbortController();
    async function load() {
      try {
        const result = await privateApiFetch(
          "/api/account/reservation-refunds",
          parseDepositRefundList,
          {
            signal: controller.signal,
          },
        );
        if (controller.signal.aborted) {
          return;
        }

        if (result.status === 200 && result.data) {
          setList(result.data);
          setFailed(false);
        } else {
          setFailed(true);
        }
      } catch {
        // Leaving the page, or a repeated development effect, aborts the read; the list simply stays as it was.
      }
    }

    void load();
    return () => controller.abort();
  }, [actorId, reads]);

  function reload() {
    setReads((count) => count + 1);
  }

  return { list, failed, reload };
}
