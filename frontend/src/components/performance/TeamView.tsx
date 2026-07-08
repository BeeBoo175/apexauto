"use client";

import { useEffect, useRef, useState } from "react";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { privateApiFetch } from "@/lib/api-browser";
import { roleNames, type CurrentActor, type Role } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { rangeText } from "@/lib/performance/format";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import {
  parseSalesTeam,
  parseWorkshopTeam,
  personStatusLabels,
  type SalesRow,
  type TeamPage,
  type TeamPoint,
  type TeamPerson,
  type TeamTab,
  type WorkshopRow,
} from "@/lib/performance/team";
import { HeadlineFigures } from "./HeadlineFigures";
import { TeamCharts, teamStats, type OverviewLabels } from "./TeamOverview";
import { PerformanceDrawer } from "./PerformanceDrawer";
import { PerformanceSection } from "./PerformanceSection";
import { RowsSkeleton } from "./PerformanceSkeletons";
import { salesColumns, teamTitles, workshopColumns, type TeamColumn } from "./TeamColumns";
import styles from "./Performance.module.css";

// What the page toolbar needs to offer this table's export.
export interface TeamExport {
  run: () => void;
  busy: boolean;
  disabled: boolean;
}

interface TeamProps {
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
}

// Team output exports carry no money columns: values are counts and elapsed days only.
function csvFor<Row extends TeamPerson>(columns: TeamColumn<Row>[]): CsvColumn<Row>[] {
  return [
    { label: "Name", value: (row) => row.name },
    { label: "Branch", value: (row) => row.branchName },
    { label: "Status", value: (row) => row.status },
    ...columns.map((column) => ({
      label: column.label,
      value: (row: Row) => column.value(row)?.toString() ?? "",
    })),
  ];
}

// Roles are never compared in one view, so each role has its own view and reads only its own measures.
export function TeamView({
  tab,
  ...props
}: TeamProps & {
  tab: TeamTab;
  onExport: (tab: TeamTab, state: TeamExport | null) => void;
  // What the page entry already read for this tab, so a direct entry opens complete.
  initial: unknown;
  // Called once the first figures (or a failure) are in, so the page can swap to this view without a gap.
  onSettled: () => void;
}) {
  if (tab === "sales") {
    return (
      <TeamTable key="sales" tab="sales" parse={parseSalesTeam} columns={salesColumns} {...props} />
    );
  }
  return (
    <TeamTable
      key="workshop"
      tab="workshop"
      parse={parseWorkshopTeam}
      columns={workshopColumns}
      {...props}
    />
  );
}

const overviewLabels: Record<TeamTab, OverviewLabels> = {
  sales: { volume: "Cars delivered", days: "Average days to deliver" },
  workshop: { volume: "Inspections signed", days: "Average days to sign off" },
};

function personDetail(row: TeamPerson, showBranch: boolean) {
  const role = roleNames[row.role as Role] ?? row.role;
  const parts = [showBranch ? `${role}, ${row.branchName}` : role];
  if (row.status !== "Active") {
    parts.push(personStatusLabels[row.status]);
  }
  return parts.join(", ");
}

// One team's output as bars: pick a measure, read it for every person in alphabetical order on one shared scale,
// open a person for their period detail and milestones. The CSV export in the page toolbar carries every measure.
function TeamTable<Row extends SalesRow | WorkshopRow>({
  tab,
  parse,
  columns,
  period,
  actor,
  refreshes,
  onExport,
  initial,
  onSettled,
}: TeamProps & {
  tab: TeamTab;
  onExport: (tab: TeamTab, state: TeamExport | null) => void;
  initial: unknown;
  onSettled: () => void;
  parse: (value: unknown) => TeamPage<Row> | null;
  columns: TeamColumn<Row>[];
}) {
  const measures = columns.filter((column) => !column.detailOnly);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [selected, setSelected] = useState<Row | null>(null);
  // The entry parsed this page with the same parser for this tab.
  const [initialPage] = useState(() => initial as TeamPage<Row> | null);
  const read = usePerformanceSection(
    `team/${tab}`,
    parse,
    period,
    actor,
    refreshes,
    `${teamTitles[tab]} team output`,
    true,
    `page=${page}&pageSize=${pageSize}`,
    initialPage,
  );
  const settled = read.data !== null || (read.failed && !read.busy);
  useEffect(() => {
    if (settled) {
      onSettled();
    }
  }, [settled, onSettled]);
  const exporter = usePerformanceExport<Row>(
    `team-output-${tab}`,
    csvFor(columns),
    async (number, size, signal) => {
      const result = await privateApiFetch(
        `/api/staff/performance/team/${tab}?${periodQuery(period)}&page=${number}&pageSize=${size}`,
        parse,
        { signal },
      );
      return { status: result.status, data: result.data };
    },
    (row) => `${row.personId}:${row.branchId}`,
    `${teamTitles[tab]} team output`,
  );
  const people = read.data;
  const exportDisabled = !people || people.total === 0;
  const { busy: exportBusy, exportAll } = exporter;
  const latestExport = useRef(exportAll);
  useEffect(() => {
    latestExport.current = exportAll;
  });
  useEffect(() => {
    onExport(tab, {
      run: () => void latestExport.current(),
      busy: exportBusy,
      disabled: exportDisabled,
    });
    return () => onExport(tab, null);
  }, [onExport, tab, exportBusy, exportDisabled]);
  const showBranch = actor.role === "GeneralManager";
  const rows = people?.items ?? [];
  const tableColumns: DataColumn<Row>[] = [
    {
      id: "person",
      label: "Person",
      width: 280,
      cell: (row) => (
        <PersonLabel
          id={row.personId}
          name={row.name}
          identity={personDetail(row, showBranch)}
          pictureId={row.pictureId}
          nested
        />
      ),
    },
    ...measures.map((column): DataColumn<Row> => ({
      id: column.key,
      label: column.label,
      align: "right",
      cell: (row) => column.text(row),
    })),
  ];
  // A dot for someone on another page jumps to that page first, then opens them once it has loaded.
  const [pendingOpen, setPendingOpen] = useState<string | null>(null);
  const drawerRow =
    selected ?? rows.find((row) => `${row.personId}:${row.branchId}` === pendingOpen) ?? null;
  function openPoint(point: TeamPoint) {
    const key = `${point.personId}:${point.branchId}`;
    const found = rows.find((row) => `${row.personId}:${row.branchId}` === key);
    if (found) {
      setSelected(found);
      return;
    }
    const index =
      people?.points.findIndex((item) => `${item.personId}:${item.branchId}` === key) ?? -1;
    if (index >= 0) {
      setPendingOpen(key);
      setPage(Math.floor(index / pageSize) + 1);
    }
  }
  const sectionState = {
    hasData: people !== null,
    busy: read.busy,
    failed: read.failed,
    retry: read.retry,
  };
  return (
    <>
      <LoadingProgress label="Loading team output" active={read.busy} />
      <div className={styles.view}>
        <HeadlineFigures
          stats={teamStats(people?.points ?? [], people?.total ?? 0, overviewLabels[tab])}
          covers={rangeText(period.from, period.to)}
          state={sectionState}
        />
        <TeamCharts
          points={people?.points ?? []}
          labels={overviewLabels[tab]}
          state={sectionState}
          onOpen={openPoint}
        />
        <PerformanceSection
          title="Everyone, in alphabetical order"
          state={sectionState}
          placeholder={<RowsSkeleton count={5} tall />}
        >
          <DataTable
            label={`${teamTitles[tab]} team output`}
            density="comfortable"
            rows={rows}
            rowId={(row) => `${row.personId}:${row.branchId}`}
            columns={tableColumns}
            page={{
              page: people?.page ?? page,
              pageSize: people?.pageSize ?? pageSize,
              total: people?.total ?? 0,
            }}
            disabled={false}
            busy={read.busy}
            emptyState={{
              title: "No one in this team",
              description: "No one in this team matches this period and branch.",
            }}
            onPage={(next, size) => {
              setPage(size === pageSize ? next : 1);
              setPageSize(size);
            }}
            onRowClick={setSelected}
          />
        </PerformanceSection>
      </div>
      <PerformanceDrawer
        row={drawerRow}
        columns={columns}
        period={period}
        actor={actor}
        refreshes={refreshes}
        onClose={() => {
          setSelected(null);
          setPendingOpen(null);
        }}
      />
    </>
  );
}
