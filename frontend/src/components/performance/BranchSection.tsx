import { formatCurrency } from "@/lib/currency-format";
import { changeFrom, formatDays } from "@/lib/performance/format";
import type { PerformanceBranches } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import styles from "./Performance.module.css";

export function BranchSection({
  branches,
  state,
}: {
  branches: PerformanceBranches | null;
  state: SectionState;
}) {
  return (
    <PerformanceSection title="Branch comparison" state={state}>
      {branches && (
        <div className={styles.scroll}>
          <table className={styles.table}>
            <caption className="sr-only">Headline measures for each branch</caption>
            <thead>
              <tr>
                <th scope="col">Branch</th>
                <th scope="col" className={styles.number}>
                  Delivered value
                </th>
                <th scope="col" className={styles.number}>
                  Vehicles delivered
                </th>
                <th scope="col" className={styles.number}>
                  Check-in to listed
                </th>
                <th scope="col" className={styles.number}>
                  Customer no-shows
                </th>
                <th scope="col">Deliveries against the previous period</th>
              </tr>
            </thead>
            <tbody>
              {branches.branches.map((branch) => (
                <tr key={branch.branchId}>
                  <th scope="row">{branch.branchName}</th>
                  <td className={styles.number}>{formatCurrency(branch.current.deliveredValue)}</td>
                  <td className={styles.number}>{branch.current.vehiclesDelivered}</td>
                  <td className={styles.number}>
                    {formatDays(branch.current.checkInToListedDays)}
                  </td>
                  <td className={styles.number}>{branch.current.customerNoShows}</td>
                  <td>
                    {
                      changeFrom(
                        branch.current.vehiclesDelivered,
                        branch.previous.vehiclesDelivered,
                      ).text
                    }
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PerformanceSection>
  );
}
