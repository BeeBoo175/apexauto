"use client";

import { useState } from "react";
import { StaffHistorySection } from "@/components/staff/StaffHistorySection";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { useAddressSection } from "@/hooks/use-address-section";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { privateApiFetch } from "@/lib/api-browser";
import { roleNames, type CurrentActor, type Role } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { formatCurrency } from "@/lib/currency-format";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import {
  milestoneHref,
  milestoneLabels,
  parseMilestonePage,
  personStatusLabels,
  type Milestone,
  type TeamPerson,
} from "@/lib/performance/team";
import { RowsSkeleton } from "./PerformanceSkeletons";
import type { TeamColumn } from "./TeamColumns";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Performance.module.css";

type Section = "summary" | "milestones";

const milestoneCsvColumns: CsvColumn<Milestone>[] = [
  { label: "Milestone", value: (item) => milestoneLabels[item.kind] ?? item.kind },
  { label: "Reference", value: (item) => item.reference },
  { label: "Date (UTC)", value: (item) => item.at },
  { label: "Branch", value: (item) => item.branchName },
  {
    label: "Agreed sale total (MYR)",
    value: (item) => (item.agreedTotal === null ? "" : item.agreedTotal.toFixed(2)),
  },
];

interface DrawerProps<Row extends TeamPerson> {
  columns: TeamColumn<Row>[];
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
}

// One person's period in the shared staff drawer layout, like the Accounts drawers: the summary band, This period
// and Milestones tabs over one content area, and a footer that stays on every tab with the milestone export.
export function PerformanceDrawer<Row extends TeamPerson>({
  row,
  onClose,
  ...props
}: DrawerProps<Row> & { row: Row | null; onClose: () => void }) {
  return (
    <Dialog
      open={Boolean(row)}
      variant="drawer"
      density="compact"
      title={row?.name ?? "Employee"}
      onClose={onClose}
    >
      {row && <DrawerContent key={`${row.personId}:${row.branchId}`} row={row} {...props} />}
    </Dialog>
  );
}

function DrawerContent<Row extends TeamPerson>({
  row,
  columns,
  period,
  actor,
  refreshes,
}: DrawerProps<Row> & { row: Row }) {
  const [section, chooseSection] = useAddressSection<Section>(["summary", "milestones"]);
  // Milestones are read only once their tab has been opened.
  const [opened, setOpened] = useState(section === "milestones");
  const path = `people/${row.personId}/milestones`;
  const exporter = usePerformanceExport<Milestone>(
    `${row.name.toLowerCase().replace(/[^a-z]+/g, "-")}-milestones`,
    milestoneCsvColumns,
    async (number, size, signal) => {
      const result = await privateApiFetch(
        `/api/staff/performance/${path}?${periodQuery(period)}&page=${number}&pageSize=${size}`,
        parseMilestonePage,
        { signal },
      );
      return { status: result.status, data: result.data };
    },
    (item) => `${item.kind}:${item.recordId}`,
    "Milestones",
  );
  return (
    <div className={workbench.workspace}>
      <StaffRecordSummary
        ariaLabel="Role, branch and status"
        label={row.branchName}
        value={roleNames[row.role as Role] ?? row.role}
        lead={
          <Avatar accountId={row.personId} name={row.name} pictureId={row.pictureId} size="lg" />
        }
        context={<span>{personStatusLabels[row.status]}</span>}
      />
      <StaffWorkspaceTabs
        label="Performance sections"
        tabs={[
          { value: "summary", label: "This period", available: true },
          { value: "milestones", label: "Milestones", available: true },
        ]}
        value={section}
        onChange={(next) => {
          chooseSection(next);
          setOpened((current) => current || next === "milestones");
        }}
      />
      {section === "summary" && (
        <div className={`${workbench.body} ${styles.drawerBody}`}>
          <dl className={styles.facts}>
            {columns.map((column) => (
              <div key={column.key} className={styles.fact}>
                <dt>{column.now ? `${column.label}, now` : column.label}</dt>
                <dd className={column.value(row) === null ? styles.absent : undefined}>
                  {column.text(row)}
                </dd>
              </div>
            ))}
          </dl>
          <p className={styles.note}>
            Results belong to the branch that recorded them. Elapsed times are calendar days, not
            hours worked.
          </p>
        </div>
      )}
      {opened && (
        <div className={styles.milestonePanel} hidden={section !== "milestones"}>
          <Milestones path={path} period={period} actor={actor} refreshes={refreshes} />
        </div>
      )}
      <StaffWorkspaceFooter
        title="Milestones export"
        description="Every milestone in this period, with the agreed total of each delivered sale."
      >
        <Button
          size="compact"
          variant="secondary"
          disabled={exporter.busy}
          onClick={() => void exporter.exportAll()}
        >
          {exporter.busy ? "Preparing CSV…" : "Export milestones CSV"}
        </Button>
      </StaffWorkspaceFooter>
    </div>
  );
}

const milestonePageSize = 10;

// The person's completed work in the period on the shared history timeline, newest first, paged with the shared
// table pager.
function Milestones({
  path,
  period,
  actor,
  refreshes,
}: {
  path: string;
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(milestonePageSize);
  const read = usePerformanceSection(
    path,
    parseMilestonePage,
    period,
    actor,
    refreshes,
    "Milestones",
    true,
    `page=${page}&pageSize=${pageSize}`,
  );
  const milestones = read.data;
  return (
    <div className={`${workbench.body} ${styles.drawerBody}`}>
      <StaffHistorySection
        label="Milestones"
        busy={read.busy}
        flush
        empty={milestones?.total === 0 && <p>No completed work in this period.</p>}
        progress={
          <>
            {!milestones && !read.failed && (
              <div role="status" aria-label="Loading milestones">
                <span className="sr-only">Loading milestones…</span>
                <RowsSkeleton count={4} />
              </div>
            )}
            {!milestones && read.failed && (
              <div className={styles.retry}>
                <p className={styles.note}>Milestones could not be loaded.</p>
                <Button size="compact" variant="secondary" onClick={read.retry}>
                  Retry milestones
                </Button>
              </div>
            )}
            {milestones && milestones.total > milestones.pageSize && (
              <DataTablePagination
                page={{
                  page: milestones.page,
                  pageSize: milestones.pageSize,
                  total: milestones.total,
                }}
                count={milestones.items.length}
                disabled={read.busy}
                onPage={(next, size) => {
                  setPage(size === pageSize ? next : 1);
                  setPageSize(size);
                }}
              />
            )}
          </>
        }
      >
        <StaffTimeline>
          {milestones?.items.map((item) => (
            <StaffTimelineEvent
              key={`${item.kind}:${item.recordId}`}
              title={milestoneLabels[item.kind] ?? item.kind}
              at={item.at}
              context={<span>{item.branchName}</span>}
            >
              <p>
                {item.hasAccess ? (
                  <a href={milestoneHref(item)}>{item.reference || "Open record"}</a>
                ) : (
                  <span>No access, {item.reference}</span>
                )}
                {item.agreedTotal !== null && `, ${formatCurrency(item.agreedTotal)} agreed`}
              </p>
            </StaffTimelineEvent>
          ))}
        </StaffTimeline>
      </StaffHistorySection>
    </div>
  );
}
