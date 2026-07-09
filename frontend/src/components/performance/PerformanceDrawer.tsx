"use client";

import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import { StaffHistoryProgress } from "@/components/staff/StaffHistoryProgress";
import { StaffHistorySection } from "@/components/staff/StaffHistorySection";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffTimeline, StaffTimelineEvent } from "@/components/staff/StaffTimeline";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useAddressSection } from "@/hooks/use-address-section";
import { usePerformanceExport } from "@/hooks/use-performance-export";
import { useStaffHistory } from "@/hooks/use-staff-history";
import type { ApiRequest } from "@/lib/api";
import { privateApiFetch } from "@/lib/api-browser";
import { roleNames, type Role } from "@/lib/auth/actor";
import type { CsvColumn } from "@/lib/csv";
import { formatCurrency } from "@/lib/currency-format";
import { branchLabel } from "@/lib/performance/format";
import { periodQuery } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import {
  milestoneHref,
  milestoneLabels,
  appendMilestones,
  parseMilestoneHistory,
  parseMilestonePage,
  personStatusLabels,
  type Milestone,
  type MilestoneHistory,
  type TeamPerson,
} from "@/lib/performance/team";
import type { TeamColumn } from "./TeamColumns";
import shared from "@/components/staff/StaffDetails.module.css";
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

// The This period tab groups the measures the way a reader asks about them, each group titled like the sections of the
// other drawers.
const measureGroups = [
  { title: "Completed work", includes: (column: TeamColumn<never>) => column.bar && !column.now },
  { title: "Time taken", includes: (column: TeamColumn<never>) => !column.bar },
  { title: "Held now", includes: (column: TeamColumn<never>) => Boolean(column.now) },
];

interface DrawerProps<Row extends TeamPerson> {
  columns: TeamColumn<Row>[];
  period: PerformancePeriod;
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
      <div className={shared.panel} hidden={section !== "summary"}>
        <div className={`${workbench.body} ${shared.content}`}>
          {measureGroups.map((group) => {
            const shown = columns.filter(group.includes);
            return (
              shown.length > 0 && (
                <section key={group.title} className={shared.section} aria-label={group.title}>
                  <div className={workbench.sectionHeader}>
                    <h2 className={workbench.sectionTitle}>{group.title}</h2>
                  </div>
                  <dl className={shared.details}>
                    {shown.map((column) => (
                      <Fragment key={column.key}>
                        <dt>{column.label}</dt>
                        <dd className={column.value(row) === null ? styles.absent : undefined}>
                          {column.text(row)}
                        </dd>
                      </Fragment>
                    ))}
                  </dl>
                </section>
              )
            );
          })}
          <p className={styles.note}>
            Results belong to the branch that recorded them. Elapsed times are calendar days, not
            hours worked.
          </p>
        </div>
      </div>
      {opened && (
        <div className={shared.panel} hidden={section !== "milestones"}>
          <div className={`${workbench.body} ${shared.content}`}>
            <Milestones
              personId={row.personId}
              period={period}
              refreshes={refreshes}
              enabled={section === "milestones"}
            />
          </div>
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

// The person's completed work in the period on the shared history timeline, newest first. The shared history reveals
// older entries on request, like every other record history, so a busy person never loads hundreds at once.
function Milestones({
  personId,
  period,
  refreshes,
  enabled,
}: {
  personId: string;
  period: PerformancePeriod;
  refreshes: number;
  enabled: boolean;
}) {
  const resource = useMemo(
    () => ({
      path: `people/${personId}/milestones`,
      parse: parseMilestoneHistory,
      matches: (value: MilestoneHistory) => value.personId === personId,
      append: appendMilestones,
      // The history adds its page to the path; the period belongs on every read.
      read: ((url, parse, options) =>
        privateApiFetch(
          `/api/staff/performance/${url}&${periodQuery(period)}`,
          parse,
          options,
        )) as ApiRequest,
    }),
    [personId, period],
  );
  const read = useStaffHistory(
    null,
    resource,
    "Milestones could not be loaded. Confirmed entries remain shown.",
    { enabled, progressive: true },
  );
  const { history, refresh } = read;
  // A refresh of the page reads the milestones again.
  const seen = useRef(refreshes);
  useEffect(() => {
    if (seen.current !== refreshes) {
      seen.current = refreshes;
      void refresh();
    }
  }, [refreshes, refresh]);
  return (
    <StaffHistorySection
      label="Milestones"
      busy={read.busy}
      flush
      empty={history?.totalCount === 0 && <p>No completed work in this period.</p>}
      progress={<StaffHistoryProgress read={read} label="Milestones" />}
    >
      <StaffTimeline>
        {history?.items.map((item) => (
          <StaffTimelineEvent
            key={`${item.kind}:${item.recordId}`}
            title={milestoneLabels[item.kind] ?? item.kind}
            at={item.at}
            context={<span>{branchLabel(item.branchName)}</span>}
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
  );
}
