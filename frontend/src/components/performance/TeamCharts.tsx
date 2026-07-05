import type { CSSProperties } from "react";
import styles from "./Performance.module.css";

export interface ChartPerson {
  key: string;
  name: string;
  detail: string;
  onOpen: () => void;
}

export interface MatrixMeasure<Row> {
  key: string;
  label: string;
  value: (row: Row) => number | null;
  text: (row: Row) => string;
}

// A highlight matrix (the heat-table of analytics tools): people as rows in alphabetical order, measures as
// columns, each cell shaded against its own column's highest value with the figure printed in it. Every measure
// reads at once and the grid stays compact as the team grows; shading compares within a measure only, never across.
export function HighlightMatrix<Row>({
  people,
  rows,
  measures,
  caption,
}: {
  people: ChartPerson[];
  rows: Row[];
  measures: MatrixMeasure<Row>[];
  caption: string;
}) {
  const maxima = measures.map((measure) =>
    Math.max(1, ...rows.map((row) => measure.value(row) ?? 0)),
  );
  return (
    <div className={styles.scroll}>
      <table className={styles.matrix}>
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">Person</th>
            {measures.map((measure) => (
              <th key={measure.key} scope="col">
                {measure.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => {
            const person = people[index];
            return (
              <tr key={person.key}>
                <th scope="row">
                  <button
                    type="button"
                    className={styles.matrixPerson}
                    onClick={person.onOpen}
                    aria-label={`${person.name}, ${person.detail}. Open their period detail`}
                  >
                    <span>{person.name}</span>
                    <span className={styles.personMeta}>{person.detail}</span>
                  </button>
                </th>
                {measures.map((measure, column) => {
                  const value = measure.value(row);
                  const share = value === null ? 0 : value / maxima[column];
                  return (
                    <td
                      key={measure.key}
                      className={styles.matrixCell}
                      data-empty={!value}
                      data-strong={share > 0.6}
                      style={{ "--heat": share } as CSSProperties}
                    >
                      {measure.text(row)}
                    </td>
                  );
                })}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

// A dot plot for elapsed days: every person on one shared days axis, so the gap between two people reads as a
// distance in days rather than as a bar to be compared by size. People without a measurable interval say so.
export function DotPlot<Row>({
  people,
  rows,
  value,
  text,
  caption,
}: {
  people: ChartPerson[];
  rows: Row[];
  value: (row: Row) => number | null;
  text: (row: Row) => string;
  caption: string;
}) {
  const known = rows.map(value).filter((item): item is number => item !== null);
  if (known.length === 0) {
    return <p className={styles.note}>No measurable intervals in this period.</p>;
  }
  const maximum = Math.max(1, ...known);
  const ticks = [0, maximum / 2, maximum];
  return (
    <div className={styles.dotPlot} role="img" aria-label={caption}>
      <div className={styles.dotAxis} aria-hidden="true">
        <span />
        <span className={styles.dotTicks}>
          {ticks.map((tick) => (
            <span key={tick} style={{ left: `${(tick / maximum) * 100}%` }}>
              {tick < 1 ? Math.round(tick * 10) / 10 : Math.round(tick)}d
            </span>
          ))}
        </span>
        <span />
      </div>
      {rows.map((row, index) => {
        const person = people[index];
        const days = value(row);
        return (
          <button
            key={person.key}
            type="button"
            className={styles.dotRow}
            onClick={person.onOpen}
            aria-label={`${person.name}, ${text(row)}. Open their period detail`}
          >
            <span className={styles.barLabel}>{person.name}</span>
            <span className={styles.dotTrack} aria-hidden="true">
              {days !== null && (
                <span className={styles.dot} style={{ left: `${(days / maximum) * 100}%` }} />
              )}
            </span>
            <span className={days === null ? styles.absent : styles.dotValue}>{text(row)}</span>
          </button>
        );
      })}
    </div>
  );
}
