"use client";

import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useColumnResize } from "./use-column-resize";
import { Table, TableCell } from "../Table";
import { useToast } from "../Toast";
import { DataTableHeader } from "./DataTableHeader";
import { DataTableToolbar } from "./DataTableToolbar";
import { DataTablePagination } from "./DataTablePagination";
import { Button } from "@/components/ui/Button";
import { downloadCsv, selectedCsv } from "./csv";
import type { DataTableProps } from "./types";
import styles from "./DataTable.module.css";

export function DataTable<Row>(props: DataTableProps<Row>) {
  const { rows, rowId, selectionScope, disabled } = props;
  const columns = props.columns.filter((column) => !column.exportOnly);
  const [selection, setSelection] = useState<{ scope: string; ids: Set<string> }>({
    scope: selectionScope,
    ids: new Set(),
  });
  const [exporting, setExporting] = useState(false);
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
  const { columnWidths, handleResizeStart } = useColumnResize(columns);
  const tracks = columns.map((column) => {
    const minimum = `${column.minWidth ?? 100}px`;
    const resized = columnWidths[column.id];
    if (resized !== undefined) {
      return `${resized}px`;
    }
    return `minmax(${minimum}, ${column.width ?? 160}fr)`;
  });
  tracks.unshift("44px");
  const toast = useToast();
  // Selection belongs to exactly the confirmed query/page snapshot supplied by the caller.
  const selected = selection.scope === selectionScope ? selection.ids : new Set<string>();
  const selectedRows = rows.filter((row) => selected.has(rowId(row)));
  const all = rows.length > 0 && selectedRows.length === rows.length;
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
      checkbox.current.indeterminate = selectedRows.length > 0 && !all;
    }
  }, [selectedRows.length, all]);

  function toggle(id: string) {
    const ids = new Set(selected);
    if (ids.has(id)) {
      ids.delete(id);
    } else {
      ids.add(id);
    }
    setSelection({ scope: selectionScope, ids });
  }
  async function exportAll() {
    if (exporting) {
      return;
    }
    setExporting(true);
    try {
      await props.onExportAll();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "CSV export could not be completed.");
    } finally {
      setExporting(false);
    }
  }
  return (
    <section
      className={`${styles.workspace} ${props.fillHeight ? styles.fillHeight : ""}`}
      aria-label={props.label}
    >
      <DataTableToolbar
        filters={props.filters}
        filterCount={props.filterCount}
        filterSummary={props.filterSummary}
        onFiltersOpenChange={props.onFiltersOpenChange}
        defaultFiltersExpanded={props.defaultFiltersExpanded}
        selected={selectedRows.length}
        disabled={disabled}
        exporting={exporting}
        exportDisabled={props.busy || props.page.total === 0}
        onClear={() => setSelection({ scope: selectionScope, ids: new Set() })}
        onSelected={() =>
          downloadCsv(
            new Blob([selectedCsv(selectedRows, props.columns)], {
              type: "text/csv;charset=utf-8",
            }),
            `${props.exportFileName}-selected.csv`,
          )
        }
        onAll={() => void exportAll()}
      >
        {props.toolbar}
      </DataTableToolbar>
      {rows.length > 0 ? (
        <Table
          ref={table}
          density="compact"
          framed={false}
          className={styles.table}
          containerClassName={styles.scroll}
          style={{
            gridTemplateColumns: tracks.join(" "),
          }}
        >
          <caption className="sr-only">{props.label}</caption>
          <thead>
            <tr>
              <th scope="col" className={styles.selectionCell}>
                <label className={styles.selectionControl}>
                  <span className="sr-only">Select all loaded rows</span>
                  <input
                    ref={checkbox}
                    type="checkbox"
                    disabled={disabled || rows.length === 0}
                    checked={all}
                    onChange={() =>
                      setSelection({
                        scope: selectionScope,
                        ids: new Set(all ? [] : rows.map(rowId)),
                      })
                    }
                  />
                </label>
              </th>
              {columns.map((column, index) => (
                <DataTableHeader
                  key={column.id}
                  column={column}
                  sort={props.sort}
                  disabled={disabled}
                  resizable={index < columns.length - 1}
                  onSort={props.onSort}
                  onStart={(event) => handleResizeStart(index, event)}
                />
              ))}
            </tr>
          </thead>
          <tbody aria-busy={props.busy}>
            {rows.map((row) => (
              <tr
                key={rowId(row)}
                className={props.onRowClick && !disabled ? styles.interactiveRow : undefined}
                tabIndex={props.onRowClick ? -1 : undefined}
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
                  props.selectedRowId === rowId(row) || selected.has(rowId(row)) || undefined
                }
                data-active={props.selectedRowId === rowId(row) || undefined}
              >
                <TableCell className={styles.selectionCell}>
                  <label className={styles.selectionControl}>
                    <span className="sr-only">Select {rowId(row)}</span>
                    <input
                      type="checkbox"
                      checked={selected.has(rowId(row))}
                      disabled={disabled}
                      onChange={() => toggle(rowId(row))}
                    />
                  </label>
                </TableCell>
                {columns.map((column) => (
                  <TableCell key={column.id} style={{ textAlign: column.align ?? "left" }}>
                    <div className={styles.cell}>{column.cell(row)}</div>
                  </TableCell>
                ))}
              </tr>
            ))}
          </tbody>
        </Table>
      ) : (
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
        disabled={disabled}
        onPage={props.onPage}
      />
    </section>
  );
}
