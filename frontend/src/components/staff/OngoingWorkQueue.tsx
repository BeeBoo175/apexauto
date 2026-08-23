import { Badge } from "@/components/ui/Badge";
import { WorkshopStatus } from "@/components/inspection/WorkshopStatus";
import { SaleStatus } from "@/components/sales/SaleStatus";
import { workshopStatuses } from "@/lib/inspection/workshop-contracts";
import { saleStatuses } from "@/lib/sales/contracts";
import type { IconName } from "@/lib/icons";
import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { StaffQueueTabs } from "./StaffQueueTabs";
import { StaffQueueFooter } from "./StaffQueueFooter";
import type { UnscheduledWorkPage, WorkItem } from "@/lib/staff/work-calendar";
import { workItemHref } from "@/lib/staff/work-calendar-presentation";
import { StaffLink } from "./StaffLink";
import queue from "./StaffQueueWorkspace.module.css";
import styles from "./WorkCalendar.module.css";
import { PersonLabel } from "./PersonLabel";

const emptyKinds: Record<
  "" | "workshop" | "sale",
  { icon: IconName; title: string; text: string }
> = {
  "": { icon: "clock", title: "No ongoing work", text: "Jobs in progress appear here." },
  workshop: {
    icon: "tool",
    title: "No workshop jobs",
    text: "Vehicles in the workshop appear here.",
  },
  sale: { icon: "car", title: "No sales in progress", text: "Sales being worked appear here." },
};

export function OngoingWorkQueue({
  snapshot,
  busy,
  own,
  showBranch,
  search,
  kind,
  kinds,
  onKind,
  onPage,
}: {
  snapshot: UnscheduledWorkPage | null;
  busy: boolean;
  own: boolean;
  showBranch: boolean;
  search: string;
  kind: "" | "workshop" | "sale";
  kinds: ("workshop" | "sale")[];
  onKind: (kind: "" | "workshop" | "sale") => void;
  onPage: (page: number, pageSize: number) => void;
}) {
  return (
    <section className={styles.ongoing} aria-label="Ongoing work" aria-busy={busy}>
      <header className={styles.ongoingToolbar}>
        {kinds.length === 1 ? (
          <div className={styles.singleQueueHeading}>
            <h2>Sales</h2>
            {snapshot && (
              <Badge variant="count">
                {snapshot.counts.sale}
                <span className="sr-only"> jobs</span>
              </Badge>
            )}
          </div>
        ) : (
          <>
            <h2 className="sr-only">Ongoing work</h2>
            <StaffQueueTabs<"" | "workshop" | "sale">
              label="Ongoing work types"
              layout="queue"
              countNoun="jobs"
              tabs={[
                { value: "", label: "All work", count: snapshot?.counts.all },
                { value: "workshop", label: "Workshop", count: snapshot?.counts.workshop },
                { value: "sale", label: "Sales", count: snapshot?.counts.sale },
              ]}
              value={kind}
              disabled={busy}
              onChange={onKind}
            />
          </>
        )}
      </header>
      <div className={queue.list} key={`${snapshot?.page}:${snapshot?.pageSize}:${search}:${kind}`}>
        {snapshot?.items.length === 0 && search && (
          <EmptyNotice icon="search" title="No matches">
            No job matches this search.
          </EmptyNotice>
        )}
        {snapshot?.items.length === 0 && !search && (
          <EmptyNotice icon={emptyKinds[kind].icon} title={emptyKinds[kind].title}>
            {emptyKinds[kind].text}
          </EmptyNotice>
        )}
        {!snapshot && !busy && (
          <EmptyNotice icon="alert-triangle" tone="alert" title="Ongoing work unavailable">
            Reload the page to try again.
          </EmptyNotice>
        )}
        <ul className={queue.jobs}>
          {snapshot?.items.map((item) => (
            <li key={`${item.kind}:${item.id}`}>
              <StaffLink
                href={workItemHref(item)}
                prefetch={false}
                className={`${queue.job} ${styles.workLink}`}
              >
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
            </li>
          ))}
        </ul>
      </div>
      {snapshot && (
        <StaffQueueFooter
          page={snapshot}
          count={snapshot.items.length}
          busy={busy}
          failed={false}
          onPage={onPage}
        />
      )}
    </section>
  );
}

function OngoingStatus({ item }: { item: WorkItem }) {
  if (item.kind === "Workshop") {
    const status = workshopStatuses.find((value) => value === item.status);
    return status ? <WorkshopStatus status={status} /> : null;
  }
  const status = saleStatuses.find((value) => value === item.status);
  return status ? <SaleStatus status={status} /> : null;
}
