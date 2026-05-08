import { Button } from "@/components/ui/Button";
import type { StaffHistoryPage } from "@/hooks/use-staff-history";
import placeholders from "./StaffRecordLoadingShell.module.css";
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
  const { history, busy, failed, load, retry } = read;
  if (!history && busy) {
    return (
      <div role="status" aria-label={`Loading ${label}`}>
        <ul className={timeline.repairs} aria-hidden="true">
          {[0, 1, 2].map((row) => (
            <li key={row}>
              <span className={`${placeholders.placeholder} ${placeholders.status}`} />
              <span className={`${placeholders.placeholder} ${placeholders.value}`} />
            </li>
          ))}
        </ul>
      </div>
    );
  }
  if (failed) {
    return (
      <div>
        <p role="alert">{label} could not be loaded. Revealed history remains shown.</p>
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled || busy}
          onClick={() => void retry()}
        >
          Retry {label.toLowerCase()}
        </Button>
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
