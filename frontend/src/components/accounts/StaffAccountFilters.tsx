"use client";

import { useState } from "react";
import { type Page } from "@/lib/api";
import { type Branch } from "@/lib/branches";
import { BranchFilter } from "@/components/staff/BranchFilter";
import { ChoiceColumnFilter } from "@/components/ui/data-table/ChoiceColumnFilter";
import { ColumnFilterForm, TextColumnFilter } from "@/components/ui/data-table/ColumnFilterForm";
import type { useStaffAccountsList } from "@/hooks/use-staff-accounts-list";
import { useBranchChoices } from "@/hooks/use-branch-choices";
import { roleNames } from "@/lib/auth/actor";
import { staffAccountRoles } from "@/lib/accounts/staff-accounts";

const roleOptions = [
  { value: "", label: "All roles" },
  ...staffAccountRoles.map((role) => ({ value: role, label: roleNames[role] })),
];

export function StaffAccountFilters({
  list,
  column,
  initialBranches,
  disabled,
}: {
  list: ReturnType<typeof useStaffAccountsList>;
  column: string;
  initialBranches: Page<Branch> | null;
  disabled: boolean;
}) {
  const { filters } = list.active;
  const [branchId, setBranchId] = useState(filters.branchId);
  const branchChoices = useBranchChoices(
    initialBranches,
    25,
    "Branch choices could not be refreshed. Reload the page to try again.",
  );
  const { branches, busy } = branchChoices;

  if (column === "name" || column === "email" || column === "mobile") {
    const labels = { name: "Name", email: "Email", mobile: "Mobile" } as const;
    return (
      <TextColumnFilter
        label={`${labels[column]} contains`}
        value={filters[column]}
        busy={disabled}
        onApply={(value) => list.load({ ...filters, [column]: value }, 1)}
      />
    );
  }
  if (column === "role") {
    return (
      <ChoiceColumnFilter
        label="Role"
        options={roleOptions}
        value={filters.role}
        empty=""
        busy={disabled}
        onApply={(role) => list.load({ ...filters, role }, 1)}
      />
    );
  }
  return (
    <ColumnFilterForm
      busy={disabled || busy}
      onApply={() => list.load({ ...filters, branchId }, 1)}
      onClear={() => list.load({ ...filters, branchId: "" }, 1)}
    >
      <BranchFilter
        branches={branches}
        branchId={branchId}
        selectedBranchName={
          list.snapshot.items.find((row) => row.branchId === branchId)?.branchName ?? undefined
        }
        disabled={disabled || busy}
        labelHidden={false}
        onChange={setBranchId}
        onPage={(page) => void branchChoices.load(page)}
      />
    </ColumnFilterForm>
  );
}
