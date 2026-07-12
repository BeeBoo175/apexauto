"use client";
import { StaffLink } from "@/components/staff/StaffLink";
import type { RepairSelection } from "@/lib/inspection/query";

export function InspectionRepairLink({
  inspectionId,
  selection,
  disabled,
  onSelect,
}: {
  inspectionId: string;
  selection: RepairSelection;
  disabled: boolean;
  onSelect: (selection: RepairSelection) => void;
}) {
  return (
    <StaffLink
      prefetch={false}
      href={`/dashboard/inspection?inspection=${inspectionId}&section=repairs&request=${selection.requestId}&revision=${selection.revision}`}
      aria-disabled={disabled || undefined}
      onClick={(event) => {
        if (disabled) {
          event.preventDefault();
          return;
        }
        if (
          event.button !== 0 ||
          event.ctrlKey ||
          event.metaKey ||
          event.shiftKey ||
          event.altKey
        ) {
          return;
        }
        // Local history selection keeps current form drafts and command receipts mounted.
        event.preventDefault();
        onSelect(selection);
      }}
    >
      Open repair request
    </StaffLink>
  );
}
