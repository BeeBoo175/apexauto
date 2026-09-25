"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { faqCategories, type FaqCategory, type FaqEntry } from "@/lib/guide/faq-items";
import type { IconName } from "@/lib/icons";
import { GuideAccordionList, type GuideAccordionRow } from "./GuideAccordionList";

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

  const topics = faqCategories.map((item) => ({
    id: item,
    label: item,
    icon: categoryIcons[item],
    count: countFor(item),
  }));

  const rows: GuideAccordionRow[] = visibleEntries.map((entry) => ({
    id: entry.id,
    panelId: `faq-panel-${entry.id}`,
    title: entry.question,
    expanded: openIds.includes(entry.id),
    onToggle: () => toggleQuestion(entry.id),
    body: (
      <>
        <p>{entry.answer}</p>
        <Link href={entry.href} prefetch={false} className={layout.arrowLink}>
          <span>{entry.linkLabel}</span>
          <Icon name="next" />
        </Link>
      </>
    ),
  }));

  return (
    <GuideAccordionList
      railLabel="FAQ topics"
      filterLabel="Filter FAQ topics"
      topics={topics}
      activeTopic={category}
      onTopicChange={setCategory}
      title={category === "All" ? "All questions" : category}
      titleId="faq-list-heading"
      summary={`${visibleEntries.length} of ${faqEntries.length}`}
      rows={rows}
      emptyState={
        <>
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
        </>
      }
    />
  );
}
