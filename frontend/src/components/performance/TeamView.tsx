"use client";

import { useEffect, useEffectEvent, useRef, useState } from "react";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { ChoiceColumnFilter } from "@/components/ui/data-table/ChoiceColumnFilter";
import { TextColumnFilter } from "@/components/ui/data-table/ColumnFilterForm";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { privateApiFetch } from "@/lib/api-browser";
import { roleNames, type CurrentActor, type Role } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { usePerformanceAddressContext } from "@/hooks/use-performance-address";
import { branchLabel } from "@/lib/branches";
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
import {
  appliedTeamFilters,
  statusChoices,
  teamQuery,
  teamUrlValues,
  type TeamFilters,
  type TeamUrlState,
} from "@/lib/performance/team-filters";
import { TeamTableToolbar } from "./TeamTableToolbar";
import { TeamCharts, teamStats, type OverviewLabels } from "./TeamOverview";
import { PerformanceDrawer } from "./PerformanceDrawer";
import { DashboardSection } from "@/components/staff/DashboardSection";
import { RowsSkeleton } from "@/components/staff/DashboardSkeletons";
import { salesColumns, teamTitles, workshopColumns, type TeamColumn } from "./team-columns";
import styles from "./Performance.module.css";

// What the page toolbar needs to offer this table's export.
export interface TeamExport {
  run: () => void;
  busy: boolean;
  disabled: boolean;
}

interface TeamProps {
  // Only the shown view writes its table to the address.
  active: boolean;
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
  // The search, filters, sort, page and open person the address names for this tab.
  urlState: TeamUrlState;
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
  sales: {
    volume: "Cars delivered",
    days: "Average days to deliver",
    scatter: "Deliveries and time to deliver",
    spread: "Deliveries per person",
  },
  workshop: {
    volume: "Inspections signed",
    days: "Average days to sign off",
    scatter: "Inspections and time to sign off",
    spread: "Inspections per person",
  },
};

function personDetail(row: TeamPerson, showBranch: boolean) {
  const role = roleNames[row.role as Role] ?? row.role;
  const parts = [showBranch ? `${role}, ${branchLabel(row.branchName)}` : role];
  if (row.status !== "Active") {
    parts.push(personStatusLabels[row.status]);
  }
  return parts.join(", ");
}

// One team's output as bars: pick a measure, read it for every person in alphabetical order on one shared scale,
// open a person for their period detail and milestones. The CSV export in the page toolbar carries every measure.
function TeamTable<Row extends SalesRow | WorkshopRow>({
  tab,
  active,
  parse,
  columns,
  period,
  actor,
  refreshes,
  onExport,
  initial,
  urlState,
  onSettled,
}: TeamProps & {
  tab: TeamTab;
  onExport: (tab: TeamTab, state: TeamExport | null) => void;
  initial: unknown;
  urlState: TeamUrlState;
  onSettled: () => void;
  parse: (value: unknown) => TeamPage<Row> | null;
  columns: TeamColumn<Row>[];
}) {
  const measures = columns.filter((column) => !column.detailOnly);
  const [page, setPage] = useState(urlState.page);
  const [pageSize, setPageSize] = useState(urlState.pageSize);
  const [filters, setFilters] = useState<TeamFilters>(urlState.filters);
  // Any change to what the table shows starts again at the first page.
  function change(next: TeamFilters) {
    setFilters(next);
    setPage(1);
  }
  const [selected, setSelected] = useState<Row | null>(null);
  const [section, setSection] = useState(urlState.section);
  // The entry parsed this page with the same parser for this tab.
  const [initialPage] = useState(() => initial as TeamPage<Row> | null);
  const read = usePerformanceSection({
    section: `team/${tab}`,
    parse,
    period,
    actor,
    refreshes,
    label: `${teamTitles[tab]} team output`,
    extra: `page=${page}&pageSize=${pageSize}&${teamQuery(filters)}`,
    initial: initialPage,
  });
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
        `/api/staff/performance/team/${tab}?${periodQuery(period)}&page=${number}&pageSize=${size}&${teamQuery(filters)}`,
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
  const busy = read.busy;
  const labelOf = (measure: string) =>
    measures.find((column) => column.key === measure)?.label ?? measure;
  const minimumFilter = (column: TeamColumn<Row>) => ({
    active: filters.minimums[column.key] !== undefined,
    content: (
      <TextColumnFilter
        label="At least"
        value={filters.minimums[column.key]?.toString() ?? ""}
        busy={busy}
        onApply={async (value) => {
          const count = Number.parseInt(value, 10);
          const { [column.key]: removed, ...rest } = filters.minimums;
          void removed;
          change({
            ...filters,
            minimums:
              Number.isInteger(count) && count >= 0 ? { ...rest, [column.key]: count } : rest,
          });
          return true;
        }}
      />
    ),
  });
  const tableColumns: DataColumn<Row>[] = [
    {
      id: "name",
      label: "Person",
      width: 280,
      sortable: true,
      filter: {
        active: filters.name !== "",
        content: (
          <TextColumnFilter
            label="Name contains"
            value={filters.name}
            busy={busy}
            onApply={async (value) => {
              change({ ...filters, name: value });
              return true;
            }}
          />
        ),
      },
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
    {
      id: "status",
      label: "Status",
      width: 128,
      sortable: true,
      filter: {
        active: filters.status !== "",
        content: (
          <ChoiceColumnFilter
            label="Status"
            options={statusChoices}
            value={filters.status}
            empty=""
            busy={busy}
            onApply={async (value) => {
              change({ ...filters, status: value });
              return true;
            }}
          />
        ),
      },
      cell: (row) => personStatusLabels[row.status] ?? row.status,
    },
    ...measures.map((column): DataColumn<Row> => ({
      id: column.key,
      label: column.label,
      align: "right",
      sortable: true,
      // Counts can be limited to people with at least some; elapsed days can only be sorted.
      filter: column.bar && !column.now ? minimumFilter(column) : undefined,
      cell: (row) => column.text(row),
    })),
  ];
  // A dot for someone on another page jumps to that page first, then opens them once it has loaded.
  const [pendingOpen, setPendingOpen] = useState<string | null>(urlState.person || null);
  const drawerRow =
    selected ?? rows.find((row) => `${row.personId}:${row.branchId}` === pendingOpen) ?? null;
  // What the table shows rides in the address, like the other staff pages: search, filters, sort, page and the open
  // person. Leaving the tab clears it.
  const drawerKey = drawerRow ? `${drawerRow.personId}:${drawerRow.branchId}` : (pendingOpen ?? "");
  const address = usePerformanceAddressContext();
  const writeAddress = useEffectEvent(() =>
    address.patch(teamUrlValues({ filters, page, pageSize, person: drawerKey, section }), "push"),
  );
  useEffect(() => {
    if (active) {
      writeAddress();
    }
  }, [active, filters, page, pageSize, drawerKey, section]);
  function openRow(row: Row) {
    setSection("summary");
    setSelected(row);
  }
  function openPoint(point: TeamPoint) {
    const key = `${point.personId}:${point.branchId}`;
    const found = rows.find((row) => `${row.personId}:${row.branchId}` === key);
    if (found) {
      openRow(found);
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
          stats={teamStats(people?.points ?? [], people?.points.length ?? 0, overviewLabels[tab])}
          state={sectionState}
        />
        <TeamCharts
          points={people?.points ?? []}
          labels={overviewLabels[tab]}
          state={sectionState}
          onOpen={openPoint}
        />
        <DashboardSection
          title="People"
          state={sectionState}
          placeholder={<RowsSkeleton count={5} tall />}
        >
          <DataTable
            label={`${teamTitles[tab]} team output`}
            density="comfortable"
            sorting={{
              value: filters.sort,
              onChange: (sort) => change({ ...filters, sort }),
            }}
            toolbar={
              <TeamTableToolbar
                filters={filters}
                applied={appliedTeamFilters(filters, labelOf)}
                busy={busy}
                onChange={change}
              />
            }
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
              title: "No one matches",
              description: "Change the search or filters, or choose another period or branch.",
            }}
            onPage={(next, size) => {
              setPage(size === pageSize ? next : 1);
              setPageSize(size);
            }}
            selectedRowId={drawerRow ? `${drawerRow.personId}:${drawerRow.branchId}` : undefined}
            onRowClick={openRow}
          />
        </DashboardSection>
      </div>
      <PerformanceDrawer
        row={drawerRow}
        columns={columns}
        period={period}
        refreshes={refreshes}
        section={section}
        onSection={setSection}
        onClose={() => {
          setSelected(null);
          setPendingOpen(null);
          setSection("summary");
        }}
      />
    </>
  );
}
