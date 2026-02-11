import { appointmentStatusLabel } from "@/lib/staff/status";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { FilterSection } from "@/components/ui/FilterSection";
import { FilterChoices } from "@/components/ui/FilterChoices";
import { worklistStatuses } from "@/lib/staff/contracts";
import { resetWorklistFilters } from "@/lib/staff/filter-summary";
import { useStaffWorklist } from "@/hooks/use-staff-worklist";
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
          <Select
            density="compact"
            label="Branch"
            labelHidden
            id="worklist-branch"
            value={draft.branchId}
            disabled={disabled}
            onChange={(event) => setDraft({ ...draft, branchId: event.target.value })}
          >
            <option value="">All branches</option>
            {draft.branchId && !branches?.items.some((branch) => branch.id === draft.branchId) && (
              <option value={draft.branchId}>
                {worklist.snapshot?.items.find((item) => item.branchId === draft.branchId)
                  ?.branchName ?? "Selected branch"}
              </option>
            )}
            {branches?.items.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        )}
        {group && (!branches || branches.total > branches.pageSize) && (
          <div>
            {!branches && (
              <Button
                size="compact"
                variant="secondary"
                type="button"
                disabled={disabled}
                onClick={() => void worklist.loadBranches(1)}
              >
                Retry branches
              </Button>
            )}
            {branches && branches.total > branches.pageSize && (
              <div className={styles.actions}>
                <Button
                  size="compact"
                  variant="secondary"
                  type="button"
                  disabled={disabled || branches.page === 1}
                  onClick={() => void worklist.loadBranches(branches.page - 1)}
                >
                  Previous branches
                </Button>
                <Button
                  size="compact"
                  variant="secondary"
                  type="button"
                  disabled={disabled || branches.page * branches.pageSize >= branches.total}
                  onClick={() => void worklist.loadBranches(branches.page + 1)}
                >
                  More branches
                </Button>
              </div>
            )}
          </div>
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
