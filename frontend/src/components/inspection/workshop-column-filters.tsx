import type { DataColumn } from "@/components/ui/data-table/types";
import type { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { QueueFilters } from "./QueueFilters";

export function workshopColumnFilters<T>(
  state: ReturnType<typeof useInspectionQueue<T>>,
  columns: DataColumn<T>[],
  disabled: boolean,
) {
  const filters = state.active.filters;
  const filteredColumns = columns.map((column) => {
    const indicators: Record<string, boolean> = {
      registrationPlate: Boolean(filters.registration),
      vehicle: Boolean(filters.vehicle),
      branchName: Boolean(filters.branchId),
      status: Boolean(filters.workStatus),
      technicianIdentity: Boolean(filters.technician),
      budget: Boolean(filters.minimumBudget || filters.maximumBudget),
      checkedInAt: Boolean(filters.from || filters.to),
      requestedAt: Boolean(filters.from || filters.to),
    };
    return {
      ...column,
      filter: {
        active: indicators[column.id],
        content: (
          <QueueFilters
            filters={filters}
            column={column.id}
            apply={(filters) => state.load(filters, 1)}
            busy={state.busy || disabled}
            branches={state.branches}
            loadBranches={(page) => void state.loadBranches(page)}
          />
        ),
      },
    };
  });

  return filteredColumns;
}
