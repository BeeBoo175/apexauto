import { useEffect, useRef, type RefObject } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { useColumnResize } from "@/hooks/use-column-resize";
import { activateTableRow, activateTableRowKey } from "./row-interaction";
import type { DataTableProps } from "./types";
import headerStyles from "./DataTableHeader.module.css";
import iconStyles from "./TableIcons.module.css";
import styles from "./DataTableMobile.module.css";

// The identity and metric columns split the width like two desktop columns; dragging the divider moves the split.
const splitColumns = [
  { id: "identity", width: 56, minWidth: 132 },
  { id: "metric", width: 44, minWidth: 96 },
];

export function DataTableMobile<Row>({
  props,
  toggle,
  containerRef,
}: {
  props: DataTableProps<Row>;
  toggle: (id: string) => void;
  containerRef: RefObject<HTMLDivElement | null>;
}) {
  const { mobile, rows, rowId, selection, disabled, onRowClick } = props;
  const checkbox = useRef<HTMLInputElement>(null);
  const selected = rows.filter((row) => selection?.ids.has(rowId(row))).length;
  const all = rows.length > 0 && selected === rows.length;
  useEffect(() => {
    if (checkbox.current) {
      checkbox.current.indeterminate = selected > 0 && !all;
    }
  }, [selected, all]);
  const { columnWidths, handleResizeStart } = useColumnResize(splitColumns, { touch: true });
  if (!mobile) {
    return null;
  }
  const sort = props.sorting?.value;
  const sorted = sort?.column === mobile.metricColumn;
  let sortDirection: "ascending" | "descending" | "none" | undefined;
  if (props.sorting) {
    sortDirection = "none";
  }
  if (sorted) {
    sortDirection = sort?.direction === "asc" ? "ascending" : "descending";
  }
  const shares = splitColumns.map((column) => columnWidths[column.id] ?? column.width);
  const total = shares[0] + shares[1];

  // The desktop header cycle: ascending, then descending, then the default order.
  function cycleSort() {
    if (!props.sorting || !mobile) {
      return;
    }
    if (sorted && sort?.direction === "desc") {
      props.sorting.onChange(null);
      return;
    }
    props.sorting.onChange({
      column: mobile.metricColumn,
      direction: sorted ? "desc" : "asc",
    });
  }

  return (
    <div ref={containerRef} className={styles.view}>
      {mobile.controls}
      <table className={styles.table}>
        <caption className="sr-only">{props.label}, comparative mobile view</caption>
        <colgroup>
          <col style={{ width: `${(shares[0] / total) * 100}%` }} />
          <col style={{ width: `${(shares[1] / total) * 100}%` }} />
        </colgroup>
        <thead>
          <tr>
            <th scope="col" data-column-id="identity" className={styles.identityColumn}>
              <div className={styles.identityHeader}>
                {selection && (
                  <label className={styles.selection}>
                    <span className="sr-only">Select all loaded rows</span>
                    <input
                      ref={checkbox}
                      type="checkbox"
                      checked={all}
                      disabled={disabled || rows.length === 0}
                      onChange={() => selection.onChange(new Set(all ? [] : rows.map(rowId)))}
                    />
                  </label>
                )}
                <span>{mobile.identityLabel}</span>
              </div>
              <div
                aria-hidden="true"
                data-resize-column="identity"
                className={`${headerStyles.resize} ${styles.divider}`}
                onPointerDown={(event) => {
                  if (!disabled) {
                    handleResizeStart(0, event);
                  }
                }}
              />
            </th>
            <th scope="col" data-column-id="metric" aria-sort={sortDirection}>
              <Button
                variant="ghost"
                size="compact"
                className={styles.sortButton}
                disabled={disabled || props.busy || !props.sorting}
                onClick={cycleSort}
                aria-label={`Sort by ${mobile.metricLabel}`}
              >
                <span>{mobile.metricLabel}</span>
                {sorted && (
                  <span aria-hidden="true" className={iconStyles.glyph}>
                    <Icon
                      name={
                        sort?.direction === "desc"
                          ? "table-sort-descending"
                          : "table-sort-ascending"
                      }
                      grid={16}
                    />
                  </span>
                )}
              </Button>
            </th>
          </tr>
        </thead>
        <tbody aria-busy={props.busy}>
          {rows.map((row) => (
            <tr
              key={rowId(row)}
              tabIndex={onRowClick && !disabled ? 0 : undefined}
              data-selected={selection?.ids.has(rowId(row)) || undefined}
              data-active={props.selectedRowId === rowId(row) || undefined}
              onClick={(event) => {
                if (onRowClick && !disabled) {
                  activateTableRow(event, () => onRowClick(row));
                }
              }}
              onKeyDown={(event) => {
                if (onRowClick && !disabled) {
                  activateTableRowKey(event, () => onRowClick(row));
                }
              }}
            >
              <td>
                <div className={styles.identity}>
                  {selection && (
                    <label className={styles.selection}>
                      <span className="sr-only">Select {rowId(row)}</span>
                      <input
                        type="checkbox"
                        checked={selection.ids.has(rowId(row))}
                        disabled={disabled}
                        onChange={() => toggle(rowId(row))}
                      />
                    </label>
                  )}
                  <div className={styles.identityContent}>{mobile.identity(row)}</div>
                </div>
              </td>
              <td>{mobile.metric(row)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
