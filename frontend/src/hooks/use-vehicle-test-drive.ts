"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { privateFetchFor } from "@/lib/staff/assisted-fetch";
import { calendarDate, isCalendarDate } from "@/lib/calendar";
import type { CustomerAccount } from "@/lib/auth/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import {
  matchesTestDriveBooking,
  parseTestDriveBooking,
  parseTestDriveRequest,
  parseTestDriveSlots,
} from "@/lib/test-drives/customer";
import { useApiCommand } from "./use-api-command";

type Offer = NonNullable<ReturnType<typeof parseTestDriveSlots>>;
type Request = NonNullable<ReturnType<typeof parseTestDriveRequest>>;

export function useVehicleTestDrive(
  detail: PublicVehicleDetail,
  account: CustomerAccount,
  actingFor?: string,
) {
  const [open, setOpen] = useState(false);
  const [date, setDate] = useState(calendarDate);
  const [offer, setOffer] = useState<Offer | null>(null);
  const [startsAt, setStartsAt] = useState("");
  const [confirmation, setConfirmation] = useState<Request | null>(null);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  const command = useApiCommand(
    `/api/vehicles/${detail.listing.id}/test-drives`,
    parseTestDriveBooking,
    (receipt, body) => {
      const request = parseTestDriveRequest(body);
      return Boolean(
        request &&
        receipt.branchId === detail.listing.branchId &&
        matchesTestDriveBooking(receipt, request, detail.listing.id, account.userId),
      );
    },
    privateFetchFor(actingFor),
  );
  const locked = command.busy || command.uncertain;
  useEffect(() => () => controller.current?.abort(), []);

  async function load(nextDate = date) {
    if (locked || confirmation || command.receipt || !isCalendarDate(nextDate)) {
      return;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setDate(nextDate);
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/vehicles/${detail.listing.id}/test-drive-slots?date=${nextDate}`,
        parseTestDriveSlots,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (
        result.status !== 200 ||
        !result.data ||
        result.data.date !== nextDate ||
        result.data.vehicleId !== detail.listing.id ||
        result.data.branchId !== detail.listing.branchId ||
        result.data.publicationId !== detail.publicationId
      ) {
        throw new Error("Slots unavailable");
      }
      const confirmedOffer = result.data;
      setOffer(confirmedOffer);
      setStartsAt((current) =>
        confirmedOffer.slots.some((slot) => slot.startsAt === current) ? current : "",
      );
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Test-drive times could not be refreshed. Reload the page to try again.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function show() {
    setOpen(true);
    void load();
  }

  function close() {
    if (locked) {
      return;
    }
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
    setOpen(false);
  }

  function confirm() {
    if (
      busy ||
      locked ||
      !offer ||
      offer.date !== date ||
      !offer.slots.some((slot) => slot.startsAt === startsAt)
    ) {
      return;
    }
    setConfirmation({
      operationId: crypto.randomUUID(),
      publicationId: offer.publicationId,
      startsAt,
    });
  }

  async function book() {
    if (!confirmation || command.busy || command.receipt) {
      return;
    }
    const result = await command.submit(command.uncertain ? null : confirmation);
    if (!result || result.uncertain || result.status >= 500) {
      return;
    }
    setConfirmation(null);
    if (result.status === 200 && result.data) {
      return;
    }
    // A confirmed denial invalidates the offer; only a deliberate new read can restore it.
    setOffer(null);
    setStartsAt("");
  }

  return {
    open,
    date,
    offer,
    startsAt,
    setStartsAt,
    confirmation,
    setConfirmation,
    busy,
    command,
    locked,
    show,
    close,
    load,
    confirm,
    book,
  };
}
