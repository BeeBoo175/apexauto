import styles from "./Performance.module.css";

export interface BarRow {
  key: string;
  label: string;
  value: number;
  valueText: string;
  detail: string;
}

// Aligned horizontal bars sharing one scale, or the same values as a table for screen readers and keyboard users.
export function PerformanceBars({
  rows,
  asTable,
  caption,
  valueHeading,
  detailHeading,
}: {
  rows: BarRow[];
  asTable: boolean;
  caption: string;
  valueHeading: string;
  detailHeading: string;
}) {
  if (asTable) {
    return (
      <div className={styles.scroll}>
        <table className={styles.table}>
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col" className={styles.number}>
                {valueHeading}
              </th>
              <th scope="col">{detailHeading}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.key}>
                <th scope="row">{row.label}</th>
                <td className={styles.number}>{row.valueText}</td>
                <td>{row.detail}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }
  const maximum = Math.max(1, ...rows.map((row) => row.value));
  return (
    <ul className={styles.bars} aria-label={caption}>
      {rows.map((row) => (
        <li key={row.key} className={styles.barRow}>
          <span className={styles.barLabel}>{row.label}</span>
          <span className={styles.track} aria-hidden="true">
            <span className={styles.fill} style={{ width: `${(row.value / maximum) * 100}%` }} />
          </span>
          <span className={styles.barValue}>
            {row.valueText}
            <span className="sr-only">, {row.detail}</span>
          </span>
        </li>
      ))}
    </ul>
  );
}
