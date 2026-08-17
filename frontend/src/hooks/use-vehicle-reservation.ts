"use client";

import { useState } from "react";
import { useApiCommand } from "./use-api-command";
import { privateFetchFor } from "@/lib/staff/assisted-fetch";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { CustomerAccount } from "@/lib/auth/contracts";
import {
  isReservationPayment,
  matchesCustomerReservation,
  parseCustomerReservationOutcome,
  type CustomerReservationRequest,
} from "@/lib/vehicles/customer-reservation";

// actingFor names the customer when sales staff reserve on their behalf; the account then carries masked contact.
export function useVehicleReservation(
  detail: PublicVehicleDetail,
  account: CustomerAccount,
  actingFor?: string,
) {
  const [confirmation, setConfirmation] = useState<CustomerReservationRequest | null>(null);
  const command = useApiCommand(
    `/api/vehicles/${detail.listing.id}/reservations`,
    parseCustomerReservationOutcome,
    (outcome, body) =>
      isReservationPayment(outcome) ||
      matchesCustomerReservation(
        outcome,
        body,
        detail.listing.id,
        detail.listing.branchId,
        actingFor ? null : account.email,
      ),
    privateFetchFor(actingFor),
  );
  const locked = command.busy || command.uncertain;

  function open(promoId: string | null) {
    if (!locked && !command.receipt) {
      setConfirmation({
        operationId: crypto.randomUUID(),
        publicationId: detail.publicationId,
        promoId,
      });
    }
  }

  function close() {
    if (!locked) {
      setConfirmation(null);
    }
  }

  async function reserve() {
    if (!confirmation || command.busy || command.receipt) {
      return;
    }
    const result = await command.submit(command.uncertain ? null : confirmation);
    if (result?.data && isReservationPayment(result.data)) {
      // The customer pays on the provider's page and returns to the confirm page with the signed result.
      window.location.assign(result.data.paymentUrl);
      return;
    }
    if (result && !result.uncertain && result.status < 500) {
      setConfirmation(null);
    }
  }

  // A payment address is not a reservation: until the payment is confirmed there is no receipt to show.
  const outcome = command.receipt;
  const receipt = outcome && !isReservationPayment(outcome) ? outcome : null;

  return { command: { ...command, receipt }, confirmation, locked, open, close, reserve };
}
