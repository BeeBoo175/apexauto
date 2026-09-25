"use client";

import type { ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";
import type { IconName } from "@/lib/icons";
import styles from "./GuideAccordionList.module.css";

export interface GuideTopic<TopicId extends string> {
  id: TopicId;
  label: string;
  icon: IconName;
  count: number;
}

export interface GuideAccordionRow {
  id: string;
  // Set when a link such as #credits should land on the row.
  anchorId?: string;
  panelId: string;
  title: string;
  expanded: boolean;
  onToggle: () => void;
  body: ReactNode;
}

interface GuideAccordionListProps<TopicId extends string> {
  railLabel: string;
  filterLabel: string;
  topics: readonly GuideTopic<TopicId>[];
  activeTopic: TopicId;
  onTopicChange: (topic: TopicId) => void;
  title: string;
  titleId: string;
  // Shown beside the title, such as how many rows match.
  summary?: string;
  rows: readonly GuideAccordionRow[];
  // Replaces the rows when there are none.
  emptyState?: ReactNode;
}

// A topic rail beside hairline rows that open and close; the FAQ and the legal notices share it.
export function GuideAccordionList<TopicId extends string>({
  railLabel,
  filterLabel,
  topics,
  activeTopic,
  onTopicChange,
  title,
  titleId,
  summary,
  rows,
  emptyState,
}: GuideAccordionListProps<TopicId>) {
  return (
    <div className={styles.split}>
      <aside className={styles.rail} aria-label={railLabel}>
        <div>
          <p className={styles.railHeading}>Topics</p>
          <div className={styles.topicNav} role="group" aria-label={filterLabel}>
            {topics.map((topic) => (
              <button
                key={topic.id}
                type="button"
                className={styles.topicButton}
                aria-pressed={activeTopic === topic.id}
                onClick={() => onTopicChange(topic.id)}
              >
                <span className={styles.topicLabel}>
                  <span className={styles.topicIcon} aria-hidden="true">
                    <Icon name={topic.icon} />
                  </span>
                  <span>{topic.label}</span>
                </span>
                <span className={styles.topicCount}>{topic.count}</span>
              </button>
            ))}
          </div>
        </div>
      </aside>

      <div className={styles.main}>
        <div className={styles.topBar}>
          <div className={styles.resultsMeta}>
            <h2 id={titleId} className={styles.resultsHeading}>
              {title}
            </h2>
            {summary && (
              <span className={styles.countText} role="status">
                {summary}
              </span>
            )}
          </div>
        </div>

        {rows.length === 0 && emptyState ? (
          <div className={styles.emptyState}>{emptyState}</div>
        ) : (
          <div className={styles.hairlineList}>
            {rows.map((row) => (
              <article key={row.id} id={row.anchorId} className={styles.accordionRow}>
                <button
                  type="button"
                  className={styles.accordionTrigger}
                  aria-expanded={row.expanded}
                  aria-controls={row.panelId}
                  onClick={row.onToggle}
                >
                  <span className={styles.triggerQuestion}>{row.title}</span>
                  <span className={styles.triggerIcon} aria-hidden="true">
                    <Icon name="down" />
                  </span>
                </button>
                {row.expanded && (
                  <div id={row.panelId} className={styles.accordionBody}>
                    {row.body}
                  </div>
                )}
              </article>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
