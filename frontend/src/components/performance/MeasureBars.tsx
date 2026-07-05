import type { ReactNode } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import styles from "./Performance.module.css";

export interface BarItem {
  key: string;
  // Who or what the bar is about: a person label or a branch name, with one quiet line beneath.
  label: ReactNode;
  detail?: string;
  value: number | null;
  text: string;
  // The same measure for the period before, drawn as a second, lighter bar.
  previous?: { value: number; text: string };
  onOpen?: () => void;
  // Or the page that holds this item's work.
  href?: string;
  openLabel?: string;
}

// One measure as horizontal bars on a shared scale: the figure is printed beside every bar, absent values read as
// words, and the order is whatever the caller gives (people stay alphabetical, so a bar is never a ranking). Each
// row opens its record when the caller supplies one.
export function MeasureBars({
  items,
  caption,
  empty,
  compact = false,
}: {
  items: BarItem[];
  caption: string;
  empty: string;
  // One line per item, for a chart that sits beside others in a grid.
  compact?: boolean;
}) {
  if (items.length === 0) {
    return <p className={styles.note}>{empty}</p>;
  }
  const maximum = Math.max(
    1,
    ...items.flatMap((item) => [item.value ?? 0, item.previous?.value ?? 0]),
  );
  const width = (value: number) => `${(value / maximum) * 100}%`;
  return (
    <ul
      className={`${styles.bars} ${compact ? styles.barsCompact : ""}`.trim()}
      aria-label={caption}
    >
      {items.map((item) => {
        const content = (
          <>
            <span className={styles.barLabel}>
              {item.label}
              {item.detail && <span className={styles.personMeta}>{item.detail}</span>}
            </span>
            <span className={styles.barTracks} aria-hidden="true">
              <span className={styles.barTrack}>
                <span className={styles.barFill} style={{ width: width(item.value ?? 0) }} />
              </span>
              {item.previous && (
                <span className={styles.barTrack}>
                  <span
                    className={`${styles.barFill} ${styles.barPrevious}`}
                    style={{ width: width(item.previous.value) }}
                  />
                </span>
              )}
            </span>
            <span className={styles.barValue}>
              <span className={item.value === null ? styles.absent : undefined}>{item.text}</span>
              {item.previous && (
                <span className={styles.barPreviousText}>{item.previous.text} before</span>
              )}
            </span>
          </>
        );
        return (
          <li key={item.key}>
            {item.href && (
              <StaffLink
                href={item.href}
                className={`${styles.barRow} ${styles.barButton}`}
                aria-label={item.openLabel}
              >
                {content}
              </StaffLink>
            )}
            {!item.href && item.onOpen && (
              <button
                type="button"
                className={`${styles.barRow} ${styles.barButton}`}
                onClick={item.onOpen}
                aria-label={item.openLabel}
              >
                {content}
              </button>
            )}
            {!item.href && !item.onOpen && <span className={styles.barRow}>{content}</span>}
          </li>
        );
      })}
    </ul>
  );
}

export interface MeasurePanel {
  key: string;
  title: string;
  // One quiet line under the title, such as the team total.
  summary?: string;
  items: BarItem[];
}

// Small multiples: every measure as its own compact chart, side by side, so all of them read at a glance without
// choosing one at a time. Panels share the hairline grid of the rest of the page.
export function MeasureGrid({ panels, empty }: { panels: MeasurePanel[]; empty: string }) {
  return (
    <div className={styles.multiples}>
      {panels.map((panel) => (
        <section key={panel.key} className={styles.multiple} aria-label={panel.title}>
          <div className={styles.multipleHead}>
            <h3 className={styles.multipleTitle}>{panel.title}</h3>
            {panel.summary && <span className={styles.panelAside}>{panel.summary}</span>}
          </div>
          <MeasureBars items={panel.items} caption={panel.title} empty={empty} compact />
        </section>
      ))}
    </div>
  );
}
