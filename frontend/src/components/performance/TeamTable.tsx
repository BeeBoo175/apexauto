import { Avatar } from "@/components/ui/Avatar";
import { Badge } from "@/components/ui/Badge";
import { roleNames, type Role } from "@/lib/auth/actor";
import { personStatusLabels, type TeamPerson } from "@/lib/performance/team";
import type { TeamColumn } from "./TeamColumns";
import styles from "./Performance.module.css";

// People appear in fixed alphabetical order within each branch. With bars, every count column shares one scale
// so a row can be read against its colleagues; without them the same values read as a plain table.
export function TeamTable<Row extends TeamPerson>({
  rows,
  columns,
  bars,
  caption,
  onOpen,
}: {
  rows: Row[];
  columns: TeamColumn<Row>[];
  bars: boolean;
  caption: string;
  onOpen: (row: Row) => void;
}) {
  const maximums = new Map(
    columns.map((column) => [
      column.key,
      Math.max(1, ...rows.map((row) => column.value(row) ?? 0)),
    ]),
  );
  return (
    <div className={styles.scroll}>
      <table className={styles.table}>
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">Person</th>
            {columns.map((column) => (
              <th key={column.key} scope="col" className={styles.number}>
                {column.label}
                {column.now && <span className={styles.nowTag}> · Now</span>}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={`${row.personId}:${row.branchId}`}>
              <th scope="row">
                <button type="button" className={styles.personButton} onClick={() => onOpen(row)}>
                  <Avatar
                    accountId={row.personId}
                    name={row.name}
                    pictureId={row.pictureId}
                    size="sm"
                    detail={roleNames[row.role as Role] ?? row.role}
                    nested
                  />
                  <span>
                    {row.name}
                    <span className={styles.personFact}>{row.branchName}</span>
                  </span>
                  {row.status !== "Active" && (
                    <Badge variant="neutral">{personStatusLabels[row.status]}</Badge>
                  )}
                </button>
              </th>
              {columns.map((column) => {
                const value = column.value(row);
                const share = value === null ? 0 : value / (maximums.get(column.key) ?? 1);
                return (
                  <td key={column.key} className={styles.number}>
                    <span className={styles.cellValue}>{column.text(row)}</span>
                    {bars && column.bar && value !== null && (
                      <span className={styles.miniTrack} aria-hidden="true">
                        <span className={styles.miniFill} style={{ width: `${share * 100}%` }} />
                      </span>
                    )}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
