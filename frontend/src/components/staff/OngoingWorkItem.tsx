import { Badge } from "@/components/ui/Badge";
import { WorkshopStatus } from "@/components/inspection/WorkshopStatus";
import { SaleStatus } from "@/components/sales/SaleStatus";
import { workshopStatuses } from "@/lib/inspection/workshop-contracts";
import { saleStatuses } from "@/lib/sales/contracts";
import type { WorkItem } from "@/lib/staff/work-calendar";
import { workItemHref, workItemLabel } from "@/lib/staff/work-calendar-presentation";
import { StaffLink } from "./StaffLink";
import { PersonLabel } from "./PersonLabel";
import queue from "./StaffQueueWorkspace.module.css";

export function OngoingWorkItem({
  item,
  own = true,
  showBranch = false,
}: {
  item: WorkItem;
  own?: boolean;
  showBranch?: boolean;
}) {
  return (
    <StaffLink href={workItemHref(item)} prefetch={false} className={queue.jobLink}>
      <span className={queue.jobHeading}>
        <span className={queue.identity}>
          <strong>{item.registrationPlate}</strong>
          <span>{item.vehicle}</span>
        </span>
        <OngoingStatus item={item} />
      </span>
      {!own && (
        <span className={queue.jobMeta}>
          {showBranch && `${item.branchName} · `}
          {item.handlerId && item.handlerEmail && (
            <PersonLabel
              id={item.handlerId}
              name={item.handlerName ?? item.handlerEmail}
              identity={item.handlerEmail}
              pictureId={item.handlerPictureId}
              nested
            />
          )}
        </span>
      )}
    </StaffLink>
  );
}

function OngoingStatus({ item }: { item: WorkItem }) {
  if (item.kind === "Workshop") {
    const status = workshopStatuses.find((value) => value === item.status);
    return status ? <WorkshopStatus status={status} /> : null;
  }
  if (item.kind === "Sale") {
    const status = saleStatuses.find((value) => value === item.status);
    return status ? <SaleStatus status={status} /> : null;
  }
  return <Badge>{workItemLabel(item)}</Badge>;
}
