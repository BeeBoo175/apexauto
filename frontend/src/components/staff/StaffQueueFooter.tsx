import { Button } from "@/components/ui/Button";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import type { TablePage } from "@/components/ui/data-table/types";
import styles from "./StaffQueueWorkspace.module.css";

export function StaffQueueFooter({
  page,
  count,
  busy,
  failed,
  disabled = false,
  onPage,
  onRetry,
}: {
  page: TablePage | null;
  count: number;
  busy: boolean;
  failed: boolean;
  disabled?: boolean;
  onPage: (page: number, pageSize: number) => void;
  onRetry: () => void;
}) {
  return (
    <footer className={styles.queueFooter}>
      {page && (
        <DataTablePagination
          page={page}
          count={count}
          disabled={busy || disabled}
          onPage={onPage}
        />
      )}
      {failed && (
        <Button
          size="compact"
          variant="secondary"
          disabled={busy || disabled}
          aria-busy={busy}
          onClick={onRetry}
        >
          Retry queue
        </Button>
      )}
    </footer>
  );
}
