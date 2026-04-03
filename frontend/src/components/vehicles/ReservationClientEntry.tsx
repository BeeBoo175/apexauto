"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useReservationClientEntry } from "@/hooks/use-reservation-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export function ReservationClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useReservationClientEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Reservations">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry reservations
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Table, ...snapshot } = entry;
  return <Table actor={actor} {...snapshot} />;
}
