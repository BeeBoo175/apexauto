"use client";

import { useState } from "react";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useAddressSection } from "@/hooks/use-address-section";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { usePerformanceSection } from "@/hooks/use-performance-section";
import { privateApiFetch } from "@/lib/api-browser";
import { roleNames, type CurrentActor, type Role } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
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
import { formatCurrency } from "@/lib/currency-format";
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

const milestonePageSize = 10;

export function PerformanceDrawer<Row extends TeamPerson>({
  row,
  columns,
  period,
  actor,
  refreshes,
  onClose,
}: {
  row: Row | null;
  columns: TeamColumn<Row>[];
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
  onClose: () => void;
}) {
  const [section, chooseSection] = useAddressSection<Section>(["summary", "milestones"]);
  const [opened, setOpened] = useState(section === "milestones");
  return (
    <Dialog
      open={Boolean(row)}
      variant="drawer"
      density="compact"
      title={row?.name ?? "Employee"}
      onClose={onClose}
    >
      {row && (
        <div className={workbench.workspace}>
          <StaffRecordSummary
            ariaLabel="Role, branch and status"
            label={row.branchName}
            value={roleNames[row.role as Role] ?? row.role}
            lead={
              <Avatar
                accountId={row.personId}
                name={row.name}
                pictureId={row.pictureId}
                size="lg"
              />
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
                    <dt>{column.label}</dt>
                    <dd>{column.text(row)}</dd>
                  </div>
                ))}
              </dl>
              <p className={styles.note}>
                Results belong to the branch that recorded them. Elapsed times are calendar days,
                not hours worked.
              </p>
            </div>
          )}
          {opened && (
            <Milestones
              hidden={section !== "milestones"}
              person={row}
              period={period}
              actor={actor}
              refreshes={refreshes}
            />
          )}
        </div>
      )}
    </Dialog>
  );
}

function Milestones({
  hidden,
  person,
  period,
  actor,
  refreshes,
}: {
  hidden: boolean;
  person: TeamPerson;
  period: PerformancePeriod;
  actor: CurrentActor;
  refreshes: number;
}) {
  const [page, setPage] = useState(1);
  const path = `people/${person.personId}/milestones`;
  const read = usePerformanceSection(
    path,
    parseMilestonePage,
    period,
    actor,
    refreshes,
    "Milestones",
    true,
    `page=${page}&pageSize=${milestonePageSize}`,
  );
  const exporter = usePerformanceExport<Milestone>(
    `${person.name.toLowerCase().replace(/[^a-z]+/g, "-")}-milestones`,
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
  const milestones = read.data;
  const pages = milestones ? Math.max(1, Math.ceil(milestones.total / milestones.pageSize)) : 1;
  return (
    <div className={styles.milestonePanel} hidden={hidden}>
      <div className={`${workbench.body} ${styles.drawerBody}`}>
        {!milestones && read.failed && (
          <div className={styles.retry}>
            <p className={styles.note}>Milestones could not be loaded.</p>
            <Button size="compact" variant="secondary" onClick={read.retry}>
              Retry milestones
            </Button>
          </div>
        )}
        {!milestones && !read.failed && (
          <div role="status" className={styles.shellRows} aria-label="Loading milestones">
            <span className="sr-only">Loading milestones…</span>
            {[0, 1, 2, 3].map((index) => (
              <span key={index} className={`${styles.placeholder} ${styles.shellBlock}`} />
            ))}
          </div>
        )}
        {milestones && milestones.items.length === 0 && (
          <p className={styles.note}>No completed work in this period.</p>
        )}
        {milestones && milestones.items.length > 0 && (
          <ul className={styles.milestones}>
            {milestones.items.map((item) => (
              <li key={`${item.kind}:${item.recordId}`} className={styles.milestone}>
                <span className={styles.milestoneKind}>{milestoneLabels[item.kind]}</span>
                <span>
                  {item.hasAccess ? (
                    <a href={milestoneHref(item)}>{item.reference || "Open record"}</a>
                  ) : (
                    <span>No access, {item.reference}</span>
                  )}
                </span>
                <span className={styles.personFact}>
                  {formatMalaysianDateTime(item.at)}, {item.branchName}
                  {item.agreedTotal !== null && `, ${formatCurrency(item.agreedTotal)}`}
                </span>
              </li>
            ))}
          </ul>
        )}
        {milestones && pages > 1 && (
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
        )}
      </div>
      <StaffWorkspaceFooter title="Export" description="Delivered sales carry their agreed total.">
        <Button
          size="compact"
          variant="secondary"
          disabled={exporter.busy || !milestones || milestones.total === 0}
          onClick={() => void exporter.exportAll()}
        >
          {exporter.busy ? "Preparing CSV…" : "Export milestones CSV"}
        </Button>
      </StaffWorkspaceFooter>
    </div>
  );
}
