import type { ClosureBlocker, RetainedCategory } from "@/lib/accounts/customer-closure";
import styles from "./CloseAccountDialog.module.css";

const customerIntro =
  "Closing is blocked while these records still need you. Nothing is cancelled for you.";

export function BlockerList({
  blockers,
  intro = customerIntro,
}: {
  blockers: ClosureBlocker[];
  intro?: string;
}) {
  return (
    <>
      <p>{intro}</p>
      <ul className={styles.list}>
        {blockers.map((blocker) => (
          <li key={`${blocker.type}-${blocker.reference}`}>
            <strong>
              {blocker.type} {blocker.reference}
            </strong>
            <span>
              {blocker.status}
              {blocker.branchName ? `, ${blocker.branchName}` : ""}
            </span>
            <span>{blocker.resolution}</span>
          </li>
        ))}
      </ul>
    </>
  );
}

export function RetainedList({ retained }: { retained: RetainedCategory[] }) {
  return (
    <ul className={styles.list}>
      {retained.map((category) => (
        <li key={category.name}>
          <strong>{category.name}</strong>
          <span>{category.detail}</span>
          <span>Kept: {category.endsWhen}</span>
        </li>
      ))}
    </ul>
  );
}
