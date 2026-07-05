import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import styles from "./Performance.module.css";

export interface SectionState {
  hasData: boolean;
  busy: boolean;
  failed: boolean;
  retry: () => void;
}

// One flat section with a plain title and, on its right, what reads the section (a legend or totals). While its read
// is pending the section shows placeholders of the final geometry (`placeholder`), so the page does not move when the
// figures arrive.
export function PerformanceSection({
  title,
  half = false,
  aside,
  tools,
  state,
  placeholder,
  children,
}: {
  title: string;
  half?: boolean;
  aside?: ReactNode;
  // Controls for a long list (tabs, search). With them the heading block sticks to the top of its scroll pane, so
  // the controls stay in reach while the list scrolls under them.
  tools?: ReactNode;
  state: SectionState;
  placeholder: ReactNode;
  children: ReactNode;
}) {
  const headingId = `performance-${title.toLowerCase().replace(/[^a-z]+/g, "-")}`;
  const head = (
    <div className={styles.panelHead}>
      <h2 id={headingId} className={styles.panelTitle}>
        {title}
      </h2>
      {state.hasData && aside}
    </div>
  );
  return (
    <section
      className={`${styles.panel} ${half ? styles.half : ""}`.trim()}
      aria-labelledby={headingId}
      aria-busy={state.busy}
    >
      {tools ? (
        <div className={styles.stickyHead}>
          {head}
          {state.hasData && tools}
        </div>
      ) : (
        head
      )}
      {state.hasData ? (
        children
      ) : (
        <SectionFallback state={state} title={title} placeholder={placeholder} />
      )}
    </section>
  );
}

function SectionFallback({
  state,
  title,
  placeholder,
}: {
  state: SectionState;
  title: string;
  placeholder: ReactNode;
}) {
  if (state.failed && !state.busy) {
    return (
      <div className={styles.retry}>
        <p className={styles.note}>{title} could not be loaded.</p>
        <Button size="compact" variant="secondary" onClick={state.retry}>
          Retry
        </Button>
      </div>
    );
  }
  return (
    <div role="status" aria-label={`Loading ${title.toLowerCase()}`}>
      <span className="sr-only">Loading {title.toLowerCase()}…</span>
      <div aria-hidden="true">{placeholder}</div>
    </div>
  );
}
