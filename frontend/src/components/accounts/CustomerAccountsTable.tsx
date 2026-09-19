"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { ResetFiltersButton } from "@/components/ui/ResetFiltersButton";
import { DataTable } from "@/components/ui/data-table/DataTable";
import { TableDiscovery } from "@/components/ui/data-table/TableDiscovery";
import type { DataColumn, TableSort } from "@/components/ui/data-table/types";
import { TextColumnFilter } from "@/components/ui/data-table/ColumnFilterForm";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import type { useCustomerAccountsList } from "@/hooks/use-customer-accounts-list";
import {
  customerAccountTabs,
  customerStatus,
  type CustomerAccountFilters,
  type CustomerAccountRow,
} from "@/lib/accounts/customer-accounts";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { StaffAccountStatus } from "./StaffAccountStatus";
import styles from "@/components/staff/StaffWorklist.module.css";

const columns: DataColumn<CustomerAccountRow>[] = [
  {
    id: "name",
    label: "Name",
    ...staffColumnSizing.person,
    sortable: true,
    cell: (row) => <CustomerAvatar id={row.id} name={row.name} />,
  },
  { id: "email", label: "Email", width: 240, sortable: true, cell: (row) => row.email },
  { id: "mobile", label: "Mobile", width: 144, sortable: true, cell: (row) => row.mobile },
  {
    id: "status",
    label: "Status",
    ...staffColumnSizing.shortStatus,
    sortable: true,
    cell: (row) => <StaffAccountStatus status={customerStatus(row)} />,
  },
];

// Customers are listed with masked contact; the opened account shows the full details.
export function CustomerAccountsTable({
  list,
  selectedId,
  onOpen,
}: {
  list: ReturnType<typeof useCustomerAccountsList>;
  selectedId: string;
  onOpen: (id: string) => void;
}) {
  const { snapshot, active, busy, draft, setDraft } = list;
  const filters = active.filters;
  const animateEntry = useContext(StaffPageEntranceContext);
  const textColumns = [
    ["name", "Name"],
    ["email", "Email"],
    ["mobile", "Mobile"],
  ] as const;
  const filteredColumns = columns.map((column) => {
    const text = textColumns.find(([key]) => key === column.id);
    if (!text) {
      return column;
    }
    const [key, label] = text;
    return {
      ...column,
      filter: {
        active: Boolean(filters[key]),
        content: (
          <TextColumnFilter
            label={`${label} contains`}
            value={filters[key]}
            busy={busy}
            onApply={(value) => list.load({ ...filters, [key]: value }, 1)}
          />
        ),
      },
    };
  });
  const applied: AppliedFilter<CustomerAccountFilters>[] = textColumns
    .filter(([key]) => filters[key])
    .map(([key, label]) => ({
      key,
      label: `${label}: ${filters[key]}`,
      without: { ...filters, [key]: "" },
    }));
  // The view tab is not a criterion to clear, so an empty tab never offers a clear.
  const narrowed = applied.length > 0 || Boolean(filters.search);
  const sorting = {
    value: filters.sort ? { column: filters.sort, direction: filters.direction } : null,
    onChange: (next: TableSort | null) =>
      void list.load(
        { ...filters, sort: next?.column ?? "", direction: next?.direction ?? "asc" },
        1,
      ),
  };

  function clearAll() {
    void list.load({ ...filters, search: "", name: "", email: "", mobile: "" }, 1);
  }

  function clearFilters() {
    void list.load({ ...filters, name: "", email: "", mobile: "" }, 1);
  }

  return (
    <div className={`${styles.page} ${animateEntry ? workspace.reveal : ""}`}>
      <div className={styles.body}>
        <DataTable
          label="Customer accounts"
          rows={snapshot.items}
          columns={filteredColumns}
          rowId={(row) => row.id}
          page={snapshot}
          mobile="columns"
          sorting={sorting}
          busy={busy}
          disabled={false}
          selectedRowId={selectedId}
          onRowClick={(row) => onOpen(row.id)}
          onPage={(page, pageSize) => void list.load({ ...filters, pageSize }, page)}
          emptyState={{
            noun: "customers",
            icon: "user",
            narrowed,
            hint: "Customers appear here once they sign up.",
            onClear: clearAll,
          }}
          toolbar={
            <>
              <StaffQueueTabs
                label="Customer account tabs"
                countNoun="customers"
                tabs={customerAccountTabs.map((item) => ({
                  ...item,
                  count: snapshot.counts[item.count],
                }))}
                value={filters.view}
                disabled={busy}
                onChange={(view) => void list.load({ ...filters, view }, 1)}
              />
              <RecordToolbar
                filters={null}
                mobileDiscovery={
                  <TableDiscovery
                    label="customers"
                    columns={filteredColumns}
                    sorting={sorting}
                    filterCount={applied.length}
                    disabled={busy}
                    onClearFilters={clearFilters}
                  />
                }
                disabled={false}
                filterCount={applied.length}
                filterSummary={
                  <AppliedWorklistFilters
                    filters={applied}
                    disabled={busy}
                    onRemove={(next) => void list.load(next, 1)}
                  />
                }
                actions={
                  applied.length > 0 && (
                    <ResetFiltersButton disabled={busy} onClick={clearFilters} />
                  )
                }
              >
                <form
                  className={styles.search}
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (!busy) {
                      void list.load({ ...filters, search: draft.search.trim() }, 1);
                    }
                  }}
                >
                  <SearchField
                    label="Search customers"
                    placeholder="Search name, email or mobile"
                    value={draft.search}
                    disabled={false}
                    busy={busy}
                    onChange={(search) => setDraft((current) => ({ ...current, search }))}
                    onClear={
                      filters.search && !busy
                        ? () => void list.load({ ...filters, search: "" }, 1)
                        : undefined
                    }
                  />
                </form>
              </RecordToolbar>
            </>
          }
        />
      </div>
    </div>
  );
}
