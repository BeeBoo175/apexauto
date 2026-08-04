"use client";

import { useRef, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { parseListingWorkspace, type ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import {
  listingRecordSection,
  type ListingRecordSelection,
} from "@/lib/vehicles/listing-history-selection";
import type { ListingSection } from "@/lib/vehicles/listings";
import type { ListingSelectionPath } from "@/lib/vehicles/listing-location";
import { useStaffSelection } from "./use-staff-selection";
import { useGuardedAction } from "./use-guarded-action";

export type ListingLocationHandler = (
  selection: ListingSelectionPath,
  mode: false | "push" | "replace",
) => void;

type Destination = { section: ListingSection; record: ListingRecordSelection | null };

// The selected listing, its section and any retained history record; the path names all three.
export function useListingSelection(
  id: string,
  initial: ApiResult<ListingWorkspace> | null,
  onLocation: ListingLocationHandler,
  initialSection: ListingSection = "listing",
  initialRecord: ListingRecordSelection | null = null,
) {
  const interaction = useGuardedAction();
  const [section, setSection] = useState(initialSection);
  const [record, setRecord] = useState(initialRecord);
  // The destination of the read in flight; it becomes the shown section only once the read is confirmed.
  const destination = useRef<Destination>({ section: initialSection, record: initialRecord });
  const selection = useStaffSelection(id, initial, {
    parameter: "listing",
    path: (listingId) => `/api/staff/listings/${listingId}`,
    parse: parseListingWorkspace,
    matches: (value, listingId) => value.vehicle.vehicleId === listingId,
    read: privateApiFetch,
    commitAfterRead: true,
    address: (confirmedId, updateAddress = true) => {
      const target = confirmedId
        ? destination.current
        : { section: "listing" as const, record: null };
      onLocation({ id: confirmedId, ...target }, updateAddress && "push");
    },
  });

  function show(target: Destination) {
    destination.current = target;
    setSection(target.section);
    setRecord(target.record);
  }

  // Choosing another listing opens it on the Listing section; reading the same one again keeps where it was.
  function choose(nextId: string) {
    if (nextId === selection.id) {
      return selection.choose(nextId);
    }
    destination.current = { section: "listing", record: null };
    return selection.choose(nextId, () => show(destination.current));
  }

  // Applies a browser Back/Forward or refresh destination without writing the address.
  function visit(target: ListingSelectionPath, updateAddress = false) {
    destination.current = { section: target.section, record: target.record };
    if (target.id === selection.id && selection.result?.data) {
      show(destination.current);
      return Promise.resolve(selection.result.data);
    }
    return selection.load(target.id, () => show(destination.current), updateAddress);
  }

  function changeSection(next: ListingSection) {
    show({ section: next, record: null });
    onLocation({ id: selection.id, section: next, record: null }, "push");
  }

  function review(next: ListingRecordSelection | null) {
    const target = { section: next ? listingRecordSection(next) : section, record: next };
    show(target);
    onLocation({ id: selection.id, ...target }, "push");
  }

  function close() {
    interaction.request(() => closeConfirmed(), "Discard and close");
  }

  function closeConfirmed(updateAddress = true) {
    selection.close(updateAddress);
    show({ section: "listing", record: null });
  }

  return {
    ...selection,
    choose,
    visit,
    close,
    closeConfirmed,
    section,
    record,
    changeSection,
    review,
  };
}
