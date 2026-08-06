"use client";

import type { ReactNode } from "react";
import { ChoiceColumnFilter } from "@/components/ui/data-table/ChoiceColumnFilter";
import {
  HoursRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import type { DataColumn } from "@/components/ui/data-table/types";
import {
  tableViews,
  type SaleSummary,
  type SalesTableFilters,
  type TableView,
} from "@/lib/sales/contracts";
import { tableViewFilters } from "@/lib/sales/query";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import { hoursRangeLabel, moneyRangeLabel } from "@/lib/range-label";

export type LoadSales = (filters: SalesTableFilters, page: number) => Promise<boolean>;

export function saleTableFilters(
  filters: SalesTableFilters,
  busy: boolean,
  columns: DataColumn<SaleSummary>[],
  load: LoadSales,
) {
  const apply = (next: Partial<SalesTableFilters>) => load({ ...filters, ...next }, 1);
  const applied: AppliedFilter<SalesTableFilters>[] = [];
  const add = (
    key: AppliedFilter<SalesTableFilters>["key"],
    label: string,
    without: Partial<SalesTableFilters>,
  ) => applied.push({ key, label, without: { ...filters, ...without } });
  const texts = [
    ["reference", "Reference"],
    ["registration", "Registration"],
    ["vehicle", "Vehicle"],
    ["customer", "Customer"],
    ["handler", "Agent"],
    ["branch", "Branch"],
  ] as const;
  for (const [key, label] of texts) {
    if (filters[key]) {
      add(key, `${label}: ${filters[key]}`, { [key]: "" });
    }
  }
  if (filters.outcome) {
    add("outcome", `Outcome: ${filters.outcome}`, { outcome: "" });
  }
  if (filters.minimumDiscount || filters.maximumDiscount) {
    add(
      "discount",
      `Discount: ${moneyRangeLabel(filters.minimumDiscount, filters.maximumDiscount)}`,
      {
        minimumDiscount: "",
        maximumDiscount: "",
      },
    );
  }
  if (filters.minimumBalance || filters.maximumBalance) {
    add("balance", `Balance: ${moneyRangeLabel(filters.minimumBalance, filters.maximumBalance)}`, {
      minimumBalance: "",
      maximumBalance: "",
    });
  }
  if (filters.minimumHoursLeft || filters.maximumHoursLeft) {
    add(
      "timeLeft",
      `Time left: ${hoursRangeLabel(filters.minimumHoursLeft, filters.maximumHoursLeft)}`,
      {
        minimumHoursLeft: "",
        maximumHoursLeft: "",
      },
    );
  }

  const content: Record<string, { active: boolean; content: ReactNode }> = {
    ...Object.fromEntries(
      texts.map(([key, label]) => [
        key,
        {
          active: Boolean(filters[key]),
          content: (
            <TextColumnFilter
              label={`${label} contains`}
              value={filters[key]}
              busy={busy}
              onApply={(value) => apply({ [key]: value })}
            />
          ),
        },
      ]),
    ),
    status: {
      active: Boolean(filters.outcome),
      content: (
        <ChoiceColumnFilter
          label="Status"
          options={[
            ...tableViews.map((view) => ({ value: view.value, label: view.label })),
            { value: "Expired", label: "Expired" },
            { value: "Cancelled", label: "Cancelled" },
          ]}
          value={filters.outcome || filters.view}
          empty={filters.view}
          busy={busy}
          onApply={(value) => {
            const outcome = value === "Expired" || value === "Cancelled" ? value : "";
            const view = outcome ? "History" : (value as TableView);
            return load({ ...tableViewFilters(filters, view), outcome }, 1);
          }}
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
    timeLeft: {
      active: Boolean(filters.minimumHoursLeft || filters.maximumHoursLeft),
      content: (
        <HoursRangeFilter
          minimum={filters.minimumHoursLeft}
          maximum={filters.maximumHoursLeft}
          maximumHours={168}
          busy={busy}
          onApply={(minimumHoursLeft, maximumHoursLeft) =>
            apply({ minimumHoursLeft, maximumHoursLeft })
          }
        />
      ),
    },
  };
  return {
    applied,
    columns: columns.map((column) => ({ ...column, filter: content[column.id] })),
  };
}
