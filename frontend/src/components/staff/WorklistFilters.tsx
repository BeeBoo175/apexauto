import { appointmentStatusLabel } from "@/lib/staff/status";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { FilterSection } from "@/components/ui/FilterSection";
import { FilterChoices } from "@/components/ui/FilterChoices";
import { worklistStatuses } from "@/lib/staff/contracts";
import { resetWorklistFilters } from "@/lib/staff/filter-summary";
import { useStaffWorklist } from "@/hooks/use-staff-worklist";
import { BranchFilter } from "./BranchFilter";
import styles from "./WorklistFilters.module.css";

const statusChoices = [
  { value: "All", label: "All statuses" },
  ...worklistStatuses
    .filter((status) => status !== "All")
    .map((status) => ({
      value: status,
      label: appointmentStatusLabel(status),
    })),
];

export function WorklistFilters({
  worklist,
  disabled,
  group,
}: {
  worklist: ReturnType<typeof useStaffWorklist>;
  disabled: boolean;
  group: boolean;
}) {
  const { draft, setDraft, branches } = worklist;
  return (
    <form
      method="post"
      aria-label="Appointment filters"
      className={styles.filters}
      onSubmit={async (event) => {
        event.preventDefault();
        const form = event.currentTarget;
        const applied = await worklist.load(draft, 1);
        if (applied) {
          form.closest<HTMLElement>("[popover]")?.hidePopover();
        }
      }}
    >
      <FilterSection title="Date range">
        <div className={styles.dateRangeInputs}>
          <Input
            id="worklist-from"
            label="From"
            type="date"
            density="compact"
            value={draft.from}
            disabled={disabled}
            onChange={(event) => setDraft({ ...draft, from: event.target.value })}
          />
          <Input
            id="worklist-to"
            label="To"
            type="date"
            density="compact"
            value={draft.to}
            disabled={disabled}
            onChange={(event) => setDraft({ ...draft, to: event.target.value })}
          />
        </div>
      </FilterSection>
      <FilterSection title={group ? "Status & branch" : "Status"}>
        <FilterChoices
          label="Status"
          options={statusChoices}
          value={draft.status}
          disabled={disabled}
          onChange={(status) => setDraft({ ...draft, status })}
        />
        {group && (
          <BranchFilter
            branches={branches}
            branchId={draft.branchId}
            selectedBranchName={
              worklist.snapshot?.items.find((item) => item.branchId === draft.branchId)?.branchName
            }
            disabled={disabled}
            id="worklist-branch"
            paginationClassName={styles.actions}
            onChange={(branchId) => setDraft({ ...draft, branchId })}
            onPage={(page) => void worklist.loadBranches(page)}
          />
        )}
      </FilterSection>
      <p className={styles.help}>Changes apply only when you select Apply filters.</p>
      <div className={styles.footer}>
        <Button
          type="button"
          size="compact"
          variant="secondary"
          disabled={disabled}
          onClick={async (event) => {
            const form = event.currentTarget.closest("form");
            if (await worklist.load(resetWorklistFilters(worklist.active.filters), 1)) {
              form?.closest<HTMLElement>("[popover]")?.hidePopover();
            }
          }}
        >
          Clear all filters
        </Button>
        <Button type="submit" size="compact" variant="primary" disabled={disabled}>
          Apply filters
        </Button>
      </div>
    </form>
  );
}
