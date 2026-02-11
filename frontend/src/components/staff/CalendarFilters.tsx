import { appointmentStatusLabel } from "@/lib/staff/status";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { worklistStatuses } from "@/lib/staff/contracts";
import type { useStaffCalendar } from "@/hooks/use-staff-calendar";
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
  const branchName =
    branches?.items.find((branch) => branch.id === active.branchId)?.name ??
    calendar.snapshot?.items.find((item) => item.branchId === active.branchId)?.branchName ??
    "Selected branch";
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
        <div>
          <Select
            label="Branch"
            labelHidden
            density="compact"
            value={active.branchId}
            title={active.branchId ? branchName : "All branches"}
            disabled={disabled}
            onChange={(event) => void calendar.load({ ...active, branchId: event.target.value })}
          >
            <option value="">All branches</option>
            {active.branchId &&
              !branches?.items.some((branch) => branch.id === active.branchId) && (
                <option value={active.branchId}>{branchName}</option>
              )}
            {branches?.items.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        </div>
      )}
      {group && !branches && (
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled}
          onClick={() => void calendar.loadBranches(1)}
        >
          Retry branches
        </Button>
      )}
      {group && branches && branches.total > branches.pageSize && (
        <>
          <Button
            size="compact"
            variant="secondary"
            disabled={disabled || branches.page === 1}
            onClick={() => void calendar.loadBranches(branches.page - 1)}
          >
            Previous branches
          </Button>
          <Button
            size="compact"
            variant="secondary"
            disabled={disabled || branches.page * branches.pageSize >= branches.total}
            onClick={() => void calendar.loadBranches(branches.page + 1)}
          >
            More branches
          </Button>
        </>
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
