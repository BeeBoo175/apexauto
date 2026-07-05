"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import {
  parseSalesTeam,
  parseWorkshopTeam,
  teamTabs,
  type SalesRow,
  type TeamPage,
  type TeamPerson,
  type TeamTab,
  type WorkshopRow,
} from "@/lib/performance/team";
import { PerformanceDrawer } from "./PerformanceDrawer";
import { PerformanceSection } from "./PerformanceSection";
import { salesColumns, teamTitles, workshopColumns, type TeamColumn } from "./TeamColumns";
import { TeamTable } from "./TeamTable";
import styles from "./Performance.module.css";

const pageSize = 25;

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

// Roles are never compared in one view, so each tab reads and shows only its own measures.
export function TeamOutputSection({
  period,
  actor,
  refreshes,
  asTable,
}: {
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
  asTable: boolean;
}) {
  const [tab, setTab] = useState<TeamTab>("sales");
  return (
    <div className={styles.section}>
      <div className={styles.presets} role="group" aria-label="Team output role">
        {teamTabs.map((item) => (
          <Button
            key={item.value}
            size="compact"
            variant={tab === item.value ? "secondary" : "ghost"}
            aria-pressed={tab === item.value}
            onClick={() => setTab(item.value)}
          >
            {item.label}
          </Button>
        ))}
      </div>
      {tab === "sales" ? (
        <TeamTabContent
          key="sales"
          tab="sales"
          parse={parseSalesTeam}
          columns={salesColumns}
          period={period}
          actor={actor}
          refreshes={refreshes}
          asTable={asTable}
        />
      ) : (
        <TeamTabContent
          key="workshop"
          tab="workshop"
          parse={parseWorkshopTeam}
          columns={workshopColumns}
          period={period}
          actor={actor}
          refreshes={refreshes}
          asTable={asTable}
        />
      )}
    </div>
  );
}

function TeamTabContent<Row extends SalesRow | WorkshopRow>({
  tab,
  parse,
  columns,
  period,
  actor,
  refreshes,
  asTable,
}: {
  tab: TeamTab;
  parse: (value: unknown) => TeamPage<Row> | null;
  columns: TeamColumn<Row>[];
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
  asTable: boolean;
}) {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Row | null>(null);
  const read = usePerformanceSection(
    `team/${tab}`,
    parse,
    period,
    actor,
    refreshes,
    `${teamTitles[tab]} team output`,
    true,
    `page=${page}&pageSize=${pageSize}`,
  );
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
  const pages = people ? Math.max(1, Math.ceil(people.total / people.pageSize)) : 1;
  const state = {
    hasData: Boolean(people),
    busy: read.busy,
    failed: read.failed,
    retry: read.retry,
  };
  return (
    <>
      <PerformanceSection title={`Team output, ${teamTitles[tab]}`} state={state}>
        {people && people.items.length === 0 && (
          <p className={styles.note}>No one to show for this period and branch.</p>
        )}
        {people && people.items.length > 0 && (
          <TeamTable
            rows={people.items}
            columns={columns}
            bars={!asTable}
            caption={`${teamTitles[tab]} team output for the period`}
            onOpen={setSelected}
          />
        )}
        <div className={styles.tabs}>
          <div className={styles.pager}>
            <Button
              size="compact"
              variant="secondary"
              disabled={page <= 1 || read.busy}
              onClick={() => setPage(page - 1)}
            >
              Previous
            </Button>
            <span>
              Page {page} of {pages}
            </span>
            <Button
              size="compact"
              variant="secondary"
              disabled={page >= pages || read.busy}
              onClick={() => setPage(page + 1)}
            >
              Next
            </Button>
          </div>
          <Button
            size="compact"
            variant="secondary"
            disabled={exporter.busy || !people || people.total === 0}
            onClick={() => void exporter.exportAll()}
          >
            {exporter.busy ? "Preparing CSV…" : "Export team output CSV"}
          </Button>
        </div>
        <p className={styles.note}>
          People appear alphabetically within each branch, with no ranking. Results belong to the
          branch that recorded them. Now counts show only for people at their current branch.
        </p>
      </PerformanceSection>
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
