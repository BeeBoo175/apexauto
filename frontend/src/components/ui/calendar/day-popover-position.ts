interface CellBounds {
  top: number;
  bottom: number;
  left: number;
  right: number;
}

export function dayPopoverPosition(
  cell: CellBounds,
  popup: { width: number; contentHeight: number },
  viewport: { width: number; height: number },
) {
  const gap = 8;
  const gutter = 8;
  const below = Math.max(0, viewport.height - gutter - cell.bottom - gap);
  const above = Math.max(0, cell.top - gap - gutter);
  const desiredHeight = Math.min(400, popup.contentHeight);
  const side = below >= desiredHeight || below >= above ? "below" : "above";
  const availableHeight = side === "below" ? below : above;
  const alignedLeft =
    cell.left + popup.width <= viewport.width - gutter ? cell.left : cell.right - popup.width;

  return {
    side,
    left: Math.max(gutter, Math.min(alignedLeft, viewport.width - popup.width - gutter)),
    top: side === "below" ? cell.bottom + gap : null,
    bottom: side === "above" ? viewport.height - cell.top + gap : null,
    maxHeight: Math.min(400, availableHeight),
  };
}
