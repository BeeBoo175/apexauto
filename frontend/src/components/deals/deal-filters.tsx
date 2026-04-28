"use client";

import type { ReactNode } from "react";
import { ChoiceColumnFilter } from "@/components/ui/data-table/ChoiceColumnFilter";
import {
  DateRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import type { DataColumn } from "@/components/ui/data-table/types";
import { dateRangeLabel } from "@/lib/date-range-label";
import {
  dealStatusLabels,
  dealStatuses,
  type DealFilters,
  type DealSummary,
} from "@/lib/deals/deals";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";

export type LoadDeals = (filters: DealFilters, page: number) => Promise<boolean>;

const money = (minimum: string, maximum: string) => `MYR ${minimum || "0"}–${maximum || "any"}`;

// Every shown column carries a filter. Next step and submitted date moved to the drawer; filters already in
// the address still show as removable chips below.
export function dealTableFilters(
  filters: DealFilters,
  busy: boolean,
  columns: DataColumn<DealSummary>[],
  load: LoadDeals,
) {
  const apply = (next: Partial<DealFilters>) => load({ ...filters, ...next }, 1);
  const applied: AppliedWorklistFilter<DealFilters>[] = [];
  const texts = [
    { key: "number", label: "Deal", title: "Deal" },
    { key: "registration", label: "Registration", title: "Registration" },
    { key: "vehicle", label: "Vehicle", title: "Vehicle" },
    { key: "customer", label: "Customer", title: "Customer" },
    { key: "agent", label: "Agent", title: "Agent" },
    { key: "branch", label: "Branch", title: "Branch" },
  ] as const;
  for (const text of texts) {
    if (filters[text.key]) {
      applied.push({
        key: text.key,
        label: `${text.label}: ${filters[text.key]}`,
        without: { ...filters, [text.key]: "" },
      });
    }
  }
  if (filters.status) {
    applied.push({
      key: "status",
      label: dealStatusLabels[filters.status as keyof typeof dealStatusLabels] ?? filters.status,
      without: { ...filters, status: "" },
    });
  }
  if (filters.nextStep) {
    applied.push({
      key: "nextStep",
      label: `Next step: ${filters.nextStep}`,
      without: { ...filters, nextStep: "" },
    });
  }
  if (filters.minimumBalance || filters.maximumBalance) {
    applied.push({
      key: "total",
      label: `Balance: ${money(filters.minimumBalance, filters.maximumBalance)}`,
      without: { ...filters, minimumBalance: "", maximumBalance: "" },
    });
  }
  if (filters.minimumDiscount || filters.maximumDiscount) {
    applied.push({
      key: "discount",
      label: `Discount: ${money(filters.minimumDiscount, filters.maximumDiscount)}`,
      without: { ...filters, minimumDiscount: "", maximumDiscount: "" },
    });
  }
  if (filters.submittedFrom || filters.submittedTo) {
    applied.push({
      key: "submitted",
      label: `Submitted: ${dateRangeLabel(filters.submittedFrom, filters.submittedTo)}`,
      without: { ...filters, submittedFrom: "", submittedTo: "" },
    });
  }
  if (filters.updatedFrom || filters.updatedTo) {
    applied.push({
      key: "updated",
      label: `Updated: ${dateRangeLabel(filters.updatedFrom, filters.updatedTo)}`,
      without: { ...filters, updatedFrom: "", updatedTo: "" },
    });
  }

  const content: Record<string, ReactNode> = {
    status: (
      <ChoiceColumnFilter
        label="Status"
        options={[
          { value: "", label: "All statuses" },
          ...dealStatuses.map((value) => ({ value, label: dealStatusLabels[value] })),
        ]}
        value={filters.status}
        empty=""
        busy={busy}
        onApply={(status) => apply({ status })}
      />
    ),
    balance: (
      <MoneyRangeFilter
        minimum={filters.minimumBalance}
        maximum={filters.maximumBalance}
        busy={busy}
        onApply={(minimumBalance, maximumBalance) => apply({ minimumBalance, maximumBalance })}
      />
    ),
    discount: (
      <MoneyRangeFilter
        minimum={filters.minimumDiscount}
        maximum={filters.maximumDiscount}
        busy={busy}
        onApply={(minimumDiscount, maximumDiscount) => apply({ minimumDiscount, maximumDiscount })}
      />
    ),
    updated: (
      <DateRangeFilter
        from={filters.updatedFrom}
        to={filters.updatedTo}
        busy={busy}
        onApply={(updatedFrom, updatedTo) => apply({ updatedFrom, updatedTo })}
      />
    ),
  };
  for (const text of texts) {
    content[text.key] = (
      <TextColumnFilter
        label={`${text.title} contains`}
        value={filters[text.key]}
        busy={busy}
        onApply={(value) => apply({ [text.key]: value })}
      />
    );
  }
  const active: Record<string, boolean> = {
    number: Boolean(filters.number),
    registration: Boolean(filters.registration),
    vehicle: Boolean(filters.vehicle),
    customer: Boolean(filters.customer),
    agent: Boolean(filters.agent),
    branch: Boolean(filters.branch),
    status: Boolean(filters.status),
    balance: Boolean(filters.minimumBalance || filters.maximumBalance),
    discount: Boolean(filters.minimumDiscount || filters.maximumDiscount),
    updated: Boolean(filters.updatedFrom || filters.updatedTo),
  };
  return {
    applied,
    columns: columns.map((column) =>
      content[column.id]
        ? { ...column, filter: { active: active[column.id] ?? false, content: content[column.id] } }
        : column,
    ),
  };
}
