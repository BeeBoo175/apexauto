import styles from "./Icon.module.css";
import type { IconName } from "@/lib/icons";

export function Icon({
  name,
  spinning = false,
  grid = 24,
}: {
  name: IconName;
  spinning?: boolean;
  grid?: 16 | 24;
}) {
  return (
    <span aria-hidden="true" data-spinning={spinning || undefined} className={styles.icon}>
      <svg width={grid} height={grid} viewBox={`0 0 ${grid} ${grid}`} focusable="false">
        <use href={`#apex-icon-${name}`} />
      </svg>
    </span>
  );
}
