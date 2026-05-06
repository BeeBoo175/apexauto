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
import type { SaleSummary, SalesTableFilters } from "@/lib/sales/sales";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";

export type LoadSales = (filters: SalesTableFilters, page: number) => Promise<boolean>;

const money = (minimum: string, maximum: string) => `MYR ${minimum || "0"}–${maximum || "any"}`;

// Filters for the columns that carry them; applied filters also show as removable chips.
export function saleTableFilters(
  filters: SalesTableFilters,
  busy: boolean,
  columns: DataColumn<SaleSummary>[],
  load: LoadSales,
) {
  const apply = (next: Partial<SalesTableFilters>) => load({ ...filters, ...next }, 1);
  const applied: AppliedWorklistFilter<SalesTableFilters>[] = [];
  const add = (
    key: AppliedWorklistFilter<SalesTableFilters>["key"],
    label: string,
    without: Partial<SalesTableFilters>,
  ) => applied.push({ key, label, without: { ...filters, ...without } });
  if (filters.agent) {
    add("agent", `Agent: ${filters.agent}`, { agent: "" });
  }
  if (filters.branch) {
    add("branch", `Branch: ${filters.branch}`, { branch: "" });
  }
  if (filters.outcome) {
    add("outcome", `Outcome: ${filters.outcome}`, { outcome: "" });
  }
  if (filters.minimumDiscount || filters.maximumDiscount) {
    add("discount", `Discount: ${money(filters.minimumDiscount, filters.maximumDiscount)}`, {
      minimumDiscount: "",
      maximumDiscount: "",
    });
  }
  if (filters.minimumBalance || filters.maximumBalance) {
    add("balance", `Balance: ${money(filters.minimumBalance, filters.maximumBalance)}`, {
      minimumBalance: "",
      maximumBalance: "",
    });
  }
  if (filters.submittedFrom || filters.submittedTo) {
    add("submitted", `Submitted: ${dateRangeLabel(filters.submittedFrom, filters.submittedTo)}`, {
      submittedFrom: "",
      submittedTo: "",
    });
  }
  if (filters.closedFrom || filters.closedTo) {
    add("closed", `Closed: ${dateRangeLabel(filters.closedFrom, filters.closedTo)}`, {
      closedFrom: "",
      closedTo: "",
    });
  }

  const content: Record<string, { active: boolean; content: ReactNode }> = {
    agent: {
      active: Boolean(filters.agent),
      content: (
        <TextColumnFilter
          label="Agent contains"
          value={filters.agent}
          busy={busy}
          onApply={(agent) => apply({ agent })}
        />
      ),
    },
    branch: {
      active: Boolean(filters.branch),
      content: (
        <TextColumnFilter
          label="Branch contains"
          value={filters.branch}
          busy={busy}
          onApply={(branch) => apply({ branch })}
        />
      ),
    },
    status: {
      active: Boolean(filters.outcome),
      content: (
        <ChoiceColumnFilter
          label="Outcome"
          options={[
            { value: "", label: "All outcomes" },
            { value: "Expired", label: "Expired" },
            { value: "Cancelled", label: "Cancelled" },
          ]}
          value={filters.outcome}
          empty=""
          busy={busy}
          onApply={(outcome) => apply({ outcome })}
        />
      ),
    },
    discount: {
      active: Boolean(filters.minimumDiscount || filters.maximumDiscount),
      content: (
        <MoneyRangeFilter
          minimum={filters.minimumDiscount}
          maximum={filters.maximumDiscount}
          busy={busy}
          onApply={(minimumDiscount, maximumDiscount) =>
            apply({ minimumDiscount, maximumDiscount })
          }
        />
      ),
    },
    balance: {
      active: Boolean(filters.minimumBalance || filters.maximumBalance),
      content: (
        <MoneyRangeFilter
          minimum={filters.minimumBalance}
          maximum={filters.maximumBalance}
          busy={busy}
          onApply={(minimumBalance, maximumBalance) => apply({ minimumBalance, maximumBalance })}
        />
      ),
    },
    submitted: {
      active: Boolean(filters.submittedFrom || filters.submittedTo),
      content: (
        <DateRangeFilter
          from={filters.submittedFrom}
          to={filters.submittedTo}
          busy={busy}
          onApply={(submittedFrom, submittedTo) => apply({ submittedFrom, submittedTo })}
        />
      ),
    },
    closed: {
      active: Boolean(filters.closedFrom || filters.closedTo),
      content: (
        <DateRangeFilter
          from={filters.closedFrom}
          to={filters.closedTo}
          busy={busy}
          onApply={(closedFrom, closedTo) => apply({ closedFrom, closedTo })}
        />
      ),
    },
  };
  return {
    applied,
    columns: columns.map((column) => {
      // Outcome only means something on History, where every row has ended.
      const filter =
        column.id === "status" && filters.view !== "History" ? undefined : content[column.id];
      return filter ? { ...column, filter } : column;
    }),
  };
}
