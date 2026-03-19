import { PAGE_SIZES } from "@/lib/pagination";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { Icon } from "@/components/ui/Icon";
import type { TablePage } from "./types";
import styles from "./DataTable.module.css";

export function DataTablePagination({
  page,
  count,
  disabled,
  onPage,
}: {
  page: TablePage;
  count: number;
  disabled: boolean;
  onPage: (page: number, pageSize: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(page.total / page.pageSize));
  const first = count > 0 ? (page.page - 1) * page.pageSize + 1 : 0;
  const last = count > 0 ? first + count - 1 : 0;
  const recordLabel = page.total === 0 ? "0 records" : `${first}–${last} of ${page.total}`;

  return (
    <nav className={styles.pagination} aria-label="Table pages">
      <span className={styles.meta} aria-live="polite">
        {recordLabel}
      </span>
      <div className={styles.paginationControls}>
        <Select
          label="Rows per page"
          labelHidden
          density="compact"
          value={page.pageSize}
          disabled={disabled}
          onChange={(event) => onPage(1, Number(event.target.value))}
        >
          {PAGE_SIZES.map((size) => (
            <option key={size} value={size}>
              {size} / page
            </option>
          ))}
        </Select>
        <Button
          variant="ghost"
          size="icon"
          disabled={disabled || page.page <= 1}
          onClick={() => onPage(page.page - 1, page.pageSize)}
          aria-label="Previous records"
          title="Previous page"
        >
          <Icon name="previous" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          disabled={disabled || page.page >= pages}
          onClick={() => onPage(page.page + 1, page.pageSize)}
          aria-label="Next records"
          title="Next page"
        >
          <Icon name="next" />
        </Button>
      </div>
    </nav>
  );
}
