import type { ReactNode } from "react";
import Link from "next/link";
import styles from "./ShowroomVisitDetails.module.css";

interface ShowroomVisitDetailsProps {
  title: ReactNode;
  address?: ReactNode;
  href: string;
  // Keeps the link out of the tab order while the showroom is still loading.
  loading?: boolean;
}

// What a showroom photograph panel says when open: its name, address and the way to plan a visit.
export function ShowroomVisitDetails({
  title,
  address,
  href,
  loading = false,
}: ShowroomVisitDetailsProps) {
  return (
    <div className={styles.layout}>
      <div className={styles.summary}>
        <h3 className={styles.name}>{title}</h3>
        {address && <p className={styles.address}>{address}</p>}
      </div>
      <Link href={href} tabIndex={loading ? -1 : undefined} className={styles.visit}>
        Plan a visit
      </Link>
    </div>
  );
}
