"use client";

import type { ReactNode } from "react";
import {
  DateRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
  WholeRangeFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import { ChoiceColumnFilter } from "@/components/ui/data-table/ChoiceColumnFilter";
import type { DataColumn } from "@/components/ui/data-table/types";
import { dateRangeLabel } from "@/lib/date-range-label";
import {
  warrantyGrades,
  type ListFilters,
  type Promo,
  type Warranty,
} from "@/lib/settings/settings";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import { promoColumns, warrantyColumns } from "./settings-columns";
import { moneyRangeLabel } from "@/lib/range-label";

function wholeRangeLabel(from: string, to: string, unit: string) {
  if (from && to) {
    return `${from} to ${to} ${unit}`;
  }
  return from ? `from ${from} ${unit}` : `up to ${to} ${unit}`;
}

const statusLabel = (status: string) => `${status.charAt(0).toUpperCase()}${status.slice(1)}`;

export type LoadList = (filters: ListFilters, page: number) => Promise<boolean>;

function statusFilter(
  filters: ListFilters,
  busy: boolean,
  statuses: readonly string[],
  apply: (next: Partial<ListFilters>) => Promise<boolean>,
) {
  return (
    <ChoiceColumnFilter
      label="Status"
      options={statuses.map((value) => ({
        value,
        label: value === "all" ? "All statuses" : statusLabel(value),
      }))}
      value={filters.status}
      empty="all"
      busy={busy}
      onApply={(status) => apply({ status })}
    />
  );
}

function attach<Row>(
  columns: DataColumn<Row>[],
  content: Record<string, ReactNode>,
  active: Record<string, boolean>,
) {
  return columns.map((column) =>
    content[column.id]
      ? { ...column, filter: { active: active[column.id] ?? false, content: content[column.id] } }
      : column,
  );
}

export function promoTableFilters(
  filters: ListFilters,
  busy: boolean,
  statuses: readonly string[],
  load: LoadList,
) {
  const apply = (next: Partial<ListFilters>) => load({ ...filters, ...next }, 1);
  const applied: AppliedFilter<ListFilters>[] = [];
  if (filters.text) {
    applied.push({
      key: "code",
      label: `Code: ${filters.text}`,
      without: { ...filters, text: "" },
    });
  }
  if (filters.minimum || filters.maximum) {
    applied.push({
      key: "discount",
      label: `Discount: ${moneyRangeLabel(filters.minimum, filters.maximum)}`,
      without: { ...filters, minimum: "", maximum: "" },
    });
  }
  if (filters.expiresFrom || filters.expiresTo) {
    applied.push({
      key: "expires",
      label: `Expires: ${dateRangeLabel(filters.expiresFrom, filters.expiresTo)}`,
      without: { ...filters, expiresFrom: "", expiresTo: "" },
    });
  }
  if (filters.status !== "all") {
    applied.push({
      key: "status",
      label: statusLabel(filters.status),
      without: { ...filters, status: "all" },
    });
  }
  const columns = attach<Promo>(
    promoColumns,
    {
      code: (
        <TextColumnFilter
          label="Code contains"
          value={filters.text}
          busy={busy}
          onApply={(text) => apply({ text })}
        />
      ),
      discount: (
        <MoneyRangeFilter
          minimum={filters.minimum}
          maximum={filters.maximum}
          busy={busy}
          onApply={(minimum, maximum) => apply({ minimum, maximum })}
        />
      ),
      expiresAt: (
        <DateRangeFilter
          from={filters.expiresFrom}
          to={filters.expiresTo}
          busy={busy}
          onApply={(expiresFrom, expiresTo) => apply({ expiresFrom, expiresTo })}
        />
      ),
      status: statusFilter(filters, busy, statuses, apply),
    },
    {
      code: Boolean(filters.text),
      discount: Boolean(filters.minimum || filters.maximum),
      expiresAt: Boolean(filters.expiresFrom || filters.expiresTo),
      status: filters.status !== "all",
    },
  );
  return { columns, applied };
}

export function warrantyTableFilters(
  filters: ListFilters,
  busy: boolean,
  statuses: readonly string[],
  load: LoadList,
) {
  const apply = (next: Partial<ListFilters>) => load({ ...filters, ...next }, 1);
  const applied: AppliedFilter<ListFilters>[] = [];
  if (filters.text) {
    applied.push({
      key: "name",
      label: `Package: ${filters.text}`,
      without: { ...filters, text: "" },
    });
  }
  if (filters.minimum || filters.maximum) {
    applied.push({
      key: "price",
      label: `Price: ${moneyRangeLabel(filters.minimum, filters.maximum)}`,
      without: { ...filters, minimum: "", maximum: "" },
    });
  }
  if (filters.termFrom || filters.termTo) {
    applied.push({
      key: "term",
      label: `Term: ${wholeRangeLabel(filters.termFrom, filters.termTo, "months")}`,
      without: { ...filters, termFrom: "", termTo: "" },
    });
  }
  if (filters.mileageFrom || filters.mileageTo) {
    applied.push({
      key: "mileage",
      label: `Mileage cap: ${wholeRangeLabel(filters.mileageFrom, filters.mileageTo, "km")}`,
      without: { ...filters, mileageFrom: "", mileageTo: "" },
    });
  }
  if (filters.grade) {
    applied.push({
      key: "grade",
      label: `Grade ${filters.grade}`,
      without: { ...filters, grade: "" },
    });
  }
  if (filters.status !== "all") {
    applied.push({
      key: "status",
      label: statusLabel(filters.status),
      without: { ...filters, status: "all" },
    });
  }
  const columns = attach<Warranty>(
    warrantyColumns,
    {
      name: (
        <TextColumnFilter
          label="Package contains"
          value={filters.text}
          busy={busy}
          onApply={(text) => apply({ text })}
        />
      ),
      term: (
        <WholeRangeFilter
          unit="months"
          minimum={filters.termFrom}
          maximum={filters.termTo}
          busy={busy}
          onApply={(termFrom, termTo) => apply({ termFrom, termTo })}
        />
      ),
      mileage: (
        <WholeRangeFilter
          unit="km"
          minimum={filters.mileageFrom}
          maximum={filters.mileageTo}
          busy={busy}
          onApply={(mileageFrom, mileageTo) => apply({ mileageFrom, mileageTo })}
        />
      ),
      price: (
        <MoneyRangeFilter
          minimum={filters.minimum}
          maximum={filters.maximum}
          busy={busy}
          onApply={(minimum, maximum) => apply({ minimum, maximum })}
        />
      ),
      grade: (
        <ChoiceColumnFilter
          label="Minimum grade"
          options={[
            { value: "", label: "Any grade" },
            ...warrantyGrades.map((grade) => ({ value: grade, label: `Grade ${grade}` })),
          ]}
          value={filters.grade}
          empty=""
          busy={busy}
          onApply={(grade) => apply({ grade })}
        />
      ),
      status: statusFilter(filters, busy, statuses, apply),
    },
    {
      name: Boolean(filters.text),
      term: Boolean(filters.termFrom || filters.termTo),
      mileage: Boolean(filters.mileageFrom || filters.mileageTo),
      price: Boolean(filters.minimum || filters.maximum),
      grade: Boolean(filters.grade),
      status: filters.status !== "all",
    },
  );
  return { columns, applied };
}
