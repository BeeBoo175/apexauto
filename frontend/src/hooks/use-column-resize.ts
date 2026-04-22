"use client";

import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { resizeColumnWidths } from "@/lib/column-resize";
import type { ResizeColumn } from "@/lib/column-resize";

export function useColumnResize(columns: readonly ResizeColumn[]) {
  const signature = JSON.stringify(columns.map(({ id, width }) => [id, width]));
  const constraints = JSON.stringify(columns.map(({ id, minWidth }) => [id, minWidth]));
  const [snapshot, setSnapshot] = useState({ signature, widths: {} as Record<string, number> });
  const columnWidths = snapshot.signature === signature ? snapshot.widths : {};
  const cleanup = useRef<(() => void) | null>(null);

  useEffect(() => () => cleanup.current?.(), [signature, constraints]);

  function handleResizeStart(divider: number, event: ReactPointerEvent<HTMLDivElement>) {
    const headers = event.currentTarget
      .closest("thead")
      ?.querySelectorAll<HTMLElement>("[data-column-id]");
    if (!headers || headers.length !== columns.length || event.button !== 0) {
      return;
    }
    event.preventDefault();
    cleanup.current?.();
    const handle = event.currentTarget;
    handle.dataset.resizing = "true";
    const initial = Object.fromEntries(
      columns.map((column, index) => [column.id, headers[index].getBoundingClientRect().width]),
    );
    setSnapshot({ signature, widths: initial });
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
      setSnapshot({ signature, widths: currentWidths });
    }

    function stop(pointer?: PointerEvent) {
      if (pointer && pointer.pointerId !== pointerId) {
        return;
      }
      document.removeEventListener("pointermove", move);
      document.removeEventListener("pointerup", stop);
      document.removeEventListener("pointercancel", stop);
      window.removeEventListener("blur", cancel);
      delete handle.dataset.resizing;
      cleanup.current = null;
    }

    function cancel() {
      stop();
    }

    cleanup.current = stop;
    document.addEventListener("pointermove", move);
    document.addEventListener("pointerup", stop);
    document.addEventListener("pointercancel", stop);
    window.addEventListener("blur", cancel);
  }

  return { columnWidths, handleResizeStart };
}
