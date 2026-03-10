"use client";

import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import type { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { workshopStatusLabel, type WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import styles from "./TechnicianWorkspace.module.css";

export function TechnicianJobList({
  title,
  state,
  onOpen,
  claim = false,
  selectedId = "",
  pendingId = "",
  selectionLocked = false,
}: {
  title: string;
  state: ReturnType<typeof useInspectionQueue<WorkshopRow>>;
  onOpen: (row: WorkshopRow) => void;
  claim?: boolean;
  selectedId?: string;
  pendingId?: string;
  selectionLocked?: boolean;
}) {
  const { snapshot, active } = state;
  return (
    <section className={styles.list} aria-label={title} aria-busy={state.busy}>
      <div className={styles.listHeading}>
        <h2>{title}</h2>
        {snapshot && (
          <span>
            {snapshot.total} {active.filters.search ? "matching " : ""}
            {snapshot.total === 1 ? "job" : "jobs"}
          </span>
        )}
      </div>
      {(!snapshot || snapshot.total > 0 || active.filters.search || state.draft.search) && (
        <form
          className={styles.search}
          onSubmit={(event) => {
            event.preventDefault();
            void state.load({ ...active.filters, search: state.draft.search.trim() }, 1);
          }}
        >
          <SearchField
            label={`Search ${title.toLowerCase()}`}
            placeholder="Search vehicle or registration"
            value={state.draft.search}
            disabled={!state.ready}
            busy={state.busy}
            onChange={(search) => state.setDraft({ ...state.draft, search })}
            onClear={
              active.filters.search
                ? () => void state.load({ ...active.filters, search: "" }, 1)
                : undefined
            }
          />
        </form>
      )}
      {!snapshot && (
        <div className={styles.empty}>
          <p>This worklist is unavailable. Retry to load your jobs.</p>
          <Button size="compact" variant="secondary" onClick={() => void state.refresh()}>
            Retry {title.toLowerCase()}
          </Button>
        </div>
      )}
      {snapshot?.total === 0 && (
        <p className={styles.empty}>
          {active.filters.search ? "No jobs match this search." : "No jobs in this worklist."}
        </p>
      )}
      {snapshot && snapshot.items.length === 0 && snapshot.total > 0 && (
        <div className={styles.empty}>
          <p>No jobs remain on this page.</p>
          <Button
            size="compact"
            variant="secondary"
            onClick={() => void state.load(active.filters, 1)}
          >
            Return to first page
          </Button>
        </div>
      )}
      <ul className={styles.jobs}>
        {snapshot?.items.map((row) => (
          <li key={row.vehicleId}>
            <Button
              variant="ghost"
              size="row"
              className={styles.job}
              onClick={() => onOpen(row)}
              aria-pressed={!claim && row.inspectionId === selectedId}
              aria-busy={(!claim && row.inspectionId === pendingId) || undefined}
              disabled={selectionLocked && row.inspectionId !== selectedId}
              aria-label={`${claim ? "Claim inspection" : "Open job"} · ${row.registrationPlate}`}
            >
              <span className={styles.identity}>
                <strong>{row.registrationPlate}</strong>
                <span>
                  {row.make} {row.model}
                </span>
              </span>
              <span className={styles.jobState}>
                <strong>{claim ? "Claim inspection" : workshopStatusLabel(row.status)}</strong>
                <span>
                  {row.dateSource === "Requested" ? "Requested" : "Checked in"}{" "}
                  {formatMalaysianDateTime(row.since)}
                  {row.budget !== null && ` · ${formatCurrency(row.budget)}`}
                </span>
              </span>
            </Button>
          </li>
        ))}
      </ul>
      {snapshot && snapshot.total > 0 && (
        <DataTablePagination
          page={snapshot}
          count={snapshot.items.length}
          disabled={state.busy}
          onPage={(page, pageSize) => void state.load({ ...active.filters, pageSize }, page)}
        />
      )}
    </section>
  );
}
