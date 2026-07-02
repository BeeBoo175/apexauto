import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import styles from "./Performance.module.css";

export interface SectionState {
  hasData: boolean;
  busy: boolean;
  failed: boolean;
  retry: () => void;
}

// A section keeps its own loading silhouette, its confirmed content and its own retry, so one slow or failed read
// never blanks the rest of the page.
export function PerformanceSection({
  title,
  now = false,
  half = false,
  shell = "rows",
  state,
  children,
}: {
  title: string;
  now?: boolean;
  half?: boolean;
  shell?: "tiles" | "rows";
  state: SectionState;
  children: ReactNode;
}) {
  const headingId = `performance-${title.toLowerCase().replace(/[^a-z]+/g, "-")}`;
  return (
    <section
      className={`${styles.section} ${half ? styles.half : ""}`.trim()}
      aria-labelledby={headingId}
      aria-busy={state.busy}
    >
      <div className={styles.sectionHeader}>
        <h2 id={headingId} className={styles.sectionTitle}>
          {title}
        </h2>
        {now && <span className={styles.nowTag}>Now</span>}
      </div>
      {state.hasData ? children : <SectionFallback shell={shell} state={state} title={title} />}
    </section>
  );
}

function SectionFallback({
  shell,
  state,
  title,
}: {
  shell: "tiles" | "rows";
  state: SectionState;
  title: string;
}) {
  if (state.failed && !state.busy) {
    return (
      <div className={styles.retry}>
        <p className={styles.note}>{title} could not be loaded.</p>
        <Button size="compact" variant="secondary" onClick={state.retry}>
          Retry {title.toLowerCase()}
        </Button>
      </div>
    );
  }
  return (
    <div role="status" aria-label={`Loading ${title.toLowerCase()}`}>
      <span className="sr-only">Loading {title.toLowerCase()}…</span>
      {shell === "tiles" ? (
        <div className={styles.shellTiles} aria-hidden="true">
          {[0, 1, 2, 3].map((index) => (
            <span key={index} className={`${styles.placeholder} ${styles.shellTile}`} />
          ))}
        </div>
      ) : (
        <div className={styles.shellRows} aria-hidden="true">
          {[0, 1, 2, 3].map((index) => (
            <span key={index} className={`${styles.placeholder} ${styles.shellBlock}`} />
          ))}
        </div>
      )}
    </div>
  );
}
