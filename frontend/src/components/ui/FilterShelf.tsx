import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import styles from "./FilterShelf.module.css";

export interface FilterShelfProps {
  children: ReactNode;
  id: string;
  anchor: string;
  defaultOpen?: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
}

export function FilterShelf({
  children,
  id,
  anchor,
  defaultOpen = false,
  onOpenChange,
  title = "Filters",
}: FilterShelfProps) {
  const panel = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(defaultOpen);

  const positionPanel = useCallback(() => {
    const shelf = panel.current;
    const trigger = Array.from(
      document.querySelectorAll<HTMLButtonElement>("button[popovertarget]"),
    ).find((button) => button.getAttribute("popovertarget") === id);
    if (!shelf || !trigger || !shelf.matches(":popover-open")) {
      return;
    }
    const bounds = trigger.getBoundingClientRect();
    const gutter = 12;
    const availableBelow = window.innerHeight - bounds.bottom - gutter - 8;
    const availableAbove = bounds.top - gutter - 8;
    const above =
      availableBelow < Math.min(shelf.scrollHeight, 300) && availableAbove > availableBelow;
    shelf.style.maxHeight = `${Math.max(80, above ? availableAbove : availableBelow)}px`;
    const left = Math.max(
      gutter,
      Math.min(bounds.right - shelf.offsetWidth, window.innerWidth - shelf.offsetWidth - gutter),
    );
    const top = above ? bounds.top - shelf.offsetHeight - 8 : bounds.bottom + 8;
    shelf.style.left = `${left}px`;
    shelf.style.top = `${Math.max(gutter, top)}px`;
  }, [id]);

  useEffect(() => {
    if (!open) {
      return;
    }
    const reposition = () => positionPanel();
    const observer = new ResizeObserver(reposition);
    if (panel.current) {
      observer.observe(panel.current);
    }
    positionPanel();
    window.addEventListener("resize", reposition);
    window.addEventListener("scroll", reposition, true);
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", reposition);
      window.removeEventListener("scroll", reposition, true);
    };
  }, [open, positionPanel]);

  useEffect(() => {
    if (defaultOpen) {
      panel.current?.showPopover();
    }
  }, [defaultOpen]);

  return (
    <div
      ref={panel}
      id={id}
      popover="auto"
      className={styles.shelf}
      role="region"
      aria-label={title}
      style={{ positionAnchor: anchor }}
      onToggle={(event) => {
        setOpen(event.newState === "open");
        onOpenChange(event.newState === "open");
        if (event.newState === "open") {
          requestAnimationFrame(positionPanel);
        }
      }}
    >
      <p className={styles.title}>{title}</p>
      {children}
    </div>
  );
}
