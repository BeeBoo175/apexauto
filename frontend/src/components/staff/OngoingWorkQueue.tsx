import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { WorkshopStatus } from "@/components/inspection/WorkshopStatus";
import { SaleStatus } from "@/components/sales/SaleStatus";
import { workshopStatuses } from "@/lib/inspection/workshop-contracts";
import { saleStatuses } from "@/lib/sales/contracts";
import { StaffQueueTabs } from "./StaffQueueTabs";
import { StaffQueueFooter } from "./StaffQueueFooter";
import type { UnscheduledWorkPage, WorkItem } from "@/lib/staff/work-calendar";
import { workItemHref } from "@/lib/staff/work-calendar-presentation";
import { StaffLink } from "./StaffLink";
import queue from "./StaffQueueWorkspace.module.css";
import styles from "./WorkCalendar.module.css";

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
  onRetry,
}: {
  snapshot: UnscheduledWorkPage | null;
  busy: boolean;
  own: boolean;
  showBranch: boolean;
  search: string;
  kind: "" | "Workshop" | "Sale";
  kinds: ("Workshop" | "Sale")[];
  onKind: (kind: "" | "Workshop" | "Sale") => void;
  onPage: (page: number, pageSize: number) => void;
  onRetry: () => void;
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
            <StaffQueueTabs<"" | "Workshop" | "Sale">
              label="Ongoing work types"
              layout="queue"
              countNoun="jobs"
              tabs={[
                { value: "", label: "All work", count: snapshot?.counts.all },
                { value: "Workshop", label: "Workshop", count: snapshot?.counts.workshop },
                { value: "Sale", label: "Sales", count: snapshot?.counts.sale },
              ]}
              value={kind}
              disabled={busy}
              onChange={onKind}
            />
          </>
        )}
      </header>
      <div className={queue.list} key={`${snapshot?.page}:${snapshot?.pageSize}:${search}:${kind}`}>
        {snapshot?.items.length === 0 && (
          <p className={queue.empty}>No ongoing jobs match this view.</p>
        )}
        {!snapshot && (
          <div className={queue.empty}>
            <Button size="compact" variant="secondary" disabled={busy} onClick={onRetry}>
              Retry ongoing work
            </Button>
          </div>
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
                    {item.handlerName ?? item.handlerEmail}
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
          onRetry={onRetry}
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
