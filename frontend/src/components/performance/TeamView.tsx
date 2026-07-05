"use client";

import { useEffect, useRef, useState } from "react";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { privateApiFetch } from "@/lib/api-browser";
import { roleNames, type CurrentActor, type Role } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import {
  parseSalesTeam,
  parseWorkshopTeam,
  personStatusLabels,
  type SalesRow,
  type TeamPage,
  type TeamPerson,
  type TeamTab,
  type WorkshopRow,
} from "@/lib/performance/team";
import { MeasureGrid } from "./MeasureBars";
import { DotPlot, HighlightMatrix } from "./TeamCharts";
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
  const chartPeople = rows.map((row) => ({
    key: `${row.personId}:${row.branchId}`,
    name: row.name,
    detail: personDetail(row, showBranch),
    onOpen: () => setSelected(row),
  }));
  // Each kind of measure gets the chart that suits it: counts of completed work in a highlight matrix, elapsed days
  // on a dot plot, and work held now as bars.
  const counted = measures.filter((column) => column.bar && !column.now);
  const elapsed = measures.filter((column) => !column.bar);
  const held = measures.filter((column) => column.now);
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
        <PerformanceSection
          title="Completed work in the period"
          aside={
            people && (
              <span className={styles.panelAside}>
                {people.total} {people.total === 1 ? "person" : "people"} in alphabetical order,
                darker cells did more of that measure, open a person for their detail
              </span>
            )
          }
          state={sectionState}
          placeholder={<RowsSkeleton count={5} tall />}
        >
          <HighlightMatrix
            caption={`${teamTitles[tab]} completed work per person`}
            people={chartPeople}
            rows={rows}
            measures={counted.map((column) => ({
              key: column.key,
              label: column.label,
              value: column.value,
              text: column.text,
            }))}
          />
        </PerformanceSection>
        <div className={styles.grid}>
          {elapsed.map((column) => (
            <PerformanceSection
              key={column.key}
              title={column.label}
              half
              aside={<span className={styles.panelAside}>Calendar days, not hours worked</span>}
              state={sectionState}
              placeholder={<RowsSkeleton count={3} />}
            >
              <DotPlot
                caption={`${column.label} for each person, in days`}
                people={chartPeople}
                rows={rows}
                value={column.value}
                text={column.text}
              />
            </PerformanceSection>
          ))}
          <PerformanceSection
            title="Holding now"
            half
            aside={<span className={styles.panelAside}>Open work today, whatever the period</span>}
            state={sectionState}
            placeholder={<RowsSkeleton count={3} />}
          >
            <MeasureGrid
              empty="Nobody holds open work."
              panels={held.map((column) => ({
                key: column.key,
                title: column.label,
                items: rows.map((row, index) => ({
                  key: chartPeople[index].key,
                  label: row.name,
                  value: column.value(row),
                  text: column.text(row),
                  onOpen: chartPeople[index].onOpen,
                  openLabel: `${row.name}, ${column.label} now ${column.text(row)}. Open their period detail`,
                })),
              }))}
            />
          </PerformanceSection>
        </div>
      </div>
      <div className={styles.pagerFrame}>
        <DataTablePagination
          page={{
            page: people?.page ?? page,
            pageSize: people?.pageSize ?? pageSize,
            total: people?.total ?? 0,
          }}
          count={people?.items.length ?? 0}
          disabled={read.busy}
          onPage={(next, size) => {
            setPage(size === pageSize ? next : 1);
            setPageSize(size);
          }}
        />
      </div>
      <PerformanceDrawer
        row={selected}
        columns={columns}
        period={period}
        actor={actor}
        refreshes={refreshes}
        onClose={() => setSelected(null)}
      />
    </>
  );
}
