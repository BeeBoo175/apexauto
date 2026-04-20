import { useEffect, useRef, type ReactNode } from "react";
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
      onToggle={(event) => onOpenChange(event.newState === "open")}
    >
      <p className={styles.title}>{title}</p>
      {children}
    </div>
  );
}
