"use client";

import { useState } from "react";
import { useApiCommand } from "./use-api-command";
import { privateFetchFor } from "@/lib/staff/assisted-fetch";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { CustomerReservationRequest } from "@/lib/vehicles/customer-reservation";
import { matchesDepositRequest, parseDepositRequestReceipt } from "@/lib/vehicles/deposit-request";

// Sales staff ask the named customer to confirm the simulated deposit; the reservation is made when they do.
export function useDepositRequest(detail: PublicVehicleDetail, customerId: string) {
  const [confirmation, setConfirmation] = useState<CustomerReservationRequest | null>(null);
  const command = useApiCommand(
    `/api/vehicles/${detail.listing.id}/reservation-requests`,
    parseDepositRequestReceipt,
    (receipt, body) => matchesDepositRequest(receipt, body, detail.listing.id),
    privateFetchFor(customerId),
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

  async function send() {
    if (!confirmation || command.busy || command.receipt) {
      return;
    }
    const result = await command.submit(command.uncertain ? null : confirmation);
    if (result && !result.uncertain && result.status < 500) {
      setConfirmation(null);
    }
  }

  return { command, confirmation, locked, open, close, send };
}
