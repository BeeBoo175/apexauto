"use client";

import { useId, useState, type ReactNode } from "react";
import { Button } from "../Button";
import { Icon } from "../Icon";
import { FilterShelf } from "../FilterShelf";
import styles from "./DataTable.module.css";

export function DataTableToolbar({
  children,
  filters,
  filterCount = 0,
  filterSummary,
  onFiltersOpenChange,
  defaultFiltersExpanded = false,
  selected,
  disabled,
  exporting,
  exportDisabled = false,
  onClear,
  onSelected,
  onAll,
}: {
  children?: ReactNode;
  filters?: ReactNode;
  filterCount?: number;
  filterSummary?: ReactNode;
  onFiltersOpenChange?: (open: boolean) => void;
  defaultFiltersExpanded?: boolean;
  selected: number;
  disabled: boolean;
  exporting: boolean;
  exportDisabled?: boolean;
  onClear?: () => void;
  onSelected?: () => void;
  onAll?: () => void;
}) {
  const [expanded, setExpanded] = useState(defaultFiltersExpanded);
  const id = useId();
  const panelId = `table-filters-${id}`;
  const anchor = `--table-filters-${id.replace(/[^a-zA-Z0-9-]/g, "")}`;

  if (!children && !filters && !onClear && !onSelected && !onAll) {
    return null;
  }
  const hasSelection = selected > 0;
  const exportAction = hasSelection ? onSelected : onAll;
  const exportLabel = hasSelection ? "Export selected CSV" : "Export full CSV";
  return (
    <div className={styles.toolbar}>
      <div className={styles.toolbarMain}>
        <div className={styles.queryActions}>
          <div className={styles.query}>{children}</div>
          {filters && (
            <Button
              variant={expanded ? "secondary" : "ghost"}
              size="compact"
              disabled={disabled}
              aria-expanded={expanded}
              aria-controls={panelId}
              popoverTarget={panelId}
              style={{ anchorName: anchor }}
              className="gap-1.5"
            >
              <Icon name="filter" />
              <span>Filters</span>
              {filterCount > 0 && (
                <span className={styles.filterCount} aria-label={`${filterCount} applied filters`}>
                  {filterCount}
                </span>
              )}
            </Button>
          )}
        </div>
        <div className={styles.exportActions} role="group" aria-label="Table actions">
          {hasSelection && (
            <span className={styles.meta} role="status">
              {selected} selected
            </span>
          )}
          {onClear && hasSelection && (
            <Button variant="ghost" size="compact" disabled={disabled} onClick={onClear}>
              Clear selection
            </Button>
          )}
          {exportAction && (
            <Button
              variant="secondary"
              size="compact"
              disabled={disabled || exporting || exportDisabled}
              onClick={exportAction}
            >
              {exporting ? "Preparing CSV…" : exportLabel}
            </Button>
          )}
        </div>
      </div>
      {filterSummary}
      {filters && (
        <FilterShelf
          id={panelId}
          anchor={anchor}
          defaultOpen={defaultFiltersExpanded}
          onOpenChange={(open) => {
            setExpanded(open);
            onFiltersOpenChange?.(open);
          }}
        >
          {filters}
        </FilterShelf>
      )}
    </div>
  );
}
