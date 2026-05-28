import { Button } from "@/components/ui/Button";
import styles from "./StaffQueueWorkspace.module.css";

export function StaffQueueContinuation({
  loaded,
  total,
  more,
  busy,
  failed,
  disabled = false,
  onMore,
  onRetry,
}: {
  loaded: number;
  total?: number;
  more: boolean;
  busy: boolean;
  failed: boolean;
  disabled?: boolean;
  onMore: () => void;
  onRetry: () => void;
}) {
  return (
    <div className={styles.continuation}>
      <span role="status" aria-live="polite">
        {total === undefined ? `${loaded} loaded` : `${loaded} of ${total} loaded`}
      </span>
      {(more || failed) && (
        <Button
          size="compact"
          variant="secondary"
          disabled={busy || disabled}
          aria-busy={busy}
          onClick={failed ? onRetry : onMore}
        >
          {failed ? "Retry queue" : "Load more"}
        </Button>
      )}
    </div>
  );
}
