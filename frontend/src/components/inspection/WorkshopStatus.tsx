import { Badge } from "@/components/ui/Badge";
import { workshopStatusLabel, type WorkshopRow } from "@/lib/inspection/workshop-contracts";
import tones from "@/components/ui/StatusTone.module.css";

const statusTones = {
  AwaitingInspection: "neutral",
  InspectionUnderway: "info",
  RepairRequestRequired: "warning",
  ApprovalRequired: "warning",
  RepairsToComplete: "info",
  ReadyForSale: "success",
  Rejected: "danger",
} as const;

export function WorkshopStatus({ status }: { status: WorkshopRow["status"] }) {
  return (
    <Badge className={`${tones.badge} ${tones[statusTones[status]]}`}>
      {workshopStatusLabel(status)}
    </Badge>
  );
}
