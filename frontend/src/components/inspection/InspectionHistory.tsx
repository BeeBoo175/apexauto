"use client";
import { useInspectionHistory } from "@/hooks/use-inspection-history";
import { StaffLink } from "@/components/staff/StaffLink";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { parseRevisions, parseRepairs } from "@/lib/inspection/parse-report";
import type { RevisionHistory, RepairHistory } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import { Button } from "@/components/ui/Button";
import placeholders from "@/components/staff/AppointmentLoadingShell.module.css";
import timeline from "./InspectionTimeline.module.css";
import type { RepairSelection } from "@/lib/inspection/query";

export function InspectionRepairLink({
  inspectionId,
  selection,
  disabled,
  onSelect,
}: {
  inspectionId: string;
  selection: RepairSelection;
  disabled: boolean;
  onSelect: (selection: RepairSelection) => void;
}) {
  return (
    <StaffLink
      prefetch={false}
      href={`/dashboard/inspection?inspection=${inspectionId}&section=repairs&request=${selection.requestId}&revision=${selection.revision}`}
      aria-disabled={disabled || undefined}
      onClick={(event) => {
        if (disabled) {
          event.preventDefault();
          return;
        }
        if (
          event.button !== 0 ||
          event.ctrlKey ||
          event.metaKey ||
          event.shiftKey ||
          event.altKey
        ) {
          return;
        }
        // Local history selection keeps current form drafts and command receipts mounted.
        event.preventDefault();
        onSelect(selection);
      }}
    >
      Open repair request
    </StaffLink>
  );
}

export function InspectionHistoryProgress({
  read,
  label,
  disabled,
}: {
  read: ReturnType<typeof useInspectionHistory>;
  label: string;
  disabled: boolean;
}) {
  const { history, busy, failed, load, retry } = read;
  if (!history && busy) {
    return (
      <div role="status" aria-label={`Loading ${label}`}>
        <ul className={timeline.repairs} aria-hidden="true">
          {[0, 1, 2].map((row) => (
            <li key={row}>
              <span className={`${placeholders.placeholder} ${placeholders.status}`} />
              <span className={`${placeholders.placeholder} ${placeholders.value}`} />
            </li>
          ))}
        </ul>
      </div>
    );
  }
  if (failed) {
    return (
      <div>
        <p role="alert">{label} could not be loaded. Revealed history remains shown.</p>
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled || busy}
          onClick={() => void retry()}
        >
          Retry {label.toLowerCase()}
        </Button>
      </div>
    );
  }
  if (!history || history.page * history.pageSize >= history.totalCount) {
    return null;
  }
  return (
    <Button
      size="compact"
      variant="secondary"
      disabled={disabled || busy}
      aria-busy={busy}
      onClick={() => void load(history.page + 1, history.pageSize)}
    >
      Load older {label.toLowerCase()}
    </Button>
  );
}

export function RevisionHistoryView({
  initial,
  disabled = false,
}: {
  initial: RevisionHistory;
  disabled?: boolean;
}) {
  const { history, busy, load } = useInspectionHistory(
    initial,
    `reports/${initial.inspectionId}/revisions`,
    parseRevisions,
    "Revision history could not be refreshed. Existing revisions remain shown.",
  );
  if (!history) {
    return null;
  }
  return (
    <section
      className={`${styles.section} ${workbench.protocol}`}
      aria-label="Signed revision history"
      aria-busy={busy}
    >
      <div className={workbench.toolbar}>
        <h2 className={workbench.sectionTitle}>Signed revisions</h2>
      </div>
      {!history.items.length && <p>No signed revisions on this page.</p>}
      {history.items.map((item) => (
        <div className={styles.historyRow} key={item.operationId}>
          <StaffLink
            prefetch={false}
            target="_blank"
            rel="noopener"
            href={`/dashboard/inspection/${history.inspectionId}/reports/${item.revision}`}
          >
            Revision {item.revision} ·{" "}
            {item.assessment.isRejected ? "Rejected" : `Grade ${item.assessment.grade}`}
          </StaffLink>
          <span>
            {item.technicianIdentity} · {formatMalaysianDateTime(item.signedAt)}
          </span>
          {item.reopening && (
            <p>
              Reopened {formatMalaysianDateTime(item.reopening.recordedAt)} ·{" "}
              {item.reopening.reason}
            </p>
          )}
        </div>
      ))}
      <DataTablePagination
        page={{ ...history, total: history.totalCount }}
        count={history.items.length}
        disabled={disabled || busy}
        onPage={(p, size) => void load(p, size)}
      />
    </section>
  );
}

export function RepairHistoryView({
  initial,
  disabled = false,
}: {
  initial: RepairHistory;
  disabled?: boolean;
}) {
  const { history, busy, load } = useInspectionHistory(
    initial,
    `reports/${initial.inspectionId}/revisions/${initial.revision}/repair-history`,
    parseRepairs,
    "Repair history could not be refreshed. Existing receipts remain shown.",
  );
  if (!history) {
    return null;
  }
  return (
    <section
      className={`${styles.section} ${workbench.protocol}`}
      aria-label="Retained repair history"
      aria-busy={busy}
    >
      <div className={workbench.toolbar}>
        <h2 className={workbench.sectionTitle}>Retained repair history</h2>
      </div>
      <p className={styles.help}>
        Linked work and receipts preserve the original signed findings, estimate and grade.
      </p>
      {!history.items.length && <p>No repair requests on this page.</p>}
      {history.items.map(({ request, approval, completion }) => (
        <div className={styles.historyRow} key={request.operationId}>
          <StaffLink
            prefetch={false}
            href={`/dashboard/inspection?inspection=${request.inspectionId}&section=repairs&request=${request.operationId}&revision=${request.revision}`}
          >
            Work version {request.version} · {formatCurrency(request.budget)}
          </StaffLink>
          <span>
            Requested {formatMalaysianDateTime(request.requestedAt)} · {request.actorId}
          </span>
          <ul>
            {request.items.map((item) => (
              <li key={item.pointCode}>
                {item.pointCode} · {item.work}
              </li>
            ))}
          </ul>
          <p>
            {approval
              ? `Exact approval ${approval.operationId} · ${formatMalaysianDateTime(approval.approvedAt)} · ${approval.actorId}`
              : "No exact approval receipt on this request."}
          </p>
          {completion && (
            <p>
              Completed {formatMalaysianDateTime(completion.completedAt)} · {completion.actorId} ·{" "}
              {completion.notes}
              {completion.approvalId
                ? ` · Consumed approval ${completion.approvalId}`
                : " · Approval not required"}
            </p>
          )}
        </div>
      ))}
      <DataTablePagination
        page={{ ...history, total: history.totalCount }}
        count={history.items.length}
        disabled={disabled || busy}
        onPage={(p, size) => void load(p, size)}
      />
    </section>
  );
}
