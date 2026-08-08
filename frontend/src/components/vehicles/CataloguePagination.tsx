import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { buildPaginationSlots } from "@/lib/vehicles/catalogue-controls";
import type { CataloguePage } from "@/lib/vehicles/contracts";
import styles from "./Catalogue.module.css";

interface CataloguePaginationProps {
  page: number;
  snapshot: CataloguePage;
  busy: boolean;
  onPageChange: (nextPage: number) => void;
}

export function CataloguePagination({
  page,
  snapshot,
  busy,
  onPageChange,
}: CataloguePaginationProps) {
  const totalPages = Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize));
  const slots = buildPaginationSlots(page, totalPages);

  return (
    <nav className={styles.paging} aria-label="Vehicle catalogue pages">
      <Button
        type="button"
        variant="secondary"
        size="md"
        disabled={busy || page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        <Icon name="previous" /> Previous
      </Button>

      <div className={styles.pageNumbers}>
        {slots.map((slot) => {
          if (typeof slot !== "number") {
            return (
              <span key={slot} className={styles.ellipsis} aria-hidden="true">
                …
              </span>
            );
          }
          const isCurrent = slot === page;
          return (
            <Button
              key={slot}
              type="button"
              variant={isCurrent ? "primary" : "secondary"}
              size="compact"
              className={styles.pageButton}
              disabled={busy}
              aria-label={`Page ${slot}`}
              aria-current={isCurrent ? "page" : undefined}
              onClick={() => {
                if (!isCurrent) {
                  onPageChange(slot);
                }
              }}
            >
              {slot}
            </Button>
          );
        })}
      </div>

      <Button
        type="button"
        variant="secondary"
        size="md"
        disabled={busy || page >= totalPages}
        onClick={() => onPageChange(page + 1)}
      >
        Next <Icon name="next" />
      </Button>
    </nav>
  );
}
