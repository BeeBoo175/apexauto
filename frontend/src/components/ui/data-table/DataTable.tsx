"use client";

import { useEffect, useId, useLayoutEffect, useRef } from "react";
import { useColumnResize } from "@/hooks/use-column-resize";
import { useTableColumnSizing } from "@/hooks/use-table-column-sizing";
import { columnLayout } from "@/lib/column-resize";
import { DataTableHeader } from "./DataTableHeader";
import { DataTablePagination } from "./DataTablePagination";
import { Button } from "@/components/ui/Button";
import type { DataTableProps } from "./types";
import styles from "./DataTable.module.css";

export function DataTable<Row>(props: DataTableProps<Row>) {
  const { rows, rowId, columns, selection, disabled } = props;
  const tableId = useId();
  const checkbox = useRef<HTMLInputElement>(null);
  const table = useRef<HTMLTableElement>(null);
  const confirmedPage = useRef(props.page);
  useLayoutEffect(() => {
    const previous = confirmedPage.current;
    confirmedPage.current = props.page;
    if (previous.page === props.page.page && previous.pageSize === props.page.pageSize) {
      return;
    }
    const scroller = table.current?.parentElement;
    if (!scroller) {
      return;
    }
    scroller.scrollTop = 0;
    if (
      window.matchMedia("(max-width: 820px)").matches &&
      scroller.getBoundingClientRect().top < 0
    ) {
      scroller.scrollIntoView({ block: "start", inline: "nearest", behavior: "instant" });
    }
  }, [props.page]);
  const sizing = useTableColumnSizing(table, columns, Boolean(props.sorting));
  const { columnWidths, handleResizeStart } = useColumnResize(sizing);
  const layout = columnLayout(sizing, columnWidths, Boolean(selection));
  const selectedCount = rows.filter((row) => selection?.ids.has(rowId(row))).length;
  const all = rows.length > 0 && selectedCount === rows.length;
  const emptyState =
    props.page.page > 1 || props.page.total > 0
      ? {
          title: "No records on this page",
          description: "Return to the first page to see the latest results for your criteria.",
          action: {
            label: "Go to first page",
            onClick: () => props.onPage(1, props.page.pageSize),
          },
        }
      : props.emptyState;
  useEffect(() => {
    if (checkbox.current) {
      checkbox.current.indeterminate = selectedCount > 0 && !all;
    }
  }, [selectedCount, all]);

  function toggle(id: string) {
    if (!selection) {
      return;
    }

    const ids = new Set(selection.ids);
    if (ids.has(id)) {
      ids.delete(id);
    } else {
      ids.add(id);
    }
    selection.onChange(ids);
  }

  return (
    <section
      className={`${styles.workspace} ${props.density === "comfortable" ? styles.comfortable : styles.fillHeight}`}
      aria-label={props.label}
    >
      {props.toolbar}
      <div
        className={`${styles.scroll} ${rows.length === 0 ? styles.emptyScroll : ""}`}
        role="region"
        aria-label={`${props.label} records`}
        tabIndex={0}
      >
        <table ref={table} className={styles.table} style={layout}>
          <caption className="sr-only">{props.label}</caption>
          <thead>
            <tr>
              {selection && (
                <th scope="col" className={styles.selectionCell}>
                  <label className={styles.selectionControl}>
                    <span className="sr-only">Select all loaded rows</span>
                    <input
                      id={`${tableId}-select-all`}
                      ref={checkbox}
                      type="checkbox"
                      disabled={disabled || rows.length === 0}
                      checked={all}
                      onChange={() => selection.onChange(new Set(all ? [] : rows.map(rowId)))}
                    />
                  </label>
                </th>
              )}
              {columns.map((column, index) => (
                <DataTableHeader
                  key={column.id}
                  column={column}
                  sorting={props.sorting}
                  disabled={disabled || Boolean(props.busy)}
                  resizable={index < columns.length - 1}
                  onStart={(event) => handleResizeStart(index, event)}
                />
              ))}
            </tr>
          </thead>
          <tbody aria-busy={props.busy}>
            {rows.map((row) => (
              <tr
                key={rowId(row)}
                className={props.onRowClick ? styles.interactiveRow : undefined}
                tabIndex={props.onRowClick && !disabled ? 0 : undefined}
                onKeyDown={(event) => {
                  if (
                    !props.onRowClick ||
                    disabled ||
                    event.defaultPrevented ||
                    event.repeat ||
                    event.target !== event.currentTarget ||
                    event.metaKey ||
                    event.ctrlKey ||
                    event.shiftKey ||
                    event.altKey
                  ) {
                    return;
                  }
                  if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    props.onRowClick(row);
                  }
                }}
                onClick={(event) => {
                  if (!props.onRowClick || disabled || event.defaultPrevented) {
                    return;
                  }
                  if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                    return;
                  }
                  const target = event.target as Element;
                  if (
                    target.closest(
                      "a, button, input, label, select, textarea, [contenteditable]",
                    ) ||
                    window.getSelection()?.isCollapsed === false
                  ) {
                    return;
                  }
                  event.currentTarget.focus({ preventScroll: true });
                  props.onRowClick(row);
                }}
                data-selected={
                  props.selectedRowId === rowId(row) || selection?.ids.has(rowId(row)) || undefined
                }
                data-active={props.selectedRowId === rowId(row) || undefined}
              >
                {selection && (
                  <td className={styles.selectionCell}>
                    <label className={styles.selectionControl}>
                      <span className="sr-only">Select {rowId(row)}</span>
                      <input
                        id={`${tableId}-select-${rowId(row)}`}
                        type="checkbox"
                        checked={selection.ids.has(rowId(row))}
                        disabled={disabled}
                        onChange={() => toggle(rowId(row))}
                      />
                    </label>
                  </td>
                )}
                {columns.map((column) => (
                  <td key={column.id} data-align={column.align ?? "left"}>
                    <div className={styles.cell}>{column.cell(row)}</div>
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {rows.length === 0 && (
        <div className={styles.empty} aria-busy={props.busy}>
          <div className={styles.emptyContent} role="status">
            <h3>{emptyState.title}</h3>
            <p>{emptyState.description}</p>
          </div>
          {emptyState.action && (
            <Button
              size="compact"
              variant="secondary"
              disabled={disabled || props.busy}
              onClick={emptyState.action.onClick}
            >
              {emptyState.action.label}
            </Button>
          )}
        </div>
      )}
      <DataTablePagination
        page={props.page}
        count={rows.length}
        disabled={disabled || props.busy}
        onPage={props.onPage}
      />
    </section>
  );
}
