import { Button } from "@/components/ui/Button";
import { FilterSection } from "@/components/ui/FilterSection";
import { BranchFilter } from "@/components/staff/BranchFilter";
import type { useReservationTable } from "@/hooks/use-reservation-table";
import styles from "@/components/staff/WorklistFilters.module.css";

export function ReservationFilters({
  state,
  group,
}: {
  state: ReturnType<typeof useReservationTable>;
  group: boolean;
}) {
  return (
    <form
      className={styles.filters}
      aria-label="Reservation filters"
      onSubmit={async (event) => {
        event.preventDefault();
        const form = event.currentTarget;
        if (await state.load({ ...state.active.filters, branchId: state.draft.branchId }, 1)) {
          form.closest<HTMLElement>("[popover]")?.hidePopover();
        }
      }}
    >
      {group ? (
        <FilterSection title="Branch">
          <BranchFilter
            branches={state.branches}
            branchId={state.draft.branchId}
            disabled={state.busy}
            labelHidden={false}
            paginationClassName={styles.actions}
            onChange={(branchId) => state.setDraft({ ...state.draft, branchId })}
            onPage={(page) => void state.loadBranches(page)}
          />
        </FilterSection>
      ) : (
        <p className={styles.help}>Reservations are limited to your branch.</p>
      )}
      <div className={styles.footer}>
        <Button
          size="compact"
          type="button"
          variant="secondary"
          disabled={state.busy}
          onClick={() => void state.load({ ...state.active.filters, branchId: "" }, 1)}
        >
          Clear all filters
        </Button>
        {group && (
          <Button size="compact" type="submit" disabled={state.busy}>
            Apply filters
          </Button>
        )}
      </div>
    </form>
  );
}
