"use client";

import { useId, useState, type ReactNode } from "react";
import { Button } from "./Button";
import { Icon } from "./Icon";
import { FilterShelf } from "./FilterShelf";
import styles from "./RecordToolbar.module.css";

export function RecordToolbar({
  children,
  filters,
  filterCount,
  filterSummary,
  actions,
  disabled,
  onFiltersOpenChange,
  defaultFiltersExpanded = false,
  summaryInline = false,
}: {
  children?: ReactNode;
  filters: ReactNode;
  filterCount: number;
  filterSummary: ReactNode;
  actions: ReactNode;
  disabled: boolean;
  onFiltersOpenChange?: (open: boolean) => void;
  defaultFiltersExpanded?: boolean;
  // A toolbar with no search shows the applied filters on the Filters row, right after the button, instead of on a
  // row of their own.
  summaryInline?: boolean;
}) {
  const [expanded, setExpanded] = useState(defaultFiltersExpanded);
  const id = useId();
  const panelId = `table-filters-${id}`;
  const anchor = `--table-filters-${id.replace(/[^a-zA-Z0-9-]/g, "")}`;

  return (
    <div className={styles.toolbar}>
      <div className={styles.toolbarMain}>
        <div className={styles.queryActions}>
          {children && <div className={styles.query}>{children}</div>}
          {filters && (
            <Button
              variant={expanded ? "secondary" : "ghost"}
              size="compact"
              disabled={disabled}
              aria-expanded={expanded}
              aria-controls={panelId}
              popoverTarget={panelId}
              style={{ anchorName: anchor }}
              className={styles.filterButton}
              data-filtered={filterCount > 0 || undefined}
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
          {summaryInline && filterSummary}
        </div>
        {actions}
      </div>
      {!summaryInline && filterSummary}
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
