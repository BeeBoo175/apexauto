"use client";

import { useState, type ReactNode } from "react";
import {
  ColumnFilterForm,
  DateRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import type { DataColumn } from "@/components/ui/data-table/types";
import { FilterChoices } from "@/components/ui/FilterChoices";
import { dateRangeLabel } from "@/lib/date-range-label";
import {
  warrantyGrades,
  type ListFilters,
  type Promo,
  type Warranty,
} from "@/lib/settings/settings";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import { promoColumns, warrantyColumns } from "./settings-columns";

export type LoadList = (filters: ListFilters, page: number) => Promise<boolean>;

function ChoiceFilter({
  label,
  options,
  value,
  empty,
  busy,
  onApply,
}: {
  label: string;
  options: { value: string; label: string }[];
  value: string;
  empty: string;
  busy: boolean;
  onApply: (value: string) => Promise<boolean>;
}) {
  const [draft, setDraft] = useState(value);
  return (
    <ColumnFilterForm busy={busy} onApply={() => onApply(draft)} onClear={() => onApply(empty)}>
      <FilterChoices
        label={label}
        options={options}
        value={draft}
        disabled={busy}
        onChange={setDraft}
      />
    </ColumnFilterForm>
  );
}

const money = (filters: ListFilters) => `MYR ${filters.minimum || "0"}–${filters.maximum || "any"}`;

function statusFilter(
  filters: ListFilters,
  busy: boolean,
  statuses: readonly string[],
  apply: (next: Partial<ListFilters>) => Promise<boolean>,
) {
  return (
    <ChoiceFilter
      label="Status"
      options={statuses.map((value) => ({
        value,
        label: value === "All" ? "All statuses" : value,
      }))}
      value={filters.status}
      empty="All"
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
  const applied: AppliedWorklistFilter<ListFilters>[] = [];
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
      label: `Discount: ${money(filters)}`,
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
  if (filters.status !== "All") {
    applied.push({ key: "status", label: filters.status, without: { ...filters, status: "All" } });
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
      status: filters.status !== "All",
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
  const applied: AppliedWorklistFilter<ListFilters>[] = [];
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
      label: `Price: ${money(filters)}`,
      without: { ...filters, minimum: "", maximum: "" },
    });
  }
  if (filters.grade) {
    applied.push({
      key: "grade",
      label: `Grade ${filters.grade}`,
      without: { ...filters, grade: "" },
    });
  }
  if (filters.status !== "All") {
    applied.push({ key: "status", label: filters.status, without: { ...filters, status: "All" } });
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
      price: (
        <MoneyRangeFilter
          minimum={filters.minimum}
          maximum={filters.maximum}
          busy={busy}
          onApply={(minimum, maximum) => apply({ minimum, maximum })}
        />
      ),
      grade: (
        <ChoiceFilter
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
      price: Boolean(filters.minimum || filters.maximum),
      grade: Boolean(filters.grade),
      status: filters.status !== "All",
    },
  );
  return { columns, applied };
}
