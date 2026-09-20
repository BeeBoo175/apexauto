"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { faqCategories, type FaqCategory, type FaqEntry } from "@/lib/guide/faq-items";
import type { IconName } from "@/lib/icons";
import styles from "./FaqExplorer.module.css";

const categoryIcons: Record<FaqCategory, IconName> = {
  All: "help",
  "Buying & warranty": "car",
  "Inspection & grades": "inspection",
  "Selling & trade-in": "tool",
  "Branches & test drives": "map-pin",
};

export function FaqExplorer({
  faqEntries,
  query,
  onResetQuery,
}: {
  faqEntries: readonly FaqEntry[];
  query: string;
  onResetQuery: () => void;
}) {
  const [category, setCategory] = useState<FaqCategory>("All");
  const [openIds, setOpenIds] = useState<string[]>([faqEntries[0].id]);

  const normalizedQuery = query.trim().toLowerCase();
  const visibleEntries = faqEntries.filter((entry) => {
    if (category !== "All" && entry.category !== category) {
      return false;
    }
    if (!normalizedQuery) {
      return true;
    }
    return (
      entry.question.toLowerCase().includes(normalizedQuery) ||
      entry.answer.toLowerCase().includes(normalizedQuery) ||
      entry.category.toLowerCase().includes(normalizedQuery)
    );
  });

  function countFor(cat: FaqCategory) {
    if (cat === "All") {
      return faqEntries.length;
    }
    return faqEntries.filter((entry) => entry.category === cat).length;
  }

  function toggleQuestion(id: string) {
    setOpenIds((prev) => (prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]));
  }

  return (
    <div className={styles.faqSplit}>
      <aside className={styles.faqRail} aria-label="FAQ topics">
        <div>
          <p className={styles.railHeading}>Topics</p>
          <div className={styles.topicNav} role="group" aria-label="Filter FAQ topics">
            {faqCategories.map((item) => (
              <button
                key={item}
                type="button"
                className={styles.topicButton}
                aria-pressed={category === item}
                onClick={() => setCategory(item)}
              >
                <span className={styles.topicLabel}>
                  <span className={styles.topicIcon} aria-hidden="true">
                    <Icon name={categoryIcons[item]} />
                  </span>
                  <span>{item}</span>
                </span>
                <span className={styles.topicCount}>{countFor(item)}</span>
              </button>
            ))}
          </div>
        </div>
      </aside>

      <div className={styles.faqMain}>
        <div className={styles.faqTopBar}>
          <div className={styles.resultsMeta}>
            <h2 id="faq-list-heading" className={styles.resultsHeading}>
              {category === "All" ? "All questions" : category}
            </h2>
            <span className={styles.countText} role="status">
              {visibleEntries.length} of {faqEntries.length}
            </span>
          </div>
        </div>

        {visibleEntries.length === 0 ? (
          <div className={styles.emptyState}>
            <p>No questions match &ldquo;{query.trim()}&rdquo;.</p>
            <Button
              type="button"
              variant="secondary"
              size="md"
              onClick={() => {
                setCategory("All");
                onResetQuery();
              }}
            >
              Reset topic and search
            </Button>
          </div>
        ) : (
          <div className={styles.hairlineList}>
            {visibleEntries.map((entry) => {
              const expanded = openIds.includes(entry.id);
              const panelId = `faq-panel-${entry.id}`;
              return (
                <article key={entry.id} className={styles.accordionRow}>
                  <button
                    type="button"
                    className={styles.accordionTrigger}
                    aria-expanded={expanded}
                    aria-controls={panelId}
                    onClick={() => toggleQuestion(entry.id)}
                  >
                    <span className={styles.triggerQuestion}>{entry.question}</span>
                    <span className={styles.triggerIcon} aria-hidden="true">
                      <Icon name="down" />
                    </span>
                  </button>
                  {expanded && (
                    <div id={panelId} className={styles.accordionBody}>
                      <p>{entry.answer}</p>
                      <Link href={entry.href} prefetch={false} className={layout.arrowLink}>
                        <span>{entry.linkLabel}</span>
                        <Icon name="next" />
                      </Link>
                    </div>
                  )}
                </article>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
