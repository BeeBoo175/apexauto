"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useListingClientEntry } from "@/hooks/use-listing-client-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";

export function ListingClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useListingClientEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Listings">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry listings
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
