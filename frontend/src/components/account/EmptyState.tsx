import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import styles from "./EmptyState.module.css";

export function EmptyState({
  title,
  description,
  href,
  action,
  prefetch,
}: {
  title: string;
  description: string;
  href: string;
  action: string;
  prefetch?: boolean;
}) {
  return (
    <div className={styles.empty}>
      <p className={styles.title}>{title}</p>
      <p className={styles.description}>{description}</p>
      <Link href={href} prefetch={prefetch} className={buttonClassName("primary", "md")}>
        {action}
      </Link>
    </div>
  );
}
