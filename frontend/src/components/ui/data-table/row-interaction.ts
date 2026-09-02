import type { KeyboardEvent, MouseEvent } from "react";

export function activateTableRow(event: MouseEvent<HTMLTableRowElement>, activate: () => void) {
  if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
    return;
  }
  const target = event.target as Element;
  if (
    target.closest("a, button, input, label, select, textarea, [contenteditable]") ||
    window.getSelection()?.isCollapsed === false
  ) {
    return;
  }
  event.currentTarget.focus({ preventScroll: true });
  activate();
}

export function activateTableRowKey(
  event: KeyboardEvent<HTMLTableRowElement>,
  activate: () => void,
) {
  if (
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
    activate();
  }
}
