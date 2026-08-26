"use client";

import { privateApiFetch } from "@/lib/api-browser";
import { parseDepositRefundReceipt, type BankAccountDetails } from "@/lib/vehicles/account-refunds";
import { useApiCommand } from "./use-api-command";

// One bank account submission for one refund. An unconfirmed result keeps the captured details and replays them, so a
// payout the provider may already have received is checked again rather than started a second time.
export function useDepositRefundRequest(paymentRequestId: string, onSettled: () => void) {
  const command = useApiCommand(
    `/api/account/reservation-refunds/${paymentRequestId}/bank-account`,
    parseDepositRefundReceipt,
    undefined,
    privateApiFetch,
  );

  async function submit(details: BankAccountDetails) {
    await command.submit(details);
    onSettled();
  }

  return { busy: command.busy, uncertain: command.uncertain, submit };
}
