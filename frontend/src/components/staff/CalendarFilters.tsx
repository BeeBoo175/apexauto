import { appointmentStatusLabel } from "@/lib/staff/status";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { worklistStatuses } from "@/lib/staff/contracts";
import type { useStaffCalendar } from "@/hooks/use-staff-calendar";
import { BranchFilter } from "./BranchFilter";
import styles from "./IntakeCalendar.module.css";

export function CalendarFilters({
  calendar,
  disabled,
  group,
}: {
  calendar: ReturnType<typeof useStaffCalendar>;
  disabled: boolean;
  group: boolean;
}) {
  const { active, branches } = calendar;
  return (
    <div className={styles.filters} role="group" aria-label="Calendar filters">
      <Select
        label="Status"
        labelHidden
        density="compact"
        value={active.status}
        title={
          active.status === "All"
            ? "All statuses"
            : active.status.replace("CheckedIn", "Checked in")
        }
        disabled={disabled}
        onChange={(event) => void calendar.load({ ...active, status: event.target.value })}
      >
        {worklistStatuses.map((status) => {
          if (status === "All") {
            return (
              <option key={status} value={status}>
                All statuses
              </option>
            );
          }
          return (
            <option key={status} value={status}>
              {appointmentStatusLabel(status)}
            </option>
          );
        })}
      </Select>
      {group && (
        <BranchFilter
          branches={branches}
          branchId={active.branchId}
          selectedBranchName={
            calendar.snapshot?.items.find((item) => item.branchId === active.branchId)?.branchName
          }
          disabled={disabled}
          showTitle
          onChange={(branchId) => void calendar.load({ ...active, branchId })}
          onPage={(page) => void calendar.loadBranches(page)}
        />
      )}
      {(active.range || active.status !== "All" || (group && active.branchId)) && (
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled}
          onClick={() =>
            void calendar.load({ ...active, range: undefined, status: "All", branchId: "" })
          }
        >
          Clear all filters
        </Button>
      )}
    </div>
  );
}
