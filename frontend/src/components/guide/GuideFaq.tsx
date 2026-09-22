import Link from "next/link";
import type { FaqEntry } from "@/lib/guide/faq-items";
import styles from "./GuideFaq.module.css";

// The same entries as the main FAQ page, narrowed to the ones that concern pricing, grades and cover.
export function GuideFaq({ entries }: { entries: readonly FaqEntry[] }) {
  return (
    <div className={styles.faq}>
      <h2 className={styles.heading}>Common questions</h2>
      <div className={styles.list}>
        {entries.map((entry) => (
          <details key={entry.id} className={styles.item}>
            <summary>{entry.question}</summary>
            <p>{entry.answer}</p>
            <Link href={entry.href} prefetch={false} className={styles.itemLink}>
              {entry.linkLabel}
            </Link>
          </details>
        ))}
      </div>
      <Link href="/faq" className={styles.allQuestions}>
        See all questions
      </Link>
    </div>
  );
}
