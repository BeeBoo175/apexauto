import { ReloadNotice } from "@/components/ui/ReloadNotice";
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
}: {
  page: TablePage | null;
  count: number;
  busy: boolean;
  failed: boolean;
  disabled?: boolean;
  onPage: (page: number, pageSize: number) => void;
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
      {failed && <ReloadNotice subject="Queue" />}
    </footer>
  );
}
