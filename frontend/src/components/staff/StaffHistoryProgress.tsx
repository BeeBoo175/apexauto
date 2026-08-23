import { Button } from "@/components/ui/Button";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import type { StaffHistoryPage } from "@/hooks/use-staff-history";
import timeline from "./StaffHistory.module.css";

interface HistoryRead {
  history: StaffHistoryPage | null;
  busy: boolean;
  failed: boolean;
  load: (page: number, pageSize: number) => Promise<unknown>;
  retry: () => Promise<unknown>;
}

export function StaffHistoryProgress({
  read,
  label,
  disabled = false,
}: {
  read: HistoryRead;
  label: string;
  disabled?: boolean;
}) {
  const { history, busy, failed, load } = read;
  if (!history && busy) {
    return (
      <div role="status" aria-label={`Loading ${label}`}>
        <ol className={timeline.timeline} aria-hidden="true">
          {[0, 1, 2].map((row) => (
            <li key={row}>
              <div className={timeline.eventHeading}>
                <span className={timeline.eventTitle} />
                <span className={timeline.eventDate} />
              </div>
              <span className={timeline.eventContext} />
              <span className={timeline.eventText} />
              <span className={timeline.eventText} />
            </li>
          ))}
        </ol>
      </div>
    );
  }
  if (failed) {
    return (
      <div>
        <ReloadNotice subject={label} />
      </div>
    );
  }
  if (!history || history.page * history.pageSize >= history.totalCount) {
    return null;
  }
  return (
    <Button
      size="compact"
      variant="secondary"
      disabled={disabled || busy}
      aria-busy={busy}
      onClick={() => void load(history.page + 1, history.pageSize)}
    >
      Load older {label.toLowerCase()}
    </Button>
  );
}
