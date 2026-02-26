"use client";
import { useEffect, useRef, useState } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseRevisions, parseRepairs } from "@/lib/inspection/parse-report";
import type { RevisionHistory, RepairHistory } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function RevisionHistoryView({
  initial,
  disabled = false,
}: {
  initial: RevisionHistory;
  disabled?: boolean;
}) {
  const [history, setHistory] = useState(initial);
  const [entry, setEntry] = useState(initial);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  if (entry !== initial) {
    setEntry(initial);
    setHistory(initial);
  }
  useEffect(() => () => controller.current?.abort(), []);
  async function load(page: number, pageSize: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await inspectionFetch(
        `reports/${history.inspectionId}/revisions?page=${page}&pageSize=${pageSize}`,
        parseRevisions,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (
        !result.data ||
        result.data.inspectionId !== history.inspectionId ||
        result.data.page !== page
      ) {
        throw new Error("History unavailable");
      }
      setHistory(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Revision history could not be refreshed. Existing revisions remain shown.");
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
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
  const [history, setHistory] = useState(initial);
  const [entry, setEntry] = useState(initial);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  if (entry !== initial) {
    setEntry(initial);
    setHistory(initial);
  }
  useEffect(() => () => controller.current?.abort(), []);
  async function load(page: number, pageSize: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await inspectionFetch(
        `reports/${history.inspectionId}/revisions/${history.revision}/repair-history?page=${page}&pageSize=${pageSize}`,
        parseRepairs,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (
        !result.data ||
        result.data.inspectionId !== history.inspectionId ||
        result.data.revision !== history.revision ||
        result.data.page !== page
      ) {
        throw new Error("History unavailable");
      }
      setHistory(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Repair history could not be refreshed. Existing receipts remain shown.");
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
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
          <strong>
            Work version {request.version} · {formatCurrency(request.budget)}
          </strong>
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
