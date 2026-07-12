import styles from "./Performance.module.css";

interface LegendItem {
  key: string;
  label: string;
  // The swatch's colour, shared with the mark it names: a colour for charts, a class for stacked bars.
  color?: string;
  className?: string;
}

// What each colour means, on the right of a section heading. Every chart and bar section uses this one list.
export function Legend({ label, items }: { label: string; items: LegendItem[] }) {
  return (
    <ul className={styles.legend} aria-label={label}>
      {items.map((item) => (
        <li key={item.key}>
          <span
            className={`${styles.swatch} ${item.className ?? ""}`.trim()}
            style={item.color ? { background: item.color } : undefined}
            aria-hidden="true"
          />
          {item.label}
        </li>
      ))}
    </ul>
  );
}
