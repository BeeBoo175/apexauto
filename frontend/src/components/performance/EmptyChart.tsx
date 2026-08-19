import { EmptyNotice } from "@/components/ui/EmptyNotice";
import type { IconName } from "@/lib/icons";
import styles from "./Performance.module.css";

// Fixed heights, as a share of the chart, for the faded stand-in columns.
const columnShapes = [34, 58, 42, 70, 50, 62, 46, 66];

// The chart a period with nothing in it would have drawn: gridlines and a faded stand-in shape that are only a hint of
// where figures will appear, with the message laid over its middle. It keeps the geometry of the real chart, so the
// section does not change height when data arrives.
export function EmptyChart({
  art,
  icon,
  title,
  children,
}: {
  art: "columns" | "line";
  icon: IconName;
  title: string;
  children: string;
}) {
  return (
    <div className={styles.emptyChart}>
      <div className={styles.emptyChartArt} aria-hidden="true">
        {art === "columns" ? (
          <div className={styles.emptyChartColumns}>
            {columnShapes.map((height, index) => (
              <span key={index} style={{ height: `${height}%` }} />
            ))}
          </div>
        ) : (
          <svg viewBox="0 0 100 40" preserveAspectRatio="none" className={styles.emptyChartLine}>
            <polyline points="0,30 14,24 28,27 42,16 56,20 70,10 84,15 100,8" />
          </svg>
        )}
      </div>
      <div className={styles.emptyChartMessage}>
        <div className={styles.emptyChartCard}>
          <EmptyNotice icon={icon} title={title}>
            {children}
          </EmptyNotice>
        </div>
      </div>
    </div>
  );
}
