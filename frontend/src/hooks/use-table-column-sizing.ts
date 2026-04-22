"use client";

import { useLayoutEffect, useState, type RefObject } from "react";
import type { DataColumn } from "@/components/ui/data-table/types";

export function useTableColumnSizing<Row>(
  table: RefObject<HTMLTableElement | null>,
  columns: readonly DataColumn<Row>[],
  sorting: boolean,
) {
  const signature = JSON.stringify(
    columns.map((column) => [
      column.id,
      column.label,
      Boolean(column.sortable && sorting),
      Boolean(column.filter),
    ]),
  );
  const [minimums, setMinimums] = useState<Record<string, number>>({});

  useLayoutEffect(() => {
    const headers = table.current?.querySelectorAll<HTMLElement>("th[data-column-id]");
    if (!headers) {
      return;
    }

    function measure() {
      const widths: Record<string, number> = {};
      headers?.forEach((header) => {
        const probe = header.querySelector<HTMLElement>("[data-header-minimum]");
        const id = header.dataset.columnId;
        if (!probe || !id) {
          return;
        }
        const style = getComputedStyle(header);
        widths[id] = Math.ceil(
          probe.getBoundingClientRect().width +
            parseFloat(style.paddingLeft) +
            parseFloat(style.paddingRight) +
            parseFloat(style.borderLeftWidth) +
            parseFloat(style.borderRightWidth),
        );
      });
      setMinimums((current) => {
        const unchanged =
          Object.keys(current).length === Object.keys(widths).length &&
          Object.entries(widths).every(([id, width]) => current[id] === width);
        return unchanged ? current : widths;
      });
    }

    const observer = new ResizeObserver(measure);
    headers.forEach((header) => {
      const probe = header.querySelector<HTMLElement>("[data-header-minimum]");
      if (probe) {
        observer.observe(probe);
      }
    });
    window.addEventListener("resize", measure);
    measure();
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", measure);
    };
  }, [table, signature]);

  return columns.map((column) => ({
    id: column.id,
    width: column.width,
    minWidth: Math.max(column.minWidth ?? 0, minimums[column.id] ?? 0) || undefined,
  }));
}
