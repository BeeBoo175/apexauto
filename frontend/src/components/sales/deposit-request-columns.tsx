import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import type { DataColumn } from "@/components/ui/data-table/types";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { DEPOSIT_REQUEST_HOURS } from "@/lib/vehicles/deposit-request";
import type { DepositRequestRow } from "@/lib/sales/contracts";

// The Awaiting deposit tab: cars held for a customer who has not yet confirmed the deposit staff asked for.
export function depositRequestColumns(
  showBranch: boolean,
  now: number,
  renderCancel: (row: DepositRequestRow) => React.ReactNode,
): DataColumn<DepositRequestRow>[] {
  const columns: DataColumn<DepositRequestRow>[] = [
    {
      id: "registration",
      label: "Registration",
      ...staffColumnSizing.registration,
      cell: (row) => <VehicleRegistration registration={row.registration} />,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      sortable: true,
      ...staffColumnSizing.vehicle,
      cell: (row) => <VehicleDescription vehicle={row} />,
    },
    {
      id: "customer",
      label: "Customer",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) => <CustomerAvatar id={row.customerId} name={row.customer} />,
    },
    {
      id: "requestedBy",
      label: "Requested by",
      ...staffColumnSizing.person,
      cell: (row) => row.requestedBy,
    },
    {
      id: "timeLeft",
      label: "Time left",
      sortable: true,
      ...staffColumnSizing.shortStatus,
      cell: (row) => (
        <HoldTimer
          dueAt={row.expiresAtUtc}
          now={now}
          totalSeconds={DEPOSIT_REQUEST_HOURS * 3600}
          compact
        />
      ),
    },
  ];
  if (showBranch) {
    columns.push({
      id: "branch",
      label: "Branch",
      sortable: true,
      ...staffColumnSizing.branch,
      cell: (row) => row.branch,
    });
  }
  columns.push({
    id: "actions",
    label: "Request",
    ...staffColumnSizing.shortStatus,
    cell: renderCancel,
  });
  return columns;
}
