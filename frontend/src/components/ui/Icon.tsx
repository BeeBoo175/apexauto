import styles from "./Icon.module.css";
import type { IconName } from "@/lib/icons";

export function Icon({ name, spinning = false }: { name: IconName; spinning?: boolean }) {
  return (
    <span aria-hidden="true" data-spinning={spinning || undefined} className={styles.icon}>
      <svg width="24" height="24" viewBox="0 0 24 24" focusable="false">
        <use href={`#apex-icon-${name}`} />
      </svg>
    </span>
  );
}
