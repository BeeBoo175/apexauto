"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
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
import { StaffAccountStatus } from "./StaffAccountStatus";
import styles from "@/components/staff/StaffWorklist.module.css";

const columns: DataColumn<CustomerAccountRow>[] = [
  { id: "name", label: "Name", ...staffColumnSizing.person, cell: (row) => row.name },
  { id: "email", label: "Email", width: 240, cell: (row) => row.email },
  { id: "mobile", label: "Mobile", width: 144, cell: (row) => row.mobile },
  {
    id: "status",
    label: "Status",
    ...staffColumnSizing.shortStatus,
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
  return (
    <div className={`${styles.page} ${animateEntry ? workspace.reveal : ""}`}>
      <div className={styles.body}>
        <DataTable
          label="Customer accounts"
          rows={snapshot.items}
          columns={filteredColumns}
          rowId={(row) => row.id}
          page={snapshot}
          busy={busy}
          disabled={false}
          selectedRowId={selectedId}
          onRowClick={(row) => onOpen(row.id)}
          onPage={(page, pageSize) => void list.load({ ...filters, pageSize }, page)}
          emptyState={{
            title: "No matching customers",
            description: "Choose another tab or search.",
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
                value={filters.tab}
                disabled={busy}
                onChange={(tab) => void list.load({ ...filters, tab }, 1)}
              />
              <RecordToolbar
                filters={null}
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
                    <Button
                      size="compact"
                      variant="ghost"
                      disabled={busy}
                      onClick={() =>
                        void list.load({ ...filters, name: "", email: "", mobile: "" }, 1)
                      }
                    >
                      Reset filters
                    </Button>
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
