"use client";

import { useState } from "react";
import { useApiCommand } from "./use-api-command";
import { useInspectionLock } from "./use-inspection-lock";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ReservationDetail } from "@/lib/vehicles/reservation-detail";
import {
  matchesReservationCancellation,
  parseReservationCancellation,
  reservationCancellationReason,
} from "@/lib/vehicles/reservation-cancellation";

export function useReservationCancellation(
  detail: ReservationDetail | null,
  actor: CurrentActor,
  onConfirmed: () => void,
) {
  const [reason, setReason] = useState("");
  const [validationAttempted, setValidationAttempted] = useState(false);
  const toast = useToast();
  const command = useApiCommand(
    `/api/staff/reservations/${detail?.reservation.id}/cancellation`,
    parseReservationCancellation,
    (receipt, body) =>
      Boolean(
        detail &&
        matchesReservationCancellation(
          receipt,
          body,
          detail.reservation.id,
          actor.userId,
          detail.deposit,
        ),
      ),
    privateApiFetch,
  );
  const operating = command.busy || command.uncertain;
  const locked = Boolean(reason) || operating;
  const lockNavigation = useInspectionLock(locked);
  const actionable = detail?.reservation.status === "Active" && !command.receipt;

  function changeReason(value: string) {
    if (operating) {
      return;
    }
    setReason(value);
    lockNavigation(Boolean(value));
  }

  function discard() {
    if (operating) {
      return;
    }
    setReason("");
    setValidationAttempted(false);
    lockNavigation(false);
  }

  async function cancel() {
    if (command.busy || (!command.uncertain && !actionable)) {
      return;
    }
    const acceptedReason = reservationCancellationReason(reason);
    if (!command.uncertain && !acceptedReason) {
      setValidationAttempted(true);
      toast.error(
        "Enter a cancellation reason of at most 1000 characters without control characters.",
      );
      return;
    }
    lockNavigation(true);
    const result = await command.submit(
      command.uncertain ? null : { operationId: crypto.randomUUID(), reason: acceptedReason },
    );
    if (result) {
      lockNavigation(Boolean(reason));
    }
    if (result?.status === 200 && result.data) {
      setReason("");
      setValidationAttempted(false);
      lockNavigation(false);
      onConfirmed();
    }
  }

  return {
    reason,
    validationAttempted,
    command,
    operating,
    locked,
    actionable,
    changeReason,
    discard,
    cancel,
  };
}
