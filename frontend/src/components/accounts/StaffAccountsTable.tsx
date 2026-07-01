"use client";

import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { Avatar } from "@/components/ui/Avatar";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import type { useStaffAccountsList } from "@/hooks/use-staff-accounts-list";
import { roleNames } from "@/lib/auth/actor";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import {
  staffAccountTabs,
  type StaffAccountFilters as Filters,
  type StaffAccountRow,
} from "@/lib/accounts/staff-accounts";
import { StaffAccountFilters } from "./StaffAccountFilters";
import { StaffAccountStatus } from "./StaffAccountStatus";
import styles from "@/components/staff/StaffWorklist.module.css";
import table from "@/components/staff/StaffTable.module.css";

const roleName = (role: string) => roleNames[role as keyof typeof roleNames] ?? role;

export function StaffAccountsTable({
  list,
  branches,
  selectedId,
  onOpen,
  actions,
}: {
  list: ReturnType<typeof useStaffAccountsList>;
  branches: Page<Branch> | null;
  selectedId: string;
  onOpen: (id: string) => void;
  // The view switch and Invite staff sit beside the search, like other staff tables.
  actions: ReactNode;
}) {
  const { snapshot, active, busy, draft, setDraft } = list;
  const filters = active.filters;
  const indicators: Record<string, boolean> = {
    role: Boolean(filters.role),
    branchName: Boolean(filters.branchId),
  };
  const columns: DataColumn<StaffAccountRow>[] = [
    {
      id: "name",
      label: "Name",
      ...staffColumnSizing.person,
      cell: (row) => (
        <span className={table.inline}>
          <Avatar accountId={row.id} name={row.name} pictureId={row.pictureId} size="sm" />
          {row.name}
        </span>
      ),
    },
    { id: "email", label: "Email", width: 240, cell: (row) => row.email },
    { id: "mobile", label: "Mobile", width: 144, cell: (row) => row.mobile },
    { id: "role", label: "Role", width: 168, cell: (row) => roleName(row.role) },
    {
      id: "branchName",
      label: "Branch",
      ...staffColumnSizing.branch,
      cell: (row) => row.branchName ?? <span className={table.muted}>All branches</span>,
    },
    {
      id: "status",
      label: "Status",
      ...staffColumnSizing.shortStatus,
      cell: (row) => <StaffAccountStatus status={row.status} />,
    },
  ];
  const filteredColumns = columns.map((column) =>
    !(column.id in indicators)
      ? column
      : {
          ...column,
          filter: {
            active: indicators[column.id],
            content: (
              <StaffAccountFilters
                list={list}
                column={column.id}
                initialBranches={branches}
                disabled={busy}
              />
            ),
          },
        },
  );
  const applied: AppliedFilter<Filters>[] = [];
  if (filters.role) {
    applied.push({
      key: "role",
      label: roleName(filters.role),
      without: { ...filters, role: "" },
    });
  }
  if (filters.branchId) {
    applied.push({
      key: "branch",
      label:
        branches?.items.find((row) => row.id === filters.branchId)?.name ??
        snapshot.items.find((row) => row.branchId === filters.branchId)?.branchName ??
        "Selected branch",
      without: { ...filters, branchId: "" },
    });
  }
  return (
    <div className={styles.page}>
      <div className={styles.body}>
        <DataTable
          label="Staff accounts"
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
            title: "No matching staff",
            description: "Choose another tab, search or filter.",
          }}
          toolbar={
            <>
              <StaffQueueTabs
                label="Staff account tabs"
                countNoun="accounts"
                tabs={staffAccountTabs.map((item) => ({
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
                  <>
                    {applied.length > 0 && (
                      <Button
                        size="compact"
                        variant="ghost"
                        disabled={busy}
                        onClick={() => void list.load({ ...filters, role: "", branchId: "" }, 1)}
                      >
                        Reset filters
                      </Button>
                    )}
                    {actions}
                  </>
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
                    label="Search staff"
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
