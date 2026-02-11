"use client";

import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { resizeColumnWidths } from "./column-resize";
import type { DataColumn } from "./types";

export function useColumnResize<Row>(columns: readonly DataColumn<Row>[]) {
  const [columnWidths, setColumnWidths] = useState<Record<string, number>>({});
  const cleanup = useRef<(() => void) | null>(null);

  useEffect(() => () => cleanup.current?.(), []);

  function handleResizeStart(divider: number, event: ReactPointerEvent<HTMLDivElement>) {
    const headers = event.currentTarget
      .closest("thead")
      ?.querySelectorAll<HTMLElement>("[data-column-id]");
    if (!headers || headers.length !== columns.length || event.button !== 0) {
      return;
    }
    event.preventDefault();
    cleanup.current?.();
    const initial = Object.fromEntries(
      columns.map((column, index) => [column.id, headers[index].getBoundingClientRect().width]),
    );
    setColumnWidths(initial);
    let previousX = event.clientX;
    let currentWidths = initial;
    const pointerId = event.pointerId;

    function move(pointer: PointerEvent) {
      if (pointer.pointerId !== pointerId) {
        return;
      }
      const delta = pointer.clientX - previousX;
      previousX = pointer.clientX;
      // Reversing direction starts from the current layout, including after a clamped move.
      currentWidths = resizeColumnWidths(columns, currentWidths, divider, delta);
      setColumnWidths(currentWidths);
    }

    function stop(pointer?: PointerEvent) {
      if (pointer && pointer.pointerId !== pointerId) {
        return;
      }
      document.removeEventListener("pointermove", move);
      document.removeEventListener("pointerup", stop);
      document.removeEventListener("pointercancel", stop);
      cleanup.current = null;
    }

    cleanup.current = stop;
    document.addEventListener("pointermove", move);
    document.addEventListener("pointerup", stop);
    document.addEventListener("pointercancel", stop);
  }

  return { columnWidths, handleResizeStart };
}
