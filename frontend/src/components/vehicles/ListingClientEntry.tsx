"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
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
  const { entry, failed } = useListingClientEntry(actor, parameters);
  if (failed) {
    return <StaffReloadNotice title="Listings" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Table, ...snapshot } = entry;
  return <Table actor={actor} {...snapshot} />;
}
