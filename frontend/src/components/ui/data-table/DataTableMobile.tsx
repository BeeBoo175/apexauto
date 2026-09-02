import { Fragment, useEffect, useRef, type RefObject } from "react";
import { useColumnResize } from "@/hooks/use-column-resize";
import { MobileSortButton } from "./MobileSortButton";
import { activateTableRow, activateTableRowKey } from "./row-interaction";
import type { DataTableProps } from "./types";
import headerStyles from "./DataTableHeader.module.css";
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
  const { rows, rowId, selection, disabled, onRowClick } = props;
  const mobile = props.mobile === "columns" ? undefined : props.mobile;
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
  const sortable = (columnId: string) =>
    Boolean(props.sorting && props.columns.find((column) => column.id === columnId)?.sortable);
  const directionOf = (columnId: string) => (sort?.column === columnId ? sort.direction : null);
  const metricDirection = directionOf(mobile.metricColumn);
  let sortDirection: "ascending" | "descending" | "none" | undefined;
  if (props.sorting) {
    sortDirection = "none";
  }
  if (metricDirection) {
    sortDirection = metricDirection === "asc" ? "ascending" : "descending";
  }
  const shares = splitColumns.map((column) => columnWidths[column.id] ?? column.width);
  const total = shares[0] + shares[1];

  // The desktop header cycle: ascending, then descending, then the default order.
  function cycleSort(columnId: string) {
    if (!props.sorting) {
      return;
    }
    const current = directionOf(columnId);
    if (current === "desc") {
      props.sorting.onChange(null);
      return;
    }
    props.sorting.onChange({ column: columnId, direction: current ? "desc" : "asc" });
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
                <span className={styles.identityHeadings}>
                  {mobile.identityColumns.map((column, index) => (
                    <Fragment key={column.id}>
                      {index > 0 && <span aria-hidden="true">&amp;</span>}
                      <MobileSortButton
                        label={column.label}
                        sort={directionOf(column.id)}
                        disabled={disabled || props.busy || !sortable(column.id)}
                        onClick={() => cycleSort(column.id)}
                      />
                    </Fragment>
                  ))}
                </span>
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
              <MobileSortButton
                label={mobile.metricLabel}
                sort={metricDirection}
                disabled={disabled || props.busy || !sortable(mobile.metricColumn)}
                onClick={() => cycleSort(mobile.metricColumn)}
              />
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
