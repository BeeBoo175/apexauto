import type { ButtonHTMLAttributes, HTMLAttributes, ReactNode } from "react";
import styles from "./PhotoAccordion.module.css";

interface PhotoAccordionProps {
  // Row panels open sideways and stack on a phone; column panels always stack.
  direction?: "row" | "column";
  // Numbered steps are an ordered list.
  ordered?: boolean;
  // Shows placeholder blocks in place of the photographs and names.
  loading?: boolean;
  className?: string;
  children: ReactNode;
}

export function PhotoAccordion({
  direction = "row",
  ordered = false,
  loading = false,
  className,
  children,
}: PhotoAccordionProps) {
  const List = ordered ? "ol" : "ul";
  return (
    <List
      className={className ? `${styles.accordion} ${className}` : styles.accordion}
      data-direction={direction}
      data-loading={loading || undefined}
    >
      {children}
    </List>
  );
}

interface PhotoPanelProps {
  active: boolean;
  // The photograph, an Image with fill, kept at a fixed size behind the scrim.
  photo: ReactNode;
  // What the panel's button holds; without it the panel has no button and cannot be chosen.
  trigger?: ReactNode;
  triggerProps?: ButtonHTMLAttributes<HTMLButtonElement>;
  detailsProps?: HTMLAttributes<HTMLDivElement>;
  // The details shown over the photograph while the panel is open.
  children: ReactNode;
}

export function PhotoPanel({
  active,
  photo,
  trigger,
  triggerProps,
  detailsProps,
  children,
}: PhotoPanelProps) {
  return (
    <li className={styles.panel} data-active={active}>
      <div className={styles.photo}>{photo}</div>
      <div className={styles.scrim} aria-hidden="true" />
      {trigger && (
        <button type="button" className={styles.trigger} aria-expanded={active} {...triggerProps}>
          {trigger}
        </button>
      )}
      <div className={styles.details} inert={!active} {...detailsProps}>
        {children}
      </div>
    </li>
  );
}

// The name a closed panel keeps on its edge.
export function PhotoCollapsedName({ children }: { children: ReactNode }) {
  return (
    <span className={styles.collapsedName} aria-hidden="true">
      {children}
    </span>
  );
}
