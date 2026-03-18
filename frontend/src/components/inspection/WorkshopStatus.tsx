import { Badge } from "@/components/ui/Badge";
import { label, type Entry, type Severity } from "@/lib/inspection/contracts";
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

const severityTones = {
  Advisory: "info",
  MinorDefect: "warning",
  MajorDefect: "danger",
  CriticalDisqualifier: "danger",
} as const;

export function InspectionPointStatus({
  kind,
  severity,
  examined = false,
  compact = false,
}: {
  kind?: Entry["kind"] | null;
  severity?: Severity | null;
  examined?: boolean;
  compact?: boolean;
}) {
  let text = "Not examined";
  let tone = "neutral";
  if (examined) {
    text = "Passed";
    tone = "success";
  }
  if (kind === "NotFitted") {
    text = "Not fitted";
    tone = "neutral";
  }
  if (kind === "Finding") {
    text = severity ? label(severity) : "Finding";
    tone = severity ? severityTones[severity] : "warning";
  }
  return (
    <Badge
      className={`${tones.badge} ${tones[tone]} ${compact ? tones.label : ""}`}
      data-quiet={compact && kind !== "Finding"}
    >
      {text}
      {examined && kind === "Finding" && " · Examined"}
    </Badge>
  );
}
