"use client";

import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import type { useCustomerAccountsList } from "@/hooks/use-customer-accounts-list";
import {
  customerAccountTabs,
  customerStatus,
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
  return (
    <div className={styles.page}>
      <div className={styles.body}>
        <DataTable
          label="Customer accounts"
          rows={snapshot.items}
          columns={columns}
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
                filterCount={0}
                filterSummary={null}
                actions={null}
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
