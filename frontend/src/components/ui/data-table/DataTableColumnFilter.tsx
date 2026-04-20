import { useId, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { FilterShelf } from "@/components/ui/FilterShelf";
import type { TableColumnFilter } from "./types";
import styles from "./DataTableColumnFilter.module.css";
import headerStyles from "./DataTableHeader.module.css";
import iconStyles from "./TableIcons.module.css";

export function DataTableColumnFilter({
  label,
  filter,
  disabled,
}: {
  label: string;
  filter: TableColumnFilter;
  disabled: boolean;
}) {
  const id = useId();
  const panelId = `column-filter-${id}`;
  const anchor = `--column-filter-${id.replace(/[^a-zA-Z0-9-]/g, "")}`;
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button
        type="button"
        size="row"
        variant="ghost"
        className={`${styles.trigger} ${headerStyles.action}`}
        disabled={disabled}
        popoverTarget={panelId}
        aria-controls={panelId}
        aria-expanded={open}
        aria-label={`Filter ${label}${filter.active ? ", filter applied" : ""}`}
        title={`Filter ${label}`}
        data-filtered={filter.active || undefined}
        style={{ anchorName: anchor }}
      >
        <span className={`${styles.glyph} ${iconStyles.glyph}`}>
          <Icon name="table-filter" grid={16} />
          {filter.active && <span className={styles.indicator} aria-hidden="true" />}
        </span>
      </Button>
      <FilterShelf
        id={panelId}
        anchor={anchor}
        title={label}
        onOpenChange={(value) => {
          setOpen(value);
          filter.onOpenChange?.(value);
        }}
      >
        {open && filter.content}
      </FilterShelf>
    </>
  );
}
