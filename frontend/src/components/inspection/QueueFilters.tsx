import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { FilterSection } from "@/components/ui/FilterSection";
import { FilterChoices } from "@/components/ui/FilterChoices";
import { workshopTasks, workshopTaskLabel } from "@/lib/inspection/workshop-contracts";
import type { QueueFilters as Filters } from "@/lib/inspection/contracts";
import type { Branch } from "@/lib/branches";
import type { Page } from "@/lib/api";
import styles from "@/components/staff/WorklistFilters.module.css";

export function QueueFilters({
  draft,
  onDraft,
  apply,
  busy,
  group,
  branches,
  loadBranches,
}: {
  draft: Filters;
  onDraft: (v: Filters) => void;
  apply: (v: Filters) => Promise<boolean | undefined>;
  busy: boolean;
  group: boolean;
  branches: Page<Branch> | null;
  loadBranches: (page: number) => void;
}) {
  return (
    <form
      className={styles.filters}
      aria-label="Queue filters"
      onSubmit={async (event) => {
        event.preventDefault();
        const form = event.currentTarget;
        if (await apply(draft)) {
          form.closest<HTMLElement>("[popover]")?.hidePopover();
        }
      }}
    >
      <FilterSection title="Work dates">
        <div className={styles.dateRangeInputs}>
          <Input
            label="From"
            type="date"
            density="compact"
            value={draft.from}
            disabled={busy}
            onChange={(e) => onDraft({ ...draft, from: e.target.value })}
          />
          <Input
            label="To"
            type="date"
            density="compact"
            value={draft.to}
            disabled={busy}
            onChange={(e) => onDraft({ ...draft, to: e.target.value })}
          />
        </div>
      </FilterSection>
      <FilterSection title={group ? "Task & branch" : "Task"}>
        <FilterChoices
          label="Task"
          options={workshopTasks.map((value) => ({ value, label: workshopTaskLabel(value) }))}
          value={draft.status}
          disabled={busy}
          onChange={(status) => onDraft({ ...draft, status })}
        />
        {group && (
          <Select
            label="Branch"
            density="compact"
            value={draft.branchId}
            disabled={busy}
            onChange={(e) => onDraft({ ...draft, branchId: e.target.value })}
          >
            <option value="">All branches</option>
            {draft.branchId && !branches?.items.some((b) => b.id === draft.branchId) && (
              <option value={draft.branchId}>Selected branch</option>
            )}
            {branches?.items.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </Select>
        )}
        {group && !branches && (
          <Button
            size="compact"
            variant="secondary"
            type="button"
            disabled={busy}
            onClick={() => loadBranches(1)}
          >
            Retry branches
          </Button>
        )}
        {group && branches && branches.total > branches.pageSize && (
          <div className={styles.actions}>
            <Button
              size="compact"
              variant="secondary"
              type="button"
              disabled={busy || branches.page === 1}
              onClick={() => loadBranches(branches.page - 1)}
            >
              Previous branches
            </Button>
            <Button
              size="compact"
              variant="secondary"
              type="button"
              disabled={busy || branches.page * branches.pageSize >= branches.total}
              onClick={() => loadBranches(branches.page + 1)}
            >
              More branches
            </Button>
          </div>
        )}
      </FilterSection>
      <p className={styles.help}>
        Work date is the current request date, or check-in date when there is no current request.{" "}
        Inclusive Malaysian dates, up to 366 days. Leave both empty for all history.
      </p>
      <div className={styles.footer}>
        <Button
          size="compact"
          type="button"
          variant="secondary"
          disabled={busy}
          onClick={() =>
            void apply({
              ...draft,
              status: "All",
              branchId: "",
              from: "",
              to: "",
              search: "",
            })
          }
        >
          Clear all filters
        </Button>
        <Button size="compact" type="submit" disabled={busy}>
          Apply filters
        </Button>
      </div>
    </form>
  );
}
